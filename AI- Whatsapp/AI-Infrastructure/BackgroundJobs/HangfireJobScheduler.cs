using System;
using Hangfire;

namespace EcomAI.Platform.Infrastructure.BackgroundJobs;

public class HangfireJobScheduler
{
    public void RegisterAllJobs()
    {
        //RecurringJob.AddOrUpdate<PublishScheduledPostsJob>(
        //    recurringJobId: "publish-scheduled-posts",
        //    methodCall: job => job.Execute(),
        //    cronExpression: "*/5 * * * *",
        //    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        //RecurringJob.AddOrUpdate<SendFollowUpRemindersJob>(
        //    recurringJobId: "send-follow-up-reminders",
        //    methodCall: job => job.Execute(),
        //    cronExpression: "0 * * * *",
        //    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // ── Marketing Engine jobs ─────────────────────────────────────────────
        // MetaSyncJob: every 6 hours — pulls campaign + ad set performance from Meta
        RecurringJob.AddOrUpdate<MetaSyncJob>(
            recurringJobId: "meta-ads-sync",
            methodCall: job => job.Execute(),
            cronExpression: "0 */6 * * *",
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // MarketingAgentJob: daily at 04:00 UTC (09:00 PKT) — Claude decision + Meta write
        RecurringJob.AddOrUpdate<MarketingAgentJob>(
            recurringJobId: "marketing-agent-daily",
            methodCall: job => job.Execute(),
            cronExpression: "0 4 * * *",
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // BudgetGuardJob: every hour — enforces daily spend cap and scale limits
        RecurringJob.AddOrUpdate<BudgetGuardJob>(
            recurringJobId: "budget-guard",
            methodCall: job => job.Execute(),
            cronExpression: "0 * * * *",
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // OutcomeTrackerJob: every Sunday at 06:00 UTC — evaluates past decisions
        RecurringJob.AddOrUpdate<OutcomeTrackerJob>(
            recurringJobId: "outcome-tracker-weekly",
            methodCall: job => job.Execute(),
            cronExpression: "0 6 * * 0",
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}
