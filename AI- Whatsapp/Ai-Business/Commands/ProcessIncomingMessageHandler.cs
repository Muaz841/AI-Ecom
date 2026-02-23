using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;

namespace EcomAI.Platform.Business.Commands;

public class ProcessIncomingMessageHandler : IRequestHandler<ProcessIncomingMessageCommand, ProcessIncomingMessageResult>
{
    private readonly IMessageRepository _messageRepository;
    private readonly IProductRepository _productRepository;
    private readonly IAIService _aiService;
    private readonly IMetaMessagingService _metaService;
    private readonly ILogger<ProcessIncomingMessageHandler> _logger;

    public ProcessIncomingMessageHandler(
        IMessageRepository messageRepository,
        IProductRepository productRepository,
        IAIService aiService,
        IMetaMessagingService metaService,
        ILogger<ProcessIncomingMessageHandler> logger)
    {
        _messageRepository = messageRepository;
        _productRepository = productRepository;
        _aiService = aiService;
        _metaService = metaService;
        _logger = logger;
    }

    public async Task<ProcessIncomingMessageResult> Handle(ProcessIncomingMessageCommand request, CancellationToken cancellationToken)
    {
        var message = Message.CreateIncoming(
            request.ClientId,
            request.Platform,
            request.From,
            request.To,
            request.Content,
            request.RawPayloadJson);

        await _messageRepository.AddAsync(message);

        var availableProducts = await _productRepository.GetAvailableProductsAsync(
            request.ClientId,
            maxItems: 15);

        var inventoryContext = BuildInventoryContext(availableProducts);

        var intentResult = await _aiService.DetectIntentAsync(
            new IntentRequest(request.Content, inventoryContext, request.Platform));

        message.MarkAsHandledByAI(intentResult.DetectedIntent);

        var replyResult = await _aiService.GenerateReplyAsync(
            new ReplyRequest(
                request.Content,
                intentResult.DetectedIntent,
                inventoryContext,
                message.Id.ToString()));

        if (!replyResult.Success)
        {
            _logger.LogWarning("AI reply generation failed for message {MessageId}: {Error}", message.Id, replyResult.Error);
            return new ProcessIncomingMessageResult(false, null, intentResult.DetectedIntent, message.Id, replyResult.Error);
        }

        var sendResult = await _metaService.SendTextMessageAsync(
            request.ClientId,
            request.Platform,
            request.From,
            replyResult.GeneratedReply);

        if (!sendResult.Success)
        {
            _logger.LogError("Failed to send reply to {From} on {Platform}: {Error}", request.From, request.Platform, sendResult.Error);
            return new ProcessIncomingMessageResult(false, null, intentResult.DetectedIntent, message.Id, sendResult.Error);
        }

        message.MarkAsSent(DateTime.UtcNow);

        await _messageRepository.UpdateAsync(message);

        if (intentResult.DetectedIntent == "order_start")
        {
            // TODO: Dispatch CreateOrderCommand
        }

        return new ProcessIncomingMessageResult(
            true,
            replyResult.GeneratedReply,
            intentResult.DetectedIntent,
            message.Id);
    }

    private static string BuildInventoryContext(IReadOnlyList<Product> products)
    {
        if (!products.Any())
        {
            return "No products currently available.";
        }

        return string.Join("\n", products.Select(p =>
            $"{p.Name} - {p.BasePrice:C} ({p.Currency}) | Stock: {p.TotalStock} | Variants: {p.Variants.Count}"));
    }
}
