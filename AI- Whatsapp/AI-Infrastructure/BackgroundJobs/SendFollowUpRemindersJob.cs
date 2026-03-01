using System;
using System.Diagnostics;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.Logging;

namespace EcomAI.Platform.Infrastructure.BackgroundJobs;

public class SendFollowUpRemindersJob
{
    private static readonly ActivitySource ActivitySource = new("EcomAI.BackgroundJobs.SendFollowUpReminders");
    private readonly IMessageRepository _messageRepository;
    private readonly IMetaMessagingService _metaMessagingService;
    private readonly ILogger<SendFollowUpRemindersJob> _logger;

    public SendFollowUpRemindersJob(
        IMessageRepository messageRepository,
        IMetaMessagingService metaMessagingService,
        ILogger<SendFollowUpRemindersJob> logger)
    {
        _messageRepository = messageRepository;
        _metaMessagingService = metaMessagingService;
        _logger = logger;
    }

    public async Task Execute()
    {
        using var activity = ActivitySource.StartActivity("SendFollowUpReminders.Execute");
        _logger.LogInformation("SendFollowUpReminders job started at {UtcNow}", DateTime.UtcNow);

        // TODO: Implement real query for abandoned cart intent + 24h window enforcement.
        // Stub intentionally no-op to avoid accidental non-compliant messaging.
        await Task.CompletedTask;

        _logger.LogInformation("SendFollowUpReminders job completed. Follow-ups sent: {Count}", 0);
    }
}
