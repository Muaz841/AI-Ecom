using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Api.Webhooks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookProcessor   _processor;
    private readonly MetaWebhookSettings _webhookSettings;

    public WebhooksController(
        IWebhookProcessor            processor,
        IOptions<MetaWebhookSettings> webhookSettings)
    {
        _processor       = processor;
        _webhookSettings = webhookSettings.Value;
    }

    [HttpGet("meta")]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")]         string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")]    string? challenge)
    {
        var verifyToken = _webhookSettings.VerifyToken;
        if (string.IsNullOrWhiteSpace(verifyToken))
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Webhook not configured.");

        if (mode == "subscribe" && token == verifyToken)
            return Ok(challenge);

        return BadRequest("Verification failed");
    }

    [HttpPost("meta")]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody      = await reader.ReadToEndAsync(cancellationToken);

        var result = await _processor.ProcessAsync(
            rawJson:         rawBody,
            skipSignature:   false,
            signatureHeader: Request.Headers["X-Hub-Signature-256"],
            endpoint:        Request.Path.Value ?? "/api/webhooks/meta",
            correlationId:   HttpContext.TraceIdentifier,
            cancellationToken: cancellationToken);

        return StatusCode(result.StatusCode, result.Message);
    }
}
