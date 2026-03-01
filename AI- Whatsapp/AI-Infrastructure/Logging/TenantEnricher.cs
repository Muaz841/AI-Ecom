using System;
using System.Linq;
using EcomAI.Platform.Infrastructure.Tenant;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace EcomAI.Platform.Infrastructure.Logging;

public class TenantEnricher : ILogEventEnricher
{
    private const string TenantIdProperty = "TenantId";
    private const string CorrelationIdProperty = "CorrelationId";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var tenantId = ResolveTenantId(httpContext);
        var correlationId = ResolveCorrelationId(httpContext);

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(TenantIdProperty, tenantId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(CorrelationIdProperty, correlationId));
    }

    private static string ResolveTenantId(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return "anonymous";
        }

        var requestServices = httpContext.RequestServices;
        if (requestServices is null)
        {
            return "anonymous";
        }

        var accessor = requestServices.GetService<ICurrentTenantAccessor>();
        var tenantId = accessor?.GetCurrentTenantId();
        return tenantId?.ToString() ?? "anonymous";
    }

    private static string ResolveCorrelationId(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return "no-http-context";
        }

        var headerValue = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue;
        }

        return httpContext.TraceIdentifier;
    }
}
