using System;
using Microsoft.AspNetCore.Http;

namespace EcomAI.Platform.Infrastructure.Tenant;

public interface ICurrentTenantAccessor
{
    Guid? GetCurrentTenantId();
    void SetCurrentTenantId(Guid tenantId);
}

public class CurrentTenantAccessor : ICurrentTenantAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _overrideTenantId;

    public CurrentTenantAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }
     
    public Guid? GetCurrentTenantId()
    {
        if (_overrideTenantId.HasValue)
        {
            return _overrideTenantId;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = httpContext.User.FindFirst("client_id")?.Value
                           ?? httpContext.User.FindFirst("sub")?.Value;

            if (Guid.TryParse(tenantClaim, out var tenantId))
            {
                return tenantId;
            }
        }

        return null;
    }

    public void SetCurrentTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Cannot set empty tenant ID", nameof(tenantId));
        }

        _overrideTenantId = tenantId;
    }
}
