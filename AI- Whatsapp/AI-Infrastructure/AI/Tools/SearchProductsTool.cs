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
    public string Description => "Use this tool ONLY when the customer asks to browse, find, or search for products by name or category. Do NOT call this tool unless a search keyword has been mentioned. Returns up to 10 matching products with name, price, stock, and currency.";
    public string ParametersSchema => """{"type":"object","properties":{"query":{"type":"string","description":"The search keyword or product name the customer mentioned"},"sort_by":{"type":"string","description":"Optional sort order for results","enum":["name","price_asc","price_desc","in_stock_first"]}},"required":["query"]}""";

    private readonly PlatformDbContext _db;

    public SearchProductsTool(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task<ToolResult> ExecuteAsync(Guid tenantId, string argumentsJson, CancellationToken ct = default)
    {
        string query;
        string sortBy = "name";
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            query = doc.RootElement.GetProperty("query").GetString()?.Trim() ?? string.Empty;
            if (doc.RootElement.TryGetProperty("sort_by", out var sortEl))
                sortBy = sortEl.GetString() ?? "name";
        }
        catch
        {
            return new ToolResult(ToolName, false,
                JsonSerializer.Serialize(new { error = "Invalid arguments.", suggestion = "Provide a 'query' string with the product name or keyword the customer mentioned." }),
                "Argument parsing failed.");
        }

        if (string.IsNullOrWhiteSpace(query))
            return new ToolResult(ToolName, false,
                JsonSerializer.Serialize(new { error = "Query is empty.", suggestion = "Ask the customer which product they are looking for." }),
                "Query cannot be empty.");

        var baseQuery = _db.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Name.Contains(query));

        var ordered = sortBy switch
        {
            "price_asc"      => baseQuery.OrderBy(p => p.BasePrice),
            "price_desc"     => baseQuery.OrderByDescending(p => p.BasePrice),
            "in_stock_first" => baseQuery.OrderByDescending(p => p.TotalStock),
            _                => baseQuery.OrderBy(p => p.Name)
        };

        var products = await ordered
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

        var result = JsonSerializer.Serialize(new { count = products.Count, products });
        return new ToolResult(ToolName, true, result);
    }
}
