using System;

namespace EcomAI.Platform.Business.Entities;

public class AppLog : Entity<Guid>, ITenantEntity
{
    public string Direction { get; private set; } = null!;
    public string Channel { get; private set; } = null!;
    public string Operation { get; private set; } = null!;
    public string? Endpoint { get; private set; }
    public string? RequestPayload { get; private set; }
    public string? ResponsePayload { get; private set; }
    public bool IsSuccess { get; private set; }
    public int? StatusCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private AppLog()
    {
    }

    public static AppLog CreateIncoming(
        Guid? tenantId,
        string channel,
        string operation,
        string? endpoint,
        string? requestPayload,
        bool isSuccess,
        int? statusCode = null,
        string? responsePayload = null,
        string? errorMessage = null,
        string? correlationId = null)
    {
        return Create("incoming", tenantId, channel, operation, endpoint, requestPayload, responsePayload, isSuccess, statusCode, errorMessage, correlationId);
    }

    public static AppLog CreateOutgoing(
        Guid? tenantId,
        string channel,
        string operation,
        string? endpoint,
        string? requestPayload,
        bool isSuccess,
        int? statusCode = null,
        string? responsePayload = null,
        string? errorMessage = null,
        string? correlationId = null)
    {
        return Create("outgoing", tenantId, channel, operation, endpoint, requestPayload, responsePayload, isSuccess, statusCode, errorMessage, correlationId);
    }

    private static AppLog Create(
        string direction,
        Guid? tenantId,
        string channel,
        string operation,
        string? endpoint,
        string? requestPayload,
        string? responsePayload,
        bool isSuccess,
        int? statusCode,
        string? errorMessage,
        string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new ArgumentException("Channel is required.", nameof(channel));
        }

        if (string.IsNullOrWhiteSpace(operation))
        {
            throw new ArgumentException("Operation is required.", nameof(operation));
        }

        return new AppLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Direction = direction,
            Channel = channel.Trim(),
            Operation = operation.Trim(),
            Endpoint = endpoint?.Trim(),
            RequestPayload = requestPayload,
            ResponsePayload = responsePayload,
            IsSuccess = isSuccess,
            StatusCode = statusCode,
            ErrorMessage = errorMessage,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
