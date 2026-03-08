using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Api.Extensions;
using EcomAI.Platform.Business.Commands;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence.Repositories;
using EcomAI.Platform.Infrastructure.Tenant;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ClientSecretsRepository _clientSecretsRepository;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IApplicationLogger _appLogger;
    private readonly MetaSecrets _metaSecrets;

    public WebhooksController(
        IMediator mediator,
        ClientSecretsRepository clientSecretsRepository,
        ICurrentTenantAccessor tenantAccessor,
        IApplicationLogger appLogger,
        IOptions<MetaSecrets> metaSecretsOptions)
    {
        _mediator = mediator;
        _clientSecretsRepository = clientSecretsRepository;
        _tenantAccessor = tenantAccessor;
        _appLogger = appLogger;
        _metaSecrets = metaSecretsOptions.Value;
    }

    [HttpGet("meta")]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        const string verifyToken = "your-secure-verify-token-2026";

        if (mode == "subscribe" && token == verifyToken)
        {
            _appLogger.Info("Webhook verification successful");
            return Ok(challenge);
        }

        _appLogger.Warning("Webhook verification failed");
        return BadRequest("Verification failed");
    }

    [HttpPost("meta")]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        if (!IsValidSignature(Request.Headers["X-Hub-Signature-256"], rawBody))
        {
            _appLogger.Warning("Webhook signature validation failed from IP {RemoteIp}", Request.HttpContext.Connection.RemoteIpAddress);
            await TryWriteWebhookLogAsync(
                tenantId: null,
                requestPayload: rawBody,
                isSuccess: false,
                statusCode: 401,
                responsePayload: "Invalid signature",
                errorMessage: "Webhook signature validation failed.",
                cancellationToken: cancellationToken);
            return Unauthorized("Invalid signature");
        }

        var payload = JsonSerializer.Deserialize<MetaWebhookPayload>(rawBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (payload?.Entry == null || !payload.Entry.Any())
        {
            _appLogger.Warning("Invalid or empty webhook payload");
            await TryWriteWebhookLogAsync(
                tenantId: null,
                requestPayload: rawBody,
                isSuccess: false,
                statusCode: 400,
                responsePayload: "Invalid payload",
                errorMessage: "Webhook payload is empty or malformed.",
                cancellationToken: cancellationToken);
            return BadRequest("Invalid payload");
        }

        var firstEntry = payload.Entry[0];
        var value = firstEntry.Changes?.FirstOrDefault()?.Value;

        var clientSecrets = await _clientSecretsRepository.GetByMetaIdentifiersAsync(
            metaPageId: value?.PageId,
            whatsAppBusinessAccountId: value?.From?.BusinessAccountId ?? firstEntry.Id);

        if (clientSecrets is null)
        {
            _appLogger.Warning("No matching tenant client secret for webhook identifiers");
            await TryWriteWebhookLogAsync(
                tenantId: null,
                requestPayload: rawBody,
                isSuccess: false,
                statusCode: 404,
                responsePayload: "No matching tenant",
                errorMessage: "No matching tenant for webhook identifiers.",
                cancellationToken: cancellationToken);
            return NotFound("No matching tenant");
        }

        _tenantAccessor.SetCurrentTenantId(clientSecrets.TenantRefId);

        foreach (var change in firstEntry.Changes ?? new List<MetaChange>())
        {
            var msgValue = change.Value;
            if (msgValue?.Messages == null || !msgValue.Messages.Any())
            {
                continue;
            }

            foreach (var msg in msgValue.Messages)
            {
                var content = msg.Text?.Body;
                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                var command = new ProcessIncomingMessageCommand(
                    TenantId: clientSecrets.TenantRefId,
                    Platform: msgValue.MessagingProduct ?? "unknown",
                    From: msg.From ?? string.Empty,
                    To: msgValue.Metadata?.PhoneNumberId ?? string.Empty,
                    Content: content,
                    RawPayloadJson: JsonSerializer.Serialize(msg),
                    ExternalMessageId: msg.Id);

                await _mediator.Send(command, cancellationToken);
            }
        }

        await TryWriteWebhookLogAsync(
            tenantId: clientSecrets.TenantRefId,
            requestPayload: rawBody,
            isSuccess: true,
            statusCode: 200,
            responsePayload: "Webhook processed",
            errorMessage: null,
            cancellationToken: cancellationToken);

        return Ok();
    }

    private bool IsValidSignature(string? signatureHeader, string payload)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedHex = signatureHeader["sha256=".Length..];
        byte[] expectedHash;

        try
        {
            expectedHash = Convert.FromHexString(expectedHex);
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_metaSecrets.AppSecret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }

    private async Task TryWriteWebhookLogAsync(
        Guid? tenantId,
        string requestPayload,
        bool isSuccess,
        int statusCode,
        string responsePayload,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await _appLogger.LogIncomingAsync(
                tenantId: tenantId,
                channel: "meta-webhook",
                operation: "receive",
                endpoint: Request.Path.Value,
                requestPayload: requestPayload,
                isSuccess: isSuccess,
                statusCode: statusCode,
                responsePayload: responsePayload,
                errorMessage: errorMessage,
                correlationId: HttpContext.TraceIdentifier,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _appLogger.Error(ex, "Failed to persist inbound webhook log.");
        }
    }
}

public class MetaWebhookPayload
{
    public List<MetaEntry>? Entry { get; set; }
}

public class MetaEntry
{
    public string? Id { get; set; }
    public List<MetaChange>? Changes { get; set; }
}

public class MetaChange
{
    public MetaValue? Value { get; set; }
}

public class MetaValue
{
    public string? MessagingProduct { get; set; }
    public MetaMetadata? Metadata { get; set; }
    public List<MetaMessage>? Messages { get; set; }
    public string? PageId { get; set; }
    public MetaFrom? From { get; set; }
}

public class MetaMetadata
{
    public string? PhoneNumberId { get; set; }
}

public class MetaMessage
{
    public string? From { get; set; }
    public string? Id { get; set; }
    public string? Type { get; set; }
    public MetaText? Text { get; set; }
}

public class MetaText
{
    public string? Body { get; set; }
}

public class MetaFrom
{
    public string? BusinessAccountId { get; set; }
}

