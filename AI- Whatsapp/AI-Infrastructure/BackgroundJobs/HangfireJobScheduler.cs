using System;
using Hangfire;

namespace EcomAI.Platform.Infrastructure.BackgroundJobs;

public class HangfireJobScheduler
{
    public void RegisterAllJobs()
    {
        RecurringJob.AddOrUpdate<PublishScheduledPostsJob>(
            recurringJobId: "publish-scheduled-posts",
            methodCall: job => job.Execute(),
            cronExpression: "*/5 * * * *",
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        RecurringJob.AddOrUpdate<SendFollowUpRemindersJob>(
            recurringJobId: "send-follow-up-reminders",
            methodCall: job => job.Execute(),
            cronExpression: "0 * * * *",
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}
