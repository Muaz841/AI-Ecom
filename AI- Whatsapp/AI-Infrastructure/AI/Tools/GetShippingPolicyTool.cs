using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.EntityFrameworkCore;
using EcomAI.Platform.Infrastructure.Persistence;

namespace EcomAI.Platform.Infrastructure.AI.Tools;

/// <summary>
/// Returns the tenant's shipping policy extracted from their AI profile brand rules,
/// or a generic fallback when no profile is configured.
/// </summary>
public sealed class GetShippingPolicyTool : IToolHandler
{
    public string ToolName => "get_shipping_policy";
    public string Description => "Use this tool ONLY when the customer asks about delivery times, shipping costs, how long their order will take to arrive, or whether the store ships to their location. No parameters required.";
    public string ParametersSchema => """{"type":"object","properties":{}}""";

    private readonly PlatformDbContext _db;

    public GetShippingPolicyTool(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task<ToolResult> ExecuteAsync(Guid tenantId, string argumentsJson, CancellationToken ct = default)
    {
        var profile = await _db.TenantAIProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, ct);

        // Extract shipping information from brand rules if present, otherwise generic fallback.
        var policy = profile?.BrandRules is not null && profile.BrandRules.Contains("ship", StringComparison.OrdinalIgnoreCase)
            ? profile.BrandRules
            : "Standard shipping: 3-5 business days. Express shipping: 1-2 business days (additional charge). " +
              "Free shipping on orders above the minimum threshold. International shipping available.";

        var result = JsonSerializer.Serialize(new { shipping_policy = policy });
        return new ToolResult(ToolName, true, result);
    }
}
