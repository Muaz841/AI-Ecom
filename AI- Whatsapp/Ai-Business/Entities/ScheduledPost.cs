using System;

namespace EcomAI.Platform.Business.Entities;

public class ScheduledPost : Entity<Guid>, ITenantEntity
{
    public string Platform { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public string? MediaUrl { get; private set; }
    public DateTime ScheduledFor { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public PostStatus Status { get; private set; } = PostStatus.Pending;
    public string? MetaPostId { get; private set; }
    public bool ModerationApproved { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private ScheduledPost()
    {
    }

    public static ScheduledPost Create(
        Guid tenantId,
        string platform,
        string content,
        string? mediaUrl,
        DateTime scheduledFor)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId required", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(platform))
        {
            throw new ArgumentException("Platform required", nameof(platform));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content required", nameof(content));
        }

        if (scheduledFor <= DateTime.UtcNow)
        {
            throw new ArgumentException("Scheduled time must be in future", nameof(scheduledFor));
        }

        return new ScheduledPost
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Platform = platform.Trim().ToLowerInvariant(),
            Content = content.Trim(),
            MediaUrl = mediaUrl?.Trim(),
            ScheduledFor = scheduledFor,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkPublished(string metaPostId)
    {
        if (string.IsNullOrWhiteSpace(metaPostId))
        {
            throw new ArgumentException("MetaPostId required", nameof(metaPostId));
        }

        if (Status is not (PostStatus.Pending or PostStatus.Approved))
        {
            throw new InvalidOperationException("Cannot publish non-pending post");
        }

        Status = PostStatus.Published;
        PublishedAt = DateTime.UtcNow;
        MetaPostId = metaPostId.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetModerationResult(bool approved)
    {
        ModerationApproved = approved;
        Status = approved ? PostStatus.Approved : PostStatus.Rejected;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        Status = PostStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum PostStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Published = 3,
    Failed = 4
}


