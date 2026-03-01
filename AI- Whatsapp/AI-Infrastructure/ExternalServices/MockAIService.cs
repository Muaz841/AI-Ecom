using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.Logging;

namespace EcomAI.Platform.Infrastructure.ExternalServices;

public class MockAIService : IAIService
{
    private readonly ILogger<MockAIService> _logger;
    private readonly Random _random = new(42);

    public MockAIService(ILogger<MockAIService> logger)
    {
        _logger = logger;
    }

    public Task<IntentDetectionResult> DetectIntentAsync(
        IntentRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var content = (request.MessageContent ?? string.Empty).ToLowerInvariant();
        var intent = content.Contains("buy") || content.Contains("order")
            ? "order_start"
            : content.Contains("price") || content.Contains("cost")
                ? "inquiry"
                : content.Contains("refund") || content.Contains("complaint")
                    ? "complaint"
                    : "greeting";

        var prompt = $"[MOCK] Intent for: {request.MessageContent}";
        var response = $"[MOCK] intent={intent}";

        _logger.LogDebug("[MockAI] DetectIntent prompt={Prompt} response={Response}", prompt, response);

        return Task.FromResult(new IntentDetectionResult(
            intent,
            0.90 + _random.NextDouble() * 0.09,
            prompt,
            response,
            15 + _random.Next(8),
            5 + _random.Next(4),
            true));
    }

    public Task<ReplyGenerationResult> GenerateReplyAsync(
        ReplyRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var firstInventoryLine = request.InventoryContext?.Split('\n').FirstOrDefault() ?? "No products currently available.";
        var reply = $"[MOCK] Thanks for your message. {firstInventoryLine}";
        var prompt = $"[MOCK] Reply for intent={request.DetectedIntent}";

        _logger.LogDebug("[MockAI] GenerateReply prompt={Prompt} reply={Reply}", prompt, reply);

        return Task.FromResult(new ReplyGenerationResult(
            true,
            reply,
            prompt,
            reply,
            26 + _random.Next(12),
            20 + _random.Next(15),
            false,
            true));
    }

    public Task<CaptionGenerationResult> GenerateCaptionAsync(
        CaptionRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var caption = $"[MOCK] {request.ProductName} only {request.Price} {request.Currency}. Shop now! #Fashion #Sale";
        var prompt = $"[MOCK] Caption for {request.ProductName}";

        return Task.FromResult(new CaptionGenerationResult(
            true,
            caption,
            new[] { "#Fashion", "#Sale", "#Trendy" },
            prompt,
            caption,
            30,
            25,
            true));
    }

    public Task<AdCopiesResult> GenerateAdCopiesAsync(
        AdRequest request,
        bool simulateOnly = false,
        bool estimateTokensOnly = false,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"[MOCK] Ads for {request.ProductName}";
        var estimatedTokens = 120;
        if (estimateTokensOnly)
        {
            return Task.FromResult(new AdCopiesResult(
                true,
                Array.Empty<string>(),
                prompt,
                "[MOCK] estimate-only",
                0,
                0,
                estimatedTokens,
                true));
        }

        var variations = new List<string>
        {
            $"[MOCK] Get {request.ProductName} now for {request.Price}!",
            $"[MOCK] Limited stock: {request.ProductName} at {request.Price}!",
            $"[MOCK] Don't miss {request.ProductName}, order today!"
        };

        return Task.FromResult(new AdCopiesResult(
            true,
            variations,
            prompt,
            "[MOCK] response",
            40,
            80,
            estimatedTokens,
            true));
    }

    public (string ProviderName, string ModelVersion) GetCurrentProviderInfo()
        => ("MOCK-AI", "debug-mode");
}
