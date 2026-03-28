using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using EcomAI.Platform.Business.Common;
using EcomAI.Platform.Business.Constants;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;

namespace EcomAI.Platform.Business.Commands;

public class ProcessIncomingMessageHandler : IRequestHandler<ProcessIncomingMessageCommand, ProcessIncomingMessageResult>
{
    private static readonly HashSet<string> AllowedIntents =
        new(AiIntentCodes.All, StringComparer.OrdinalIgnoreCase);

    private const int MaxAiCallsPerMessage = 2;
    private const string FallbackIntent = AiIntentCodes.Unhandled;
    private const string FallbackReply = "Thanks for your message. Our team will get back to you shortly.";
    private const string ModelNotConfiguredReply = "⚠️ Our AI assistant is not fully set up yet. Please contact support.";

    private readonly IConversationThreadRepository _conversationThreadRepository;
    private readonly IAIService _aiService;
    private readonly IAgentOrchestrator _agentOrchestrator;
    private readonly ITenantPromptBuilder _promptBuilder;
    private readonly IMetaMessagingService _metaService;
    private readonly IApplicationLogger _logger;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public ProcessIncomingMessageHandler(
        IConversationThreadRepository conversationThreadRepository,
        IAIService aiService,
        IAgentOrchestrator agentOrchestrator,
        ITenantPromptBuilder promptBuilder,
        IMetaMessagingService metaService,
        IApplicationLogger logger,
        IRealtimeNotifier realtimeNotifier)
    {
        _conversationThreadRepository = conversationThreadRepository;
        _aiService = aiService;
        _agentOrchestrator = agentOrchestrator;
        _promptBuilder = promptBuilder;
        _metaService = metaService;
        _logger = logger;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<ProcessIncomingMessageResult> Handle(ProcessIncomingMessageCommand request, CancellationToken cancellationToken)
    {
        var aiCallsUsed = 0;
        var provider = await _aiService.GetCurrentProviderInfoAsync(cancellationToken);
        _logger.Info(
            "Processing incoming message with AI provider {Provider}/{Model} for tenant {TenantId} on {Platform}",
            provider.ProviderName,
            provider.ModelVersion,
            request.TenantId,
            request.Platform);

        var thread = await _conversationThreadRepository.GetOrCreateAsync(
            request.TenantId,
            request.Platform,
            request.From,
            request.To,
            cancellationToken: cancellationToken);

        var message = thread.AddIncomingMessage(
            from: request.From,
            to: request.To,
            content: request.Content,
            rawPayloadJson: request.RawPayloadJson,
            externalMessageId: request.ExternalMessageId,
            messageType: request.MessageType);

        if (request.MessageType == MessageType.Comment)
        {
            await _realtimeNotifier.PublishAsync(
                request.TenantId,
                RealtimeEventNames.CommentReceived,
                new
                {
                    message.Id,
                    request.Platform,
                    request.From,
                    request.To,
                    request.Content,
                    ThreadId = thread.Id,
                    ReceivedAtUtc = message.ReceivedAt
                },
                cancellationToken);
        }
        else
        {
            await _realtimeNotifier.PublishAsync(
                request.TenantId,
                RealtimeEventNames.MessageReceived,
                new
                {
                    message.Id,
                    request.Platform,
                    request.From,
                    request.To,
                    request.Content,
                    ThreadId = thread.Id,
                    ReceivedAtUtc = message.ReceivedAt
                },
                cancellationToken);
        }

        if (!request.AllowAutoReply)
        {
            await _conversationThreadRepository.SaveThreadWithMessagesAsync(
                thread,
                new[] { message },
                cancellationToken);

            _logger.Info(
                "Inbound message stored without auto-reply (Platform={Platform}, MessageType={MessageType}, MessageId={MessageId})",
                request.Platform,
                request.MessageType,
                message.Id);

            return new ProcessIncomingMessageResult(true, null, null, message.Id);
        }

        
        var systemPrompt = await _promptBuilder.GetSystemPromptAsync(request.TenantId, cancellationToken);

        var detectedIntent = FallbackIntent;
        var intentDetectedSuccessfully = false;
        var generatedReply = FallbackReply;
        IReadOnlyList<string> toolCallsMade = Array.Empty<string>();
        var inputTokens  = 0;
        var outputTokens = 0;
        try
        {
            EnsureAiBudget(aiCallsUsed);
            aiCallsUsed++;

            var intentResult = await _aiService.DetectIntentAsync(
                new IntentRequest(request.Content, request.Platform, SystemPrompt: systemPrompt),
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
        catch (AiModelNotConfiguredException ex)
        {
            _logger.Warning(ex, "AI model not configured for message {MessageId}. Sending model-not-configured reply.", message.Id);
            generatedReply = ModelNotConfiguredReply;
            goto SendReply;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Intent detection failed for message {MessageId}. Falling back to '{FallbackIntent}'", message.Id, FallbackIntent);
        }

        message.MarkAsHandledByAI(detectedIntent);

        if (intentDetectedSuccessfully)
        {
            try
            {
               
                var agentResult = await _agentOrchestrator.RunAsync(
                    new AgentRequest(
                        request.TenantId,
                        request.Content,
                        detectedIntent,
                        message.Id.ToString(),
                        systemPrompt),
                    cancellationToken);

                if (agentResult.Success && !string.IsNullOrWhiteSpace(agentResult.FinalReply))
                {
                    generatedReply = agentResult.FinalReply.Trim();
                    toolCallsMade  = agentResult.ToolCallsMade.Select(t => t.ToolName).ToList();
                    inputTokens    = agentResult.TotalInputTokens;
                    outputTokens   = agentResult.TotalOutputTokens;
                    if (agentResult.ToolCallsMade.Count > 0)
                    {
                        _logger.Info(
                            "Agent used {ToolCount} tool(s) for message {MessageId}: {Tools}",
                            agentResult.ToolCallsMade.Count,
                            message.Id,
                            string.Join(", ", agentResult.ToolCallsMade.Select(t => t.ToolName)));
                    }
                }
                else
                {
                    _logger.Warning(
                        "Agent returned invalid/failed result for message {MessageId}. Using deterministic fallback. Error: {Error}",
                        message.Id,
                        agentResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Agent orchestration failed for message {MessageId}. Using deterministic fallback.", message.Id);
            }
        }
        else
        {
            _logger.Warning("Skipping AI reply generation for message {MessageId} due to failed intent detection.", message.Id);
        }

        SendReply:
        var sendResult = await _metaService.SendTextMessageAsync(
            request.TenantId,
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

        await _realtimeNotifier.PublishAsync(
            request.TenantId,
            RealtimeEventNames.AiReplySent,
            new
            {
                OutgoingMessageId = outgoing.Id,
                request.Platform,
                request.From,
                Reply    = generatedReply,
                Intent   = detectedIntent,
                ThreadId = thread.Id,
                SentAtUtc = outgoing.SentAt
            },
            cancellationToken);

        if (detectedIntent == AiIntentCodes.OrderStart)
        {
            // TODO: Dispatch CreateOrderCommand
        }

        return new ProcessIncomingMessageResult(
            true,
            generatedReply,
            detectedIntent,
            message.Id,
            ToolCallsMade: toolCallsMade,
            InputTokens:   inputTokens,
            OutputTokens:  outputTokens);
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
