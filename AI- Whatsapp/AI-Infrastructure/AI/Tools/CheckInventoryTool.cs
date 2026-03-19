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
/// Checks current stock levels for a product by name or ID.
/// Returns variant-level stock breakdown.
/// </summary>
public sealed class CheckInventoryTool : IToolHandler
{
    public string ToolName => "check_inventory";
    public string Description => "Use this tool ONLY when the customer explicitly asks whether a specific product is in stock, available, or how many units remain. Do NOT call unless the customer has named a product. Returns variant-level stock breakdown.";
    public string ParametersSchema => """{"type":"object","properties":{"product_name":{"type":"string","description":"The exact product name or partial name the customer asked about"}},"required":["product_name"]}""";

    private readonly PlatformDbContext _db;

    public CheckInventoryTool(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task<ToolResult> ExecuteAsync(Guid tenantId, string argumentsJson, CancellationToken ct = default)
    {
        string productName;
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            productName = doc.RootElement.GetProperty("product_name").GetString()?.Trim() ?? string.Empty;
        }
        catch
        {
            return new ToolResult(ToolName, false,
                JsonSerializer.Serialize(new { error = "Invalid arguments.", suggestion = "Provide 'product_name' with the product the customer asked about." }),
                "Argument parsing failed.");
        }

        if (string.IsNullOrWhiteSpace(productName))
            return new ToolResult(ToolName, false,
                JsonSerializer.Serialize(new { error = "Product name is empty.", suggestion = "Ask the customer which specific product they want to check." }),
                "Product name cannot be empty.");

        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Name.Contains(productName))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.BasePrice,
                p.Currency,
                Variants = p.Variants.Select(v => new
                {
                    v.Size,
                    v.Color,
                    Stock = v.Stock
                }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (product is null)
        {
            var result = JsonSerializer.Serialize(new { found = false, message = $"No product found matching '{productName}'." });
            return new ToolResult(ToolName, true, result);
        }

        var totalStock = product.Variants.Sum(v => v.Stock);
        var payload = JsonSerializer.Serialize(new
        {
            found = true,
            product.Id,
            product.Name,
            product.BasePrice,
            product.Currency,
            totalStock,
            inStock = totalStock > 0,
            product.Variants
        });

        return new ToolResult(ToolName, true, payload);
    }
}
