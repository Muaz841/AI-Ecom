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
using EcomAI.Platform.Infrastructure.Persistence.Repositories;
using EcomAI.Platform.Infrastructure.Tenant;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ClientRepository _clientRepository;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ILogger<WebhooksController> _logger;
    private readonly MetaSecrets _metaSecrets;

    public WebhooksController(
        IMediator mediator,
        ClientRepository clientRepository,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<WebhooksController> logger,
        IOptions<MetaSecrets> metaSecretsOptions)
    {
        _mediator = mediator;
        _clientRepository = clientRepository;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
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
            _logger.LogInformation("Webhook verification successful");
            return Ok(challenge);
        }

        _logger.LogWarning("Webhook verification failed");
        return BadRequest("Verification failed");
    }

    [HttpPost("meta")]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        if (!IsValidSignature(Request.Headers["X-Hub-Signature-256"], rawBody))
        {
            _logger.LogWarning("Webhook signature validation failed from IP {RemoteIp}", Request.HttpContext.Connection.RemoteIpAddress);
            return Unauthorized("Invalid signature");
        }

        var payload = JsonSerializer.Deserialize<MetaWebhookPayload>(rawBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (payload?.Entry == null || !payload.Entry.Any())
        {
            _logger.LogWarning("Invalid or empty webhook payload");
            return BadRequest("Invalid payload");
        }

        var firstEntry = payload.Entry[0];
        var value = firstEntry.Changes?.FirstOrDefault()?.Value;

        var client = await _clientRepository.GetByMetaIdentifiersAsync(
            metaPageId: value?.PageId,
            whatsAppBusinessAccountId: value?.From?.BusinessAccountId ?? firstEntry.Id);

        if (client is null)
        {
            _logger.LogWarning("No matching Client for webhook identifiers");
            return NotFound("No matching tenant");
        }

        _tenantAccessor.SetCurrentTenantId(client.Id);

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
                    ClientId: client.Id,
                    Platform: msgValue.MessagingProduct ?? "unknown",
                    From: msg.From ?? string.Empty,
                    To: msgValue.Metadata?.PhoneNumberId ?? string.Empty,
                    Content: content,
                    RawPayloadJson: JsonSerializer.Serialize(msg));

                await _mediator.Send(command, cancellationToken);
            }
        }

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
