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
    private readonly IMessageRepository _messageRepository;
    private readonly IConversationThreadRepository _conversationThreadRepository;
    private readonly IProductRepository _productRepository;
    private readonly IAIService _aiService;
    private readonly IMetaMessagingService _metaService;
    private readonly IApplicationLogger _logger;

    public ProcessIncomingMessageHandler(
        IMessageRepository messageRepository,
        IConversationThreadRepository conversationThreadRepository,
        IProductRepository productRepository,
        IAIService aiService,
        IMetaMessagingService metaService,
        IApplicationLogger logger)
    {
        _messageRepository = messageRepository;
        _conversationThreadRepository = conversationThreadRepository;
        _productRepository = productRepository;
        _aiService = aiService;
        _metaService = metaService;
        _logger = logger;
    }

    public async Task<ProcessIncomingMessageResult> Handle(ProcessIncomingMessageCommand request, CancellationToken cancellationToken)
    {
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

        var message = Message.CreateIncoming(
            request.ClientId,
            request.Platform,
            request.From,
            request.To,
            request.Content,
            request.RawPayloadJson,
            conversationThreadId: thread.Id,
            externalMessageId: request.ExternalMessageId);

        await _messageRepository.AddAsync(message, cancellationToken);
        thread.TouchWithMessage("incoming", request.Content, DateTime.UtcNow);
        await _conversationThreadRepository.UpdateAsync(thread, cancellationToken);

        var availableProducts = await _productRepository.GetAvailableProductsAsync(
            request.ClientId,
            maxItems: 15,
            cancellationToken: cancellationToken);

        var inventoryContext = BuildInventoryContext(availableProducts);

        var intentResult = await _aiService.DetectIntentAsync(
            new IntentRequest(request.Content, inventoryContext, request.Platform),
            cancellationToken: cancellationToken);

        message.MarkAsHandledByAI(intentResult.DetectedIntent);

        var replyResult = await _aiService.GenerateReplyAsync(
            new ReplyRequest(
                request.Content,
                intentResult.DetectedIntent,
                inventoryContext,
                message.Id.ToString()),
            cancellationToken: cancellationToken);

        if (!replyResult.Success)
        {
            _logger.Warning("AI reply generation failed for message {MessageId}: {Error}", message.Id, replyResult.ErrorMessage);
            return new ProcessIncomingMessageResult(false, null, intentResult.DetectedIntent, message.Id, replyResult.ErrorMessage);
        }

        var generatedReply = replyResult.GeneratedReply ?? string.Empty;

        var sendResult = await _metaService.SendTextMessageAsync(
            request.ClientId,
            request.Platform,
            request.From,
            generatedReply,
            cancellationToken: cancellationToken);

        if (!sendResult.Success)
        {
            _logger.Error("Failed to send reply to {From} on {Platform}: {Error}", request.From, request.Platform, sendResult.ErrorMessage);
            return new ProcessIncomingMessageResult(false, null, intentResult.DetectedIntent, message.Id, sendResult.ErrorMessage);
        }

        message.MarkAsSent(DateTime.UtcNow);

        await _messageRepository.UpdateAsync(message, cancellationToken);

        var outgoing = Message.CreateOutgoing(
            request.ClientId,
            request.Platform,
            request.To,
            request.From,
            generatedReply,
            conversationThreadId: thread.Id);
        outgoing.MarkAsSent(DateTime.UtcNow);
        await _messageRepository.AddAsync(outgoing, cancellationToken);
        thread.TouchWithMessage("outgoing", generatedReply, DateTime.UtcNow);
        await _conversationThreadRepository.UpdateAsync(thread, cancellationToken);

        if (intentResult.DetectedIntent == "order_start")
        {
            // TODO: Dispatch CreateOrderCommand
        }

        return new ProcessIncomingMessageResult(
            true,
            generatedReply,
            intentResult.DetectedIntent,
            message.Id);
    }

    private static string BuildInventoryContext(IReadOnlyList<Product> products)
    {
        if (!products.Any())
        {
            return "No products currently available.";
        }

        return string.Join("\n", products.Take(15).Select(p =>
            $"{p.Name} - {p.BasePrice:C} ({p.Currency}) | Stock: {p.TotalStock} | Variants: {p.Variants.Count}"));
    }
}
