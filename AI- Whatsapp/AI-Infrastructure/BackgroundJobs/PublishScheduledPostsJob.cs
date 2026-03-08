using System;
using System.Diagnostics;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence.Repositories;
using Polly;
using Polly.Retry;

namespace EcomAI.Platform.Infrastructure.BackgroundJobs;

public class PublishScheduledPostsJob
{
    private static readonly ActivitySource ActivitySource = new("EcomAI.BackgroundJobs.PublishScheduledPosts");
    private readonly ScheduledPostRepository _scheduledPostRepository;
    private readonly IMetaMessagingService _metaMessagingService;
    private readonly IApplicationLogger _logger;

    public PublishScheduledPostsJob(
        ScheduledPostRepository scheduledPostRepository,
        IMetaMessagingService metaMessagingService,
        IApplicationLogger logger)
    {
        _scheduledPostRepository = scheduledPostRepository;
        _metaMessagingService = metaMessagingService;
        _logger = logger;
    }

    public async Task Execute()
    {
        using var activity = ActivitySource.StartActivity("PublishScheduledPosts.Execute");

        _logger.Info("PublishScheduledPosts job started at {UtcNow}", DateTime.UtcNow);

        var readyPosts = await _scheduledPostRepository.GetReadyToPublishAsync(DateTime.UtcNow);
        if (readyPosts.Count == 0)
        {
            _logger.Info("No scheduled posts ready to publish");
            return;
        }

        var retryPolicy = CreateRetryPolicy();
        foreach (var post in readyPosts)
        {
            try
            {
                await retryPolicy.ExecuteAsync(async () =>
                {
                    var tenantId = post.TenantId ?? throw new InvalidOperationException("Scheduled post is missing tenant context.");
                    // TODO: replace placeholder recipient with platform-appropriate publish endpoint/input.
                    var sendResult = await _metaMessagingService.SendTextMessageAsync(
                        tenantId,
                        post.Platform,
                        recipient: "scheduled-post-publisher",
                        messageText: post.Content,
                        messagingType: "UPDATE");

                    if (!sendResult.Success)
                    {
                        throw new InvalidOperationException(sendResult.ErrorMessage ?? "Unknown Meta send error");
                    }

                    post.MarkPublished(sendResult.MessageId ?? "unknown");
                    await _scheduledPostRepository.UpdateAsync(post);
                    await _scheduledPostRepository.SaveChangesAsync();
                });

                _logger.Info("Scheduled post {PostId} published for tenant {TenantId}", post.Id, post.TenantId);
            }
            catch (Exception ex)
            {
                post.MarkFailed();
                await _scheduledPostRepository.UpdateAsync(post);
                await _scheduledPostRepository.SaveChangesAsync();
                _logger.Error(ex, "Failed to publish scheduled post {PostId} for tenant {TenantId}", post.Id, post.TenantId);
            }
        }
    }

    private AsyncRetryPolicy CreateRetryPolicy()
    {
        return Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, delay, attempt, _) =>
                {
                    _logger.Warning(
                        exception,
                        "Retry {Attempt} for scheduled post publish after {DelaySeconds}s",
                        attempt,
                        delay.TotalSeconds);
                });
    }
}

