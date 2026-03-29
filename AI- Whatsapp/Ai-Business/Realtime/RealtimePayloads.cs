namespace EcomAI.Platform.Business.Realtime;

/// <summary>
/// Canonical payload for <c>message.received</c> and <c>comment.received</c> events.
/// IDs are strings to avoid Guid serialisation ambiguity on the wire.
/// Timestamps are ISO-8601 strings to avoid DateTime timezone ambiguity.
/// </summary>
public sealed record MessageReceivedPayload(
    string MessageId,
    string ThreadId,
    string TenantId,
    string Platform,
    string From,
    string To,
    string Content,
    string CreatedAtUtc);

/// <summary>Canonical payload for <c>ai.reply.sent</c> events.</summary>
public sealed record AiReplySentPayload(
    string OutgoingMessageId,
    string ThreadId,
    string TenantId,
    string Platform,
    string From,
    string Reply,
    string Intent,
    string SentAtUtc);
