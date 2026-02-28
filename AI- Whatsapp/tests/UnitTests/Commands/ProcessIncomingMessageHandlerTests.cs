using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Commands;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.Commands;

public class ProcessIncomingMessageHandlerTests
{
    [Fact]
    public async Task Handle_Returns_Failure_When_AiReply_Generation_Fails()
    {
        var handler = new ProcessIncomingMessageHandler(
            new FakeMessageRepository(),
            new FakeConversationThreadRepository(),
            new FakeProductRepository(),
            new FakeAiService(shouldReplyFail: true),
            new FakeMetaMessagingService(),
            NullLogger<ProcessIncomingMessageHandler>.Instance);

        var result = await handler.Handle(CreateRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Meta_Send_Fails()
    {
        var handler = new ProcessIncomingMessageHandler(
            new FakeMessageRepository(),
            new FakeConversationThreadRepository(),
            new FakeProductRepository(),
            new FakeAiService(),
            new FakeMetaMessagingService(shouldFail: true),
            NullLogger<ProcessIncomingMessageHandler>.Instance);

        var result = await handler.Handle(CreateRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("meta-send-failed", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_Returns_Success_When_Ai_And_Meta_Succeed()
    {
        var handler = new ProcessIncomingMessageHandler(
            new FakeMessageRepository(),
            new FakeConversationThreadRepository(),
            new FakeProductRepository(),
            new FakeAiService(),
            new FakeMetaMessagingService(),
            NullLogger<ProcessIncomingMessageHandler>.Instance);

        var result = await handler.Handle(CreateRequest(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("inquiry", result.DetectedIntent);
        Assert.Equal("Auto reply", result.ReplySent);
        Assert.NotNull(result.CreatedMessageId);
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

    private sealed class FakeMessageRepository : IMessageRepository
    {
        public Task AddAsync(Message message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Message message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Message?> GetByIdAsync(Guid clientId, Guid messageId, CancellationToken cancellationToken = default)
            => Task.FromResult<Message?>(null);

        public Task<IReadOnlyList<Message>> GetRecentUnprocessedAsync(
            Guid clientId,
            int maxCount = 50,
            TimeSpan? withinLast = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());

        public Task<bool> ExistsAsync(Guid clientId, string externalMessageId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<Message>> GetByConversationThreadAsync(
            Guid clientId,
            Guid conversationThreadId,
            int pageIndex = 0,
            int pageSize = 200,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());
    }

    private sealed class FakeConversationThreadRepository : IConversationThreadRepository
    {
        public Task<ConversationThread> GetOrCreateAsync(Guid clientId, string platform, string customerIdentifier, string businessIdentifier, string? customerDisplayName = null, CancellationToken cancellationToken = default)
            => Task.FromResult(ConversationThread.Create(clientId, platform, customerIdentifier, businessIdentifier, customerDisplayName));

        public Task<ConversationThread?> GetByIdAsync(Guid clientId, Guid conversationThreadId, CancellationToken cancellationToken = default)
            => Task.FromResult<ConversationThread?>(null);

        public Task<IReadOnlyList<ConversationThread>> ListRecentAsync(Guid clientId, int pageIndex = 0, int pageSize = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ConversationThread>>(Array.Empty<ConversationThread>());

        public Task UpdateAsync(ConversationThread thread, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public Task<Product?> GetByIdAsync(Guid clientId, Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public Task<IReadOnlyList<Product>> GetAvailableProductsAsync(
            Guid clientId,
            int? maxItems = 20,
            string? searchTerm = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>(Array.Empty<Product>());

        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(Guid clientId, int threshold = 5, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>(Array.Empty<Product>());

        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid productId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(Guid clientId, string skuOrName, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeAiService : IAIService
    {
        private readonly bool _shouldReplyFail;

        public FakeAiService(bool shouldReplyFail = false)
        {
            _shouldReplyFail = shouldReplyFail;
        }

        public Task<IntentDetectionResult> DetectIntentAsync(IntentRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new IntentDetectionResult("inquiry", 0.97));

        public Task<ReplyGenerationResult> GenerateReplyAsync(ReplyRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shouldReplyFail
                ? new ReplyGenerationResult(false, null, "ai-reply-failed")
                : new ReplyGenerationResult(true, "Auto reply"));
        }

        public Task<CaptionGenerationResult> GenerateCaptionAsync(CaptionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new CaptionGenerationResult(true, "caption", new List<string>()));

        public Task<AdCopiesResult> GenerateAdCopiesAsync(AdRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdCopiesResult(true, new List<string> { "ad1" }));
    }

    private sealed class FakeMetaMessagingService : IMetaMessagingService
    {
        private readonly bool _shouldFail;

        public FakeMetaMessagingService(bool shouldFail = false)
        {
            _shouldFail = shouldFail;
        }

        public Task<MessagingSendResult> SendTextMessageAsync(
            Guid clientId,
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
            Guid clientId,
            string platform,
            string recipient,
            string templateName,
            object? templateParameters = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new MessagingSendResult(true, Guid.NewGuid().ToString(), null, 200));

        public Task<MessagingSendResult> SendImageMessageAsync(
            Guid clientId,
            string platform,
            string recipient,
            string imageUrl,
            string? caption = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new MessagingSendResult(true, Guid.NewGuid().ToString(), null, 200));

        public Task MarkMessageAsReadAsync(
            Guid clientId,
            string platform,
            string recipient,
            string messageId,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<MessagingSendResult> SendQuickRepliesAsync(
            Guid clientId,
            string platform,
            string recipient,
            string text,
            IEnumerable<QuickReplyOption> quickReplies,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new MessagingSendResult(true, Guid.NewGuid().ToString(), null, 200));
    }
}
