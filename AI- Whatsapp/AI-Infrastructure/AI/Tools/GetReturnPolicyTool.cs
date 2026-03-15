using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.EntityFrameworkCore;
using EcomAI.Platform.Infrastructure.Persistence;

namespace EcomAI.Platform.Infrastructure.AI.Tools;

/// <summary>
/// Returns the tenant's return and refund policy extracted from their AI profile brand rules,
/// or a generic fallback when no profile is configured.
/// </summary>
public sealed class GetReturnPolicyTool : IToolHandler
{
    public string ToolName => "get_return_policy";
    public string Description => "Retrieve the store's return and refund policy, including timeframes and conditions.";
    public string ParametersSchema => """{"type":"object","properties":{}}""";

    private readonly PlatformDbContext _db;

    public GetReturnPolicyTool(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task<ToolResult> ExecuteAsync(Guid tenantId, string argumentsJson, CancellationToken ct = default)
    {
        var profile = await _db.TenantAIProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, ct);

        var policy = profile?.BrandRules is not null && profile.BrandRules.Contains("return", StringComparison.OrdinalIgnoreCase)
            ? profile.BrandRules
            : "30-day return policy on unused items in original packaging. Refunds processed within 5-7 business days. " +
              "Sale items are final sale. Contact support to initiate a return.";

        var result = JsonSerializer.Serialize(new { return_policy = policy });
        return new ToolResult(ToolName, true, result);
    }
}
