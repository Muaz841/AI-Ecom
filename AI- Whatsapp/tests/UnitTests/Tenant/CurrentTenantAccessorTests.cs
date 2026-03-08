using System;
using System.Collections.Generic;
using System.Security.Claims;
using EcomAI.Platform.Infrastructure.Tenant;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace UnitTests.Tenant;

public class CurrentTenantAccessorTests
{
    [Fact]
    public void GetCurrentTenantId_Returns_Override_When_Set()
    {
        var accessor = new CurrentTenantAccessor(new HttpContextAccessor());
        var tenantId = Guid.NewGuid();

        accessor.SetCurrentTenantId(tenantId);

        Assert.Equal(tenantId, accessor.GetCurrentTenantId());
    }

    [Fact]
    public void GetCurrentTenantId_Reads_ClientId_Claim_When_Authenticated()
    {
        var tenantId = Guid.NewGuid();
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new List<Claim> { new("tenant_id", tenantId.ToString()) },
                    "Bearer"))
            }
        };

        var accessor = new CurrentTenantAccessor(httpContextAccessor);

        Assert.Equal(tenantId, accessor.GetCurrentTenantId());
    }
}

