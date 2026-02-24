using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Polly.Registry;

namespace EcomAI.Platform.Infrastructure.ExternalServices;

public class MetaMessagingService : IMetaMessagingService
{
    public const string HttpClientName = "MetaApi";
    public const string SendMessagePipeline = "meta-send-retry";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ClientRepository _clientRepository;
    private readonly ILogger<MetaMessagingService> _logger;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;

    public MetaMessagingService(
        IHttpClientFactory httpClientFactory,
        ClientRepository clientRepository,
        ILogger<MetaMessagingService> logger,
        ResiliencePipelineRegistry<string> pipelineRegistry)
    {
        _httpClientFactory = httpClientFactory;
        _clientRepository = clientRepository;
        _logger = logger;
        _pipelineProvider = pipelineRegistry;
    }

    public async Task<MessagingSendResult> SendTextMessageAsync(
        Guid clientId,
        string platform,
        string recipient,
        string messageText,
        string? messagingType = "RESPONSE",
        CancellationToken cancellationToken = default)
    {
        var client = await _clientRepository.GetByIdAsync(clientId);
        if (client == null)
        {
            return new MessagingSendResult(false, null, "Client not found.", 404);
        }

        if (string.IsNullOrWhiteSpace(client.MetaAccessToken))
        {
            return new MessagingSendResult(false, null, "Client Meta access token is missing.", 500);
        }

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", client.MetaAccessToken);

        var endpoint = platform.Equals("whatsapp", StringComparison.OrdinalIgnoreCase)
            ? $"v20.0/{recipient}/messages"
            : "v20.0/me/messages";

        var payload = new
        {
            messaging_type = messagingType,
            recipient = new { id = recipient },
            message = new { text = messageText }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var pipeline = _pipelineProvider.GetPipeline(SendMessagePipeline);
        return await pipeline.ExecuteAsync(async token =>
        {
            var response = await httpClient.PostAsync(endpoint, content, token);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SendResponse>(cancellationToken: token);
                return new MessagingSendResult(true, result?.MessageId);
            }

            var errorBody = await response.Content.ReadAsStringAsync(token);
            _logger.LogError("Meta send failed for client {ClientId}: {Status} - {Body}", clientId, response.StatusCode, errorBody);
            return new MessagingSendResult(false, null, errorBody, (int)response.StatusCode);
        }, cancellationToken);
    }

    public Task<MessagingSendResult> SendTemplateMessageAsync(
        Guid clientId,
        string platform,
        string recipient,
        string templateName,
        object? templateParameters = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Template sending not implemented yet.");
    }

    public Task<MessagingSendResult> SendImageMessageAsync(
        Guid clientId,
        string platform,
        string recipient,
        string imageUrl,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Image sending not implemented yet.");
    }

    public Task MarkMessageAsReadAsync(
        Guid clientId,
        string platform,
        string recipient,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Mark as read not implemented yet.");
    }

    public Task<MessagingSendResult> SendQuickRepliesAsync(
        Guid clientId,
        string platform,
        string recipient,
        string text,
        IEnumerable<QuickReplyOption> quickReplies,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Quick replies not implemented yet.");
    }

    private sealed class SendResponse
    {
        [JsonPropertyName("message_id")]
        public string? MessageId { get; set; }
    }
}
