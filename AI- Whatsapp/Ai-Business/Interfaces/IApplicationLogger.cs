using System;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface IApplicationLogger
{
    void Info(string messageTemplate, params object?[] args);
    void Warning(string messageTemplate, params object?[] args);
    void Warning(Exception exception, string messageTemplate, params object?[] args);
    void Error(string messageTemplate, params object?[] args);
    void Error(Exception exception, string messageTemplate, params object?[] args);

    Task LogIncomingAsync(
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
        CancellationToken cancellationToken = default);

    Task LogOutgoingAsync(
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
        CancellationToken cancellationToken = default);
}
