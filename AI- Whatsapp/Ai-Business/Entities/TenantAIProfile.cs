using System;

namespace EcomAI.Platform.Business.Entities;

/// <summary>
/// Tenant-scoped AI persona configuration. One profile per tenant.
/// Controls system prompt, tone, brand rules, and safety constraints
/// injected into every AI call for that tenant.
/// </summary>
public class TenantAIProfile : Entity<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }

    /// <summary>Core system instructions defining the AI assistant's role for this tenant.</summary>
    public string SystemPrompt { get; private set; } = string.Empty;

    /// <summary>Communication tone: professional, friendly, casual, formal, etc.</summary>
    public string? Tone { get; private set; }

    /// <summary>Default response language (e.g. "en", "ur", "ar"). Null = auto-detect.</summary>
    public string? Language { get; private set; }

    /// <summary>Brand voice rules: specific phrases to use or avoid, style guidelines.</summary>
    public string? BrandRules { get; private set; }

    /// <summary>Comma-separated topics the AI must refuse to discuss (safety guardrail).</summary>
    public string? ForbiddenTopics { get; private set; }

    /// <summary>Template for structuring final responses (e.g. "greeting + answer + CTA").</summary>
    public string? DefaultResponseStyle { get; private set; }

    /// <summary>Prompt sent to the vision model when extracting a pose from a reference image.</summary>
    public string? PoseExtractionPrompt { get; private set; }

    /// <summary>
    /// Prompt template for the image generation model.
    /// Use {poseScript} as a placeholder — it is replaced at runtime with the extracted pose description.
    /// </summary>
    public string? ImageGenerationPrompt { get; private set; }

    /// <summary>Per-tenant AI call rate limit per hour (0 = unlimited).</summary>
    public int AiCallsPerHourLimit { get; private set; } = 200;

    /// <summary>Incremented on every update for audit purposes.</summary>
    public int Version { get; private set; } = 1;

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private TenantAIProfile() { }

    public static TenantAIProfile Create(
        Guid tenantId,
        string systemPrompt,
        string? tone = null,
        string? language = null,
        string? brandRules = null,
        string? forbiddenTopics = null,
        string? defaultResponseStyle = null,
        int aiCallsPerHourLimit = 200,
        string? poseExtractionPrompt = null,
        string? imageGenerationPrompt = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(systemPrompt))
            throw new ArgumentException("System prompt is required.", nameof(systemPrompt));
        if (aiCallsPerHourLimit < 0)
            throw new ArgumentOutOfRangeException(nameof(aiCallsPerHourLimit), "Limit must be >= 0.");

        return new TenantAIProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SystemPrompt = systemPrompt.Trim(),
            Tone = tone?.Trim(),
            Language = language?.Trim(),
            BrandRules = brandRules?.Trim(),
            ForbiddenTopics = forbiddenTopics?.Trim(),
            DefaultResponseStyle = defaultResponseStyle?.Trim(),
            PoseExtractionPrompt = poseExtractionPrompt?.Trim(),
            ImageGenerationPrompt = imageGenerationPrompt?.Trim(),
            AiCallsPerHourLimit = aiCallsPerHourLimit,
            Version = 1,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string systemPrompt,
        string? tone,
        string? language,
        string? brandRules,
        string? forbiddenTopics,
        string? defaultResponseStyle,
        int aiCallsPerHourLimit,
        string? poseExtractionPrompt = null,
        string? imageGenerationPrompt = null)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
            throw new ArgumentException("System prompt is required.", nameof(systemPrompt));
        if (aiCallsPerHourLimit < 0)
            throw new ArgumentOutOfRangeException(nameof(aiCallsPerHourLimit), "Limit must be >= 0.");

        SystemPrompt = systemPrompt.Trim();
        Tone = tone?.Trim();
        Language = language?.Trim();
        BrandRules = brandRules?.Trim();
        ForbiddenTopics = forbiddenTopics?.Trim();
        DefaultResponseStyle = defaultResponseStyle?.Trim();
        PoseExtractionPrompt = poseExtractionPrompt?.Trim();
        ImageGenerationPrompt = imageGenerationPrompt?.Trim();
        AiCallsPerHourLimit = aiCallsPerHourLimit;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }
}
