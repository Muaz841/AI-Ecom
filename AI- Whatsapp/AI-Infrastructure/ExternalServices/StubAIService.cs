using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;

namespace EcomAI.Platform.Infrastructure.ExternalServices;

public class StubAIService : IAIService
{
    public Task<IntentDetectionResult> DetectIntentAsync(IntentRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new IntentDetectionResult("inquiry", 0.6));
    }

    public Task<ReplyGenerationResult> GenerateReplyAsync(ReplyRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ReplyGenerationResult(true, "Thanks for your message. Our team will share details shortly."));
    }

    public Task<CaptionGenerationResult> GenerateCaptionAsync(CaptionRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CaptionGenerationResult(
            true,
            $"{request.ProductName} now available at {request.Price} {request.Currency}.",
            new List<string> { "#newarrival", "#shopnow" }));
    }

    public Task<AdCopiesResult> GenerateAdCopiesAsync(AdRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AdCopiesResult(
            true,
            new List<string>
            {
                $"{request.ProductName} at {request.Price}. Limited stock.",
                $"Upgrade your style with {request.ProductName}.",
                $"{request.ProductName} - order now."
            }));
    }
}
