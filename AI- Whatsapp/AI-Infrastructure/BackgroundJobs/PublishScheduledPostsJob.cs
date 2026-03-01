using System;
using System.Diagnostics;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace EcomAI.Platform.Infrastructure.BackgroundJobs;

public class PublishScheduledPostsJob
{
    private static readonly ActivitySource ActivitySource = new("EcomAI.BackgroundJobs.PublishScheduledPosts");
    private readonly ScheduledPostRepository _scheduledPostRepository;
    private readonly IMetaMessagingService _metaMessagingService;
    private readonly ILogger<PublishScheduledPostsJob> _logger;

    public PublishScheduledPostsJob(
        ScheduledPostRepository scheduledPostRepository,
        IMetaMessagingService metaMessagingService,
        ILogger<PublishScheduledPostsJob> logger)
    {
        _scheduledPostRepository = scheduledPostRepository;
        _metaMessagingService = metaMessagingService;
        _logger = logger;
    }

    public async Task Execute()
    {
        using var activity = ActivitySource.StartActivity("PublishScheduledPosts.Execute");

        _logger.LogInformation("PublishScheduledPosts job started at {UtcNow}", DateTime.UtcNow);

        var readyPosts = await _scheduledPostRepository.GetReadyToPublishAsync(DateTime.UtcNow);
        if (readyPosts.Count == 0)
        {
            _logger.LogInformation("No scheduled posts ready to publish");
            return;
        }

        var retryPolicy = CreateRetryPolicy();
        foreach (var post in readyPosts)
        {
            try
            {
                await retryPolicy.ExecuteAsync(async () =>
                {
                    // TODO: replace placeholder recipient with platform-appropriate publish endpoint/input.
                    var sendResult = await _metaMessagingService.SendTextMessageAsync(
                        post.ClientId,
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

                _logger.LogInformation("Scheduled post {PostId} published for client {ClientId}", post.Id, post.ClientId);
            }
            catch (Exception ex)
            {
                post.MarkFailed();
                await _scheduledPostRepository.UpdateAsync(post);
                await _scheduledPostRepository.SaveChangesAsync();
                _logger.LogError(ex, "Failed to publish scheduled post {PostId} for client {ClientId}", post.Id, post.ClientId);
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
                    _logger.LogWarning(
                        exception,
                        "Retry {Attempt} for scheduled post publish after {DelaySeconds}s",
                        attempt,
                        delay.TotalSeconds);
                });
    }
}
