using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Commands;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using Xunit;

namespace UnitTests.Commands;

public class ProcessIncomingMessageHandlerTests
{
    private static ProcessIncomingMessageHandler BuildHandler(
        FakeAiService? ai = null,
        FakeMetaMessagingService? meta = null) =>
        new(
            new FakeConversationThreadRepository(),
            new FakeProductRepository(),
            ai ?? new FakeAiService(),
            new FakeAgentOrchestrator(),
            new FakeTenantPromptBuilder(),
            meta ?? new FakeMetaMessagingService(),
            new FakeApplicationLogger(),
            new FakeRealtimeNotifier());

    [Fact]
    public async Task Handle_Uses_Fallback_Reply_When_AiReply_Generation_Fails()
    {
        var handler = BuildHandler(ai: new FakeAiService(shouldReplyFail: true));
        var result = await handler.Handle(CreateRequest(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Thanks for your message. Our team will get back to you shortly.", result.ReplySent);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Meta_Send_Fails()
    {
        var handler = BuildHandler(meta: new FakeMetaMessagingService(shouldFail: true));
        var result = await handler.Handle(CreateRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("meta-send-failed", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_Returns_Success_When_Ai_And_Meta_Succeed()
    {
        var handler = BuildHandler();
        var result = await handler.Handle(CreateRequest(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("inquiry", result.DetectedIntent);
        Assert.Equal("Auto reply", result.ReplySent);
        Assert.NotNull(result.CreatedMessageId);
    }

    [Fact]
    public async Task Handle_Uses_Fallback_Intent_And_Reply_When_AiDetectIntent_Throws()
    {
        var handler = BuildHandler(ai: new FakeAiService(shouldDetectThrow: true));
        var result = await handler.Handle(CreateRequest(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("unhandled", result.DetectedIntent);
        Assert.Equal("Thanks for your message. Our team will get back to you shortly.", result.ReplySent);
    }

    private static ProcessIncomingMessageCommand CreateRequest()
    {
        return new ProcessIncomingMessageCommand(
            Guid.NewGuid(),
            "whatsapp",
            "+923001234567",
            "+920001112223",
            "Do you have shirts?");
    }

    private sealed class FakeConversationThreadRepository : IConversationThreadRepository
    {
        public Task<ConversationThread> GetOrCreateAsync(Guid TenantId, string platform, string customerIdentifier, string businessIdentifier, string? customerDisplayName = null, CancellationToken cancellationToken = default)
            => Task.FromResult(ConversationThread.Create(TenantId, platform, customerIdentifier, businessIdentifier, customerDisplayName));

        public Task<ConversationThread?> GetByIdAsync(Guid TenantId, Guid conversationThreadId, CancellationToken cancellationToken = default)
            => Task.FromResult<ConversationThread?>(null);

        public Task<IReadOnlyList<ConversationThread>> ListRecentAsync(Guid TenantId, int pageIndex = 0, int pageSize = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ConversationThread>>(Array.Empty<ConversationThread>());

        public Task UpdateAsync(ConversationThread thread, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveThreadWithMessagesAsync(ConversationThread thread, IReadOnlyCollection<Message> messages, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public Task<IReadOnlyList<ProductInventoryItem>> GetAvailableInventoryAsync(
            Guid TenantId,
            int? maxItems = 20,
            string? searchTerm = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductInventoryItem>>(Array.Empty<ProductInventoryItem>());

        public Task<Product?> GetByIdAsync(Guid TenantId, Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public Task<IReadOnlyList<Product>> GetAvailableProductsAsync(
            Guid TenantId,
            int? maxItems = 20,
            string? searchTerm = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>(Array.Empty<Product>());

        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(Guid TenantId, int threshold = 5, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>(Array.Empty<Product>());

        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid productId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(Guid TenantId, string skuOrName, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeAiService : IAIService
    {
        private readonly bool _shouldReplyFail;
        private readonly bool _shouldDetectThrow;
        private readonly string _intent;

        public FakeAiService(
            bool shouldReplyFail = false,
            bool shouldDetectThrow = false,
            string intent = "inquiry")
        {
            _shouldReplyFail = shouldReplyFail;
            _shouldDetectThrow = shouldDetectThrow;
            _intent = intent;
        }

        public Task<IntentDetectionResult> DetectIntentAsync(
            IntentRequest request,
            bool simulateOnly = false,
            CancellationToken cancellationToken = default)
        {
            if (_shouldDetectThrow)
            {
                throw new InvalidOperationException("ai-detect-failed");
            }

            return Task.FromResult(new IntentDetectionResult(
                _intent,
                0.97,
                "prompt",
                $"{{\"intent\":\"{_intent}\"}}",
                10,
                4,
                simulateOnly));
        }

        public Task<ReplyGenerationResult> GenerateReplyAsync(
            ReplyRequest request,
            bool simulateOnly = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shouldReplyFail
                ? new ReplyGenerationResult(false, null, "prompt", "{}", 8, 0, false, simulateOnly, "ai-reply-failed")
                : new ReplyGenerationResult(true, "Auto reply", "prompt", "{\"reply\":\"Auto reply\"}", 8, 5, false, simulateOnly));
        }

        public Task<CaptionGenerationResult> GenerateCaptionAsync(
            CaptionRequest request,
            bool simulateOnly = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CaptionGenerationResult(
                true,
                "caption",
                new List<string>(),
                "prompt",
                "{}",
                7,
                4,
                simulateOnly));

        public Task<AdCopiesResult> GenerateAdCopiesAsync(
            AdRequest request,
            bool simulateOnly = false,
            bool estimateTokensOnly = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AdCopiesResult(
                true,
                estimateTokensOnly ? Array.Empty<string>() : new List<string> { "ad1" },
                "prompt",
                "{}",
                6,
                5,
                11,
                simulateOnly));

        public (string ProviderName, string ModelVersion) GetCurrentProviderInfo()
            => ("FakeAI", "test-v1");
    }

    private sealed class FakeMetaMessagingService : IMetaMessagingService
    {
        private readonly bool _shouldFail;

        public FakeMetaMessagingService(bool shouldFail = false)
        {
            _shouldFail = shouldFail;
        }

        public Task<MessagingSendResult> SendTextMessageAsync(
            Guid TenantId,
            string platform,
            string recipient,
            string messageText,
            string? messagingType = "RESPONSE",
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shouldFail
                ? new MessagingSendResult(false, null, "meta-send-failed", 500)
                : new MessagingSendResult(true, Guid.NewGuid().ToString(), null, 200));
        }

        public Task<MessagingSendResult> SendTemplateMessageAsync(
            Guid TenantId,
            string platform,
            string recipient,
            string templateName,
            object? templateParameters = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new MessagingSendResult(true, Guid.NewGuid().ToString(), null, 200));

        public Task<MessagingSendResult> SendImageMessageAsync(
            Guid TenantId,
            string platform,
            string recipient,
            string imageUrl,
            string? caption = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new MessagingSendResult(true, Guid.NewGuid().ToString(), null, 200));

        public Task MarkMessageAsReadAsync(
            Guid TenantId,
            string platform,
            string recipient,
            string messageId,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<MessagingSendResult> SendQuickRepliesAsync(
            Guid TenantId,
            string platform,
            string recipient,
            string text,
            IEnumerable<QuickReplyOption> quickReplies,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new MessagingSendResult(true, Guid.NewGuid().ToString(), null, 200));
    }

    private sealed class FakeAgentOrchestrator : IAgentOrchestrator
    {
        public Task<AgentResult> RunAsync(AgentRequest request, CancellationToken ct = default)
            => Task.FromResult(new AgentResult(
                Success: true,
                FinalReply: "Auto reply",
                ToolCallsMade: Array.Empty<ToolCall>(),
                TotalInputTokens: 8,
                TotalOutputTokens: 5));
    }

    private sealed class FakeTenantPromptBuilder : ITenantPromptBuilder
    {
        public Task<string?> GetSystemPromptAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class FakeRealtimeNotifier : IRealtimeNotifier
    {
        public Task PublishAsync(Guid tenantId, string eventType, object payload, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeApplicationLogger : IApplicationLogger
    {
        public void Info(string messageTemplate, params object?[] args) { }

        public void Warning(string messageTemplate, params object?[] args) { }

        public void Warning(Exception exception, string messageTemplate, params object?[] args) { }

        public void Error(string messageTemplate, params object?[] args) { }

        public void Error(Exception exception, string messageTemplate, params object?[] args) { }

        public Task LogIncomingAsync(
            Guid? tenantId,
            string channel,
            string operation,
            string? endpoint,
            string? requestPayload,
            bool isSuccess,
            int? statusCode = null,
            string? responsePayload = null,
            string? errorMessage = null,
            string? correlationId = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogOutgoingAsync(
            Guid? tenantId,
            string channel,
            string operation,
            string? endpoint,
            string? requestPayload,
            bool isSuccess,
            int? statusCode = null,
            string? responsePayload = null,
            string? errorMessage = null,
            string? correlationId = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

