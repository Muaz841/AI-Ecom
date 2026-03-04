using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;

namespace EcomAI.Platform.Business.Commands;

public class ProcessIncomingMessageHandler : IRequestHandler<ProcessIncomingMessageCommand, ProcessIncomingMessageResult>
{
    private static readonly HashSet<string> AllowedIntents = new(StringComparer.OrdinalIgnoreCase)
    {
        "greeting",
        "order_start",
        "inquiry",
        "complaint",
        "unhandled"
    };

    private const int MaxAiCallsPerMessage = 2;
    private const string FallbackIntent = "unhandled";
    private const string FallbackReply = "Thanks for your message. Our team will get back to you shortly.";

    private readonly IConversationThreadRepository _conversationThreadRepository;
    private readonly IProductRepository _productRepository;
    private readonly IAIService _aiService;
    private readonly IMetaMessagingService _metaService;
    private readonly IApplicationLogger _logger;

    public ProcessIncomingMessageHandler(
        IConversationThreadRepository conversationThreadRepository,
        IProductRepository productRepository,
        IAIService aiService,
        IMetaMessagingService metaService,
        IApplicationLogger logger)
    {
        _conversationThreadRepository = conversationThreadRepository;
        _productRepository = productRepository;
        _aiService = aiService;
        _metaService = metaService;
        _logger = logger;
    }

    public async Task<ProcessIncomingMessageResult> Handle(ProcessIncomingMessageCommand request, CancellationToken cancellationToken)
    {
        var aiCallsUsed = 0;
        var provider = _aiService.GetCurrentProviderInfo();
        _logger.Info(
            "Processing incoming message with AI provider {Provider}/{Model} for client {ClientId} on {Platform}",
            provider.ProviderName,
            provider.ModelVersion,
            request.ClientId,
            request.Platform);

        var thread = await _conversationThreadRepository.GetOrCreateAsync(
            request.ClientId,
            request.Platform,
            request.From,
            request.To,
            cancellationToken: cancellationToken);

        var message = thread.AddIncomingMessage(
            from: request.From,
            to: request.To,
            content: request.Content,
            rawPayloadJson: request.RawPayloadJson,
            externalMessageId: request.ExternalMessageId);

        var availableProducts = await _productRepository.GetAvailableInventoryAsync(
            request.ClientId,
            maxItems: 15,
            cancellationToken: cancellationToken);

        var inventoryContext = BuildInventoryContext(availableProducts);

        var detectedIntent = FallbackIntent;
        var intentDetectedSuccessfully = false;
        try
        {
            EnsureAiBudget(aiCallsUsed);
            aiCallsUsed++;

            var intentResult = await _aiService.DetectIntentAsync(
                new IntentRequest(request.Content, inventoryContext, request.Platform),
                cancellationToken: cancellationToken);

            if (IsValidIntent(intentResult.DetectedIntent))
            {
                detectedIntent = intentResult.DetectedIntent;
                intentDetectedSuccessfully = true;
            }
            else
            {
                _logger.Warning(
                    "AI returned invalid intent for message {MessageId}. Falling back to '{FallbackIntent}'. Intent was: {Intent}",
                    message.Id,
                    FallbackIntent,
                    intentResult.DetectedIntent);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Intent detection failed for message {MessageId}. Falling back to '{FallbackIntent}'", message.Id, FallbackIntent);
        }

        message.MarkAsHandledByAI(detectedIntent);

        var generatedReply = FallbackReply;
        if (intentDetectedSuccessfully)
        {
            try
            {
                EnsureAiBudget(aiCallsUsed);
                aiCallsUsed++;

                var replyResult = await _aiService.GenerateReplyAsync(
                    new ReplyRequest(
                        request.Content,
                        detectedIntent,
                        inventoryContext,
                        message.Id.ToString()),
                    cancellationToken: cancellationToken);

                if (replyResult.Success && !string.IsNullOrWhiteSpace(replyResult.GeneratedReply))
                {
                    generatedReply = replyResult.GeneratedReply.Trim();
                }
                else
                {
                    _logger.Warning(
                        "AI reply generation returned invalid/failed result for message {MessageId}. Using deterministic fallback reply. Error: {Error}",
                        message.Id,
                        replyResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "AI reply generation failed for message {MessageId}. Using deterministic fallback reply.", message.Id);
            }
        }
        else
        {
            _logger.Warning("Skipping AI reply generation for message {MessageId} due to failed intent detection.", message.Id);
        }

        var sendResult = await _metaService.SendTextMessageAsync(
            request.ClientId,
            request.Platform,
            request.From,
            generatedReply,
            cancellationToken: cancellationToken);

        if (!sendResult.Success)
        {
            _logger.Error("Failed to send reply to {From} on {Platform}: {Error}", request.From, request.Platform, sendResult.ErrorMessage);
            return new ProcessIncomingMessageResult(false, null, detectedIntent, message.Id, sendResult.ErrorMessage);
        }

        message.MarkAsSent(DateTime.UtcNow);
        var outgoing = thread.AddOutgoingMessage(
            from: request.To,
            to: request.From,
            content: generatedReply);
        outgoing.MarkAsSent(DateTime.UtcNow);

        await _conversationThreadRepository.SaveThreadWithMessagesAsync(
            thread,
            new[] { message, outgoing },
            cancellationToken);

        if (detectedIntent == "order_start")
        {
            // TODO: Dispatch CreateOrderCommand
        }

        return new ProcessIncomingMessageResult(
            true,
            generatedReply,
            detectedIntent,
            message.Id);
    }

    private static string BuildInventoryContext(IReadOnlyList<ProductInventoryItem> products)
    {
        if (!products.Any())
        {
            return "No products currently available.";
        }

        return string.Join("\n", products.Take(15).Select(p =>
            $"{p.Name} - {p.BasePrice:C} ({p.Currency}) | Stock: {p.TotalStock}"));
    }

    private static bool IsValidIntent(string? intent)
        => !string.IsNullOrWhiteSpace(intent) && AllowedIntents.Contains(intent.Trim());

    private static void EnsureAiBudget(int callsUsed)
    {
        if (callsUsed >= MaxAiCallsPerMessage)
        {
            throw new InvalidOperationException($"AI call budget exceeded. Max allowed per message: {MaxAiCallsPerMessage}");
        }
    }
}
