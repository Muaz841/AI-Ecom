using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface IAIService
{
    Task<IntentDetectionResult> DetectIntentAsync(IntentRequest request, CancellationToken cancellationToken = default);

    Task<ReplyGenerationResult> GenerateReplyAsync(ReplyRequest request, CancellationToken cancellationToken = default);

    Task<CaptionGenerationResult> GenerateCaptionAsync(CaptionRequest request, CancellationToken cancellationToken = default);

    Task<AdCopiesResult> GenerateAdCopiesAsync(AdRequest request, CancellationToken cancellationToken = default);
}

public record IntentRequest(
    string MessageContent,
    string InventoryContext,
    string Platform);

public record IntentDetectionResult(
    string DetectedIntent,
    double ConfidenceScore,
    bool Success = true,
    string? Error = null);

public record ReplyRequest(
    string MessageContent,
    string DetectedIntent,
    string InventoryContext,
    string MessageIdForAudit);

public record ReplyGenerationResult(
    bool Success,
    string? GeneratedReply,
    string? Error = null);

public record CaptionRequest(
    string ProductName,
    string ProductDescription,
    decimal Price,
    string Currency,
    string StylePreferences);

public record CaptionGenerationResult(
    bool Success,
    string? GeneratedCaption,
    List<string>? Hashtags,
    string? Error = null);

public record AdRequest(
    string ProductName,
    string Description,
    decimal Price,
    int VariationCount = 3);

public record AdCopiesResult(
    bool Success,
    List<string>? Variations,
    string? Error = null);
