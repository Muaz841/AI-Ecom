using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.EntityFrameworkCore;
using EcomAI.Platform.Infrastructure.Persistence;

namespace EcomAI.Platform.Infrastructure.AI.Tools;

/// <summary>
/// Searches the tenant's product catalog by keyword.
/// Returns matching products with name, price, stock, and variants.
/// </summary>
public sealed class SearchProductsTool : IToolHandler
{
    public string ToolName => "search_products";
    public string Description => "Search the store's product catalog by keyword. Returns matching products with price and availability.";
    public string ParametersSchema => """{"type":"object","properties":{"query":{"type":"string","description":"Search keyword"}},"required":["query"]}""";

    private readonly PlatformDbContext _db;

    public SearchProductsTool(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task<ToolResult> ExecuteAsync(Guid tenantId, string argumentsJson, CancellationToken ct = default)
    {
        string query;
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            query = doc.RootElement.GetProperty("query").GetString()?.Trim() ?? string.Empty;
        }
        catch
        {
            return new ToolResult(ToolName, false, "{}",  "Invalid arguments: 'query' is required.");
        }

        if (string.IsNullOrWhiteSpace(query))
            return new ToolResult(ToolName, false, "{}", "Query cannot be empty.");

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Name.Contains(query))
            .OrderBy(p => p.Name)
            .Take(10)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.BasePrice,
                p.Currency,
                p.Description,
                p.TotalStock
            })
            .ToListAsync(ct);

        var result = JsonSerializer.Serialize(new
        {
            count = products.Count,
            products
        });

        return new ToolResult(ToolName, true, result);
    }
}
