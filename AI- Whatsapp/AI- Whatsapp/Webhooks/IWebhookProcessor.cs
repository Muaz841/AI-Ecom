using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Commands;

namespace EcomAI.Platform.Api.Webhooks;

/// <summary>
/// Processes a raw Meta webhook JSON payload through the full pipeline:
/// tenant resolution → MediatR command dispatch → AI processing → response logging.
/// </summary>
public interface IWebhookProcessor
{
    /// <param name="rawJson">The raw webhook JSON body.</param>
    /// <param name="skipSignature">When true, skips HMAC validation (dev/test use only).</param>
    /// <param name="signatureHeader">Value of X-Hub-Signature-256 header; ignored when skipSignature is true.</param>
    /// <param name="endpoint">Request path used for logging.</param>
    /// <param name="correlationId">Trace/correlation ID used for logging.</param>
    Task<WebhookProcessResult> ProcessAsync(
        string rawJson,
        bool skipSignature,
        string? signatureHeader,
        string endpoint,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed record WebhookProcessResult(
    bool   Success,
    int    StatusCode,
    string Message,
    int    ProcessedCount,
    IReadOnlyList<ProcessIncomingMessageResult> MessageResults,
    string? ErrorMessage = null);
