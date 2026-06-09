using System;

namespace EcomAI.Platform.Business.Entities;

// ── Host-level singleton: Marketing Engine configuration ──────────────────────
// Stores Claude API credentials, Meta Ads credentials, and engine safety params.
// NOT ITenantEntity — global row, TenantId always null.
// Repository MUST use IgnoreQueryFilters() when reading in a tenant request context.
public sealed class PlatformMarketingConfig : Entity<Guid>
{
    /// <summary>Encrypted Claude API key via ITokenProtector.</summary>
    public string? ClaudeApiKeyProtected    { get; private set; }

    /// <summary>Claude model used for daily decision-making. e.g. "claude-opus-4-6"</summary>
    public string ClaudeDecisionModel       { get; private set; } = "claude-opus-4-6";

    /// <summary>Claude model used for summaries and copy generation. e.g. "claude-haiku-4-5-20251001"</summary>
    public string ClaudeSummaryModel        { get; private set; } = "claude-haiku-4-5-20251001";

    /// <summary>Meta Ads Manager account ID (act_xxxxxxxxxxxxxxx).</summary>
    public string? MetaAdsAccountId         { get; private set; }

    /// <summary>Encrypted Meta Ads access token with ads_management scope.</summary>
    public string? MetaAdsAccessTokenProtected { get; private set; }

    /// <summary>When true, all Meta write actions are logged but NOT sent to the API.</summary>
    public bool DryRun                      { get; private set; } = true;

    /// <summary>Hard cap on agent actions per tenant per day.</summary>
    public int MaxActionsPerDay             { get; private set; } = 10;

    /// <summary>Maximum USD spend cap per day before BudgetGuard pauses all ad sets.</summary>
    public decimal DailySpendCapUsd         { get; private set; } = 100m;

    public bool IsConfigured => ClaudeApiKeyProtected != null;

    public DateTime UpdatedAt { get; private set; }

    private PlatformMarketingConfig() { }

    public static PlatformMarketingConfig Create(
        string? claudeApiKeyProtected,
        string claudeDecisionModel,
        string claudeSummaryModel,
        string? metaAdsAccountId,
        string? metaAdsAccessTokenProtected,
        bool dryRun,
        int maxActionsPerDay,
        decimal dailySpendCapUsd)
    {
        return new PlatformMarketingConfig
        {
            Id                          = Guid.NewGuid(),
            ClaudeApiKeyProtected       = claudeApiKeyProtected,
            ClaudeDecisionModel         = string.IsNullOrWhiteSpace(claudeDecisionModel) ? "claude-opus-4-6" : claudeDecisionModel.Trim(),
            ClaudeSummaryModel          = string.IsNullOrWhiteSpace(claudeSummaryModel) ? "claude-haiku-4-5-20251001" : claudeSummaryModel.Trim(),
            MetaAdsAccountId            = string.IsNullOrWhiteSpace(metaAdsAccountId) ? null : metaAdsAccountId.Trim(),
            MetaAdsAccessTokenProtected = metaAdsAccessTokenProtected,
            DryRun                      = dryRun,
            MaxActionsPerDay            = maxActionsPerDay > 0 ? maxActionsPerDay : 10,
            DailySpendCapUsd            = dailySpendCapUsd > 0 ? dailySpendCapUsd : 100m,
            UpdatedAt                   = DateTime.UtcNow,
        };
    }

    public void Update(
        string? claudeApiKeyProtected,
        string claudeDecisionModel,
        string claudeSummaryModel,
        string? metaAdsAccountId,
        string? metaAdsAccessTokenProtected,
        bool dryRun,
        int maxActionsPerDay,
        decimal dailySpendCapUsd)
    {
        if (claudeApiKeyProtected is not null)
            ClaudeApiKeyProtected = claudeApiKeyProtected;

        ClaudeDecisionModel = string.IsNullOrWhiteSpace(claudeDecisionModel) ? ClaudeDecisionModel : claudeDecisionModel.Trim();
        ClaudeSummaryModel  = string.IsNullOrWhiteSpace(claudeSummaryModel)  ? ClaudeSummaryModel  : claudeSummaryModel.Trim();

        if (metaAdsAccountId is not null)
            MetaAdsAccountId = string.IsNullOrWhiteSpace(metaAdsAccountId) ? null : metaAdsAccountId.Trim();

        if (metaAdsAccessTokenProtected is not null)
            MetaAdsAccessTokenProtected = metaAdsAccessTokenProtected;

        DryRun            = dryRun;
        MaxActionsPerDay  = maxActionsPerDay > 0 ? maxActionsPerDay : MaxActionsPerDay;
        DailySpendCapUsd  = dailySpendCapUsd > 0 ? dailySpendCapUsd : DailySpendCapUsd;
        UpdatedAt         = DateTime.UtcNow;
    }
}

// ── Tenant-scoped knowledge base chunk ───────────────────────────────────────
// Owner-editable text blocks that are assembled into Claude's decision context.
// Refined by KnowledgeRefinementService in Phase 2.
public sealed class KnowledgeChunk : Entity<Guid>, ITenantEntity
{
    public Guid? TenantId   { get; private set; }
    public string Title     { get; private set; } = default!;
    public string Content   { get; private set; } = default!;
    public string? Source   { get; private set; }
    public bool IsActive    { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>JSON-serialized float[] embedding (768-dim from Gemini text-embedding-004). Null until first embedding run.</summary>
    public string? EmbeddingJson { get; private set; }

    private KnowledgeChunk() { }

    public static KnowledgeChunk Create(Guid tenantId, string title, string content, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(title))   throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Content is required.", nameof(content));

        return new KnowledgeChunk
        {
            Id        = Guid.NewGuid(),
            TenantId  = tenantId,
            Title     = title.Trim(),
            Content   = content.Trim(),
            Source    = string.IsNullOrWhiteSpace(source) ? null : source.Trim(),
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void Update(string title, string content, string? source)
    {
        if (string.IsNullOrWhiteSpace(title))   throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Content is required.", nameof(content));

        Title     = title.Trim();
        Content   = content.Trim();
        Source    = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate() => IsActive = false;

    public void SetEmbedding(string embeddingJson) => EmbeddingJson = embeddingJson;
}

// ── Tenant-scoped agent decision log ─────────────────────────────────────────
// Records every action the marketing agent considered or took.
// Status lifecycle: Draft → PendingApproval → Approved|Rejected → Executed|DryRun
public sealed class AgentDecision : Entity<Guid>, ITenantEntity
{
    public Guid? TenantId          { get; private set; }
    public DateTime RunAt          { get; private set; }

    /// <summary>Short summary of performance context provided to Claude.</summary>
    public string ContextSummary   { get; private set; } = default!;

    /// <summary>Action type Claude decided on: scale_up | scale_down | pause | activate | no_action</summary>
    public string ActionType       { get; private set; } = default!;

    /// <summary>JSON payload of the action (targets, values, etc.).</summary>
    public string? ActionPayload   { get; private set; }

    public AgentDecisionStatus Status { get; private set; }

    /// <summary>Human-readable rationale from Claude.</summary>
    public string? Reason          { get; private set; }

    /// <summary>Claude's confidence score 0.0 – 1.0.</summary>
    public double Confidence       { get; private set; }

    /// <summary>True when the action was logged but not sent to Meta (DryRun mode).</summary>
    public bool IsDryRun           { get; private set; }

    public DateTime? ExecutedAt    { get; private set; }
    public Guid? ApprovedByUserId  { get; private set; }
    public DateTime CreatedAt      { get; private set; }

    /// <summary>JSON-serialized float[] embedding for decision memory RAG. Null until embedded.</summary>
    public string? EmbeddingJson   { get; private set; }

    /// <summary>Outcome of this decision: positive | negative | neutral. Set by OutcomeTrackerJob.</summary>
    public string? OutcomeLabel    { get; private set; }

    private AgentDecision() { }

    public static AgentDecision Create(
        Guid tenantId,
        string contextSummary,
        string actionType,
        string? actionPayload,
        string? reason,
        double confidence,
        bool requiresApproval,
        bool isDryRun)
    {
        var status = isDryRun
            ? AgentDecisionStatus.DryRun
            : requiresApproval
                ? AgentDecisionStatus.PendingApproval
                : AgentDecisionStatus.Draft;

        return new AgentDecision
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            RunAt          = DateTime.UtcNow,
            ContextSummary = contextSummary?.Trim() ?? string.Empty,
            ActionType     = (actionType ?? "no_action").Trim(),
            ActionPayload  = actionPayload,
            Status         = status,
            Reason         = reason,
            Confidence     = confidence,
            IsDryRun       = isDryRun,
            CreatedAt      = DateTime.UtcNow,
        };
    }

    public void MarkExecuted()
    {
        Status     = AgentDecisionStatus.Executed;
        ExecutedAt = DateTime.UtcNow;
    }

    public void Approve(Guid userId)
    {
        Status            = AgentDecisionStatus.Approved;
        ApprovedByUserId  = userId;
    }

    public void Reject() => Status = AgentDecisionStatus.Rejected;

    public void SetEmbedding(string embeddingJson) => EmbeddingJson = embeddingJson;

    public void SetOutcome(string outcomeLabel) => OutcomeLabel = outcomeLabel;
}

public enum AgentDecisionStatus
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    Executed,
    DryRun
}
