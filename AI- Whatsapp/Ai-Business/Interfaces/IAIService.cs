using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface IAIService
{
    Task<IntentDetectionResult> DetectIntentAsync(
        IntentRequest request,
        CancellationToken cancellationToken = default);

    Task<ReplyGenerationResult> GenerateReplyAsync(
        ReplyRequest request,
        CancellationToken cancellationToken = default);

    Task<CaptionGenerationResult> GenerateCaptionAsync(
        CaptionRequest request,
        CancellationToken cancellationToken = default);

    Task<AdCopiesResult> GenerateAdCopiesAsync(
        AdRequest request,
        bool estimateTokensOnly = false,
        CancellationToken cancellationToken = default);

    Task<(string ProviderName, string ModelVersion)> GetCurrentProviderInfoAsync(CancellationToken cancellationToken = default);

    Task<AgentTurnResult> GenerateAgentTurnAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken = default);

    Task<PoseExtractionResult> ExtractPoseAsync(
        PoseExtractionRequest request,
        CancellationToken cancellationToken = default);

    Task<ImageGenerationResult> GenerateModelImageAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default);
}

public record IntentRequest(
    string MessageContent,
    string Platform,
    string? CustomerLanguageHint = null,
    string? SystemPrompt = null);

public record IntentDetectionResult(
    string DetectedIntent,
    double ConfidenceScore,
    string RawPromptUsed,
    string RawResponseFromModel,
    int InputTokensUsed,
    int OutputTokensUsed,
    string? ErrorMessage = null);

public record ReplyRequest(
    string MessageContent,
    string DetectedIntent,
    string InventoryContext,
    string MessageIdForAudit,
    string? SystemPrompt = null);

public record ReplyGenerationResult(
    bool Success,
    string? GeneratedReply,
    string RawPromptUsed,
    string RawResponseFromModel,
    int InputTokensUsed,
    int OutputTokensUsed,
    bool WasModeratedAsUnsafe,
    string? ErrorMessage = null);

public record CaptionRequest(
    string ProductName,
    string ProductDescription,
    decimal Price,
    string Currency,
    string StylePreferences,
    int MaxLength = 2200);

public record CaptionGenerationResult(
    bool Success,
    string? GeneratedCaption,
    IReadOnlyList<string>? Hashtags,
    string RawPromptUsed,
    string RawResponseFromModel,
    int InputTokensUsed,
    int OutputTokensUsed,
    string? ErrorMessage = null);

public record AdRequest(
    string ProductName,
    string Description,
    decimal Price,
    int VariationCount = 3);

public record AdCopiesResult(
    bool Success,
    IReadOnlyList<string>? Variations,
    string RawPromptUsed,
    string RawResponseFromModel,
    int InputTokensUsed,
    int OutputTokensUsed,
    int EstimatedTokensBeforeCall,
    string? ErrorMessage = null);

// ── Image pipeline request / result records ───────────────────────────────────

/// <summary>
/// Request to extract a textual pose script from a reference image via a vision model.
/// VisionModelOverride selects a different model from the default chat model.
/// </summary>
public sealed record PoseExtractionRequest(
    byte[]  ImageBytes,
    string  MimeType,
    string? Prompt = null,
    string? VisionModelOverride = null);

public sealed record PoseExtractionResult(
    bool    Success,
    string? PoseScript,
    int     InputTokensUsed,
    int     OutputTokensUsed,
    string? ErrorMessage = null);

/// <summary>
/// Request to generate a model-wearing-dress image from a pose script and dress image.
/// ImageModelOverride selects the image generation model (e.g. dall-e-3, gemini-2.0-flash-exp).
/// </summary>
public sealed record ImageGenerationRequest(
    string  PoseScript,
    byte[]  DressImageBytes,
    string  DressImageMimeType,
    string? Prompt = null,
    string? ImageModelOverride = null);

public sealed record ImageGenerationResult(
    bool    Success,
    byte[]? GeneratedImageBytes,
    string? MimeType,
    string? ErrorMessage = null);
