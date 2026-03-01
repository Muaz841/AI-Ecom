using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Tenant;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EcomAI.Platform.Infrastructure.Logging;

public class ApplicationLogger : IApplicationLogger
{
    private readonly ILogger<ApplicationLogger> _logger;
    private readonly IRepository<AppLog> _appLogRepository;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApplicationLogger(
        ILogger<ApplicationLogger> logger,
        IRepository<AppLog> appLogRepository,
        ICurrentTenantAccessor tenantAccessor,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _appLogRepository = appLogRepository;
        _tenantAccessor = tenantAccessor;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Info(string messageTemplate, params object?[] args)
    {
        _logger.LogInformation(messageTemplate, args);
        _ = WriteStandardLogAsync("info", messageTemplate, args);
    }

    public void Warning(string messageTemplate, params object?[] args)
    {
        _logger.LogWarning(messageTemplate, args);
        _ = WriteStandardLogAsync("warning", messageTemplate, args);
    }

    public void Warning(Exception exception, string messageTemplate, params object?[] args)
    {
        _logger.LogWarning(exception, messageTemplate, args);
        _ = WriteStandardLogAsync("warning", messageTemplate, args, exception);
    }

    public void Error(string messageTemplate, params object?[] args)
    {
        _logger.LogError(messageTemplate, args);
        _ = WriteStandardLogAsync("error", messageTemplate, args);
    }

    public void Error(Exception exception, string messageTemplate, params object?[] args)
    {
        _logger.LogError(exception, messageTemplate, args);
        _ = WriteStandardLogAsync("error", messageTemplate, args, exception);
    }

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
        CancellationToken cancellationToken = default)
        => WriteAppLogAsync(AppLog.CreateIncoming(
            tenantId,
            channel,
            operation,
            endpoint,
            requestPayload,
            isSuccess,
            statusCode,
            responsePayload,
            errorMessage,
            correlationId), cancellationToken);

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
        CancellationToken cancellationToken = default)
        => WriteAppLogAsync(AppLog.CreateOutgoing(
            tenantId,
            channel,
            operation,
            endpoint,
            requestPayload,
            isSuccess,
            statusCode,
            responsePayload,
            errorMessage,
            correlationId), cancellationToken);

    private async Task WriteAppLogAsync(AppLog appLog, CancellationToken cancellationToken)
    {
        try
        {
            await _appLogRepository.AddAsync(appLog);
            await _appLogRepository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist application log.");
        }
    }

    private Task WriteStandardLogAsync(
        string level,
        string messageTemplate,
        object?[] args,
        Exception? exception = null)
    {
        var tenantId = _tenantAccessor.GetCurrentTenantId();
        var endpoint = _httpContextAccessor.HttpContext?.Request?.Path.Value;
        var correlationId = _httpContextAccessor.HttpContext?.TraceIdentifier;

        var payload = JsonSerializer.Serialize(new
        {
            level,
            template = messageTemplate,
            args
        });

        var appLog = AppLog.CreateOutgoing(
            tenantId: tenantId,
            channel: "application",
            operation: level,
            endpoint: endpoint,
            requestPayload: payload,
            isSuccess: level != "error",
            statusCode: null,
            responsePayload: null,
            errorMessage: exception?.ToString(),
            correlationId: correlationId);

        return WriteAppLogAsync(appLog, CancellationToken.None);
    }
}
