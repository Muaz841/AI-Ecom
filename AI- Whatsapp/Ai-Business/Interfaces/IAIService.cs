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
