using System;
using System.Collections.Generic;
using System.Linq;

namespace EcomAI.Platform.Business.Entities;

public class Product : Entity<Guid>, ITenantEntity
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public decimal BasePrice { get; private set; }
    public string Currency { get; private set; } = "PKR";
    public int TotalStock { get; private set; }
    public string? Sku { get; private set; }
    public string? ExternalId { get; private set; }
    public virtual List<ProductVariant> Variants { get; private set; } = new();
    public virtual List<ProductImage> Images { get; private set; } = new();
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Product()
    {
    }

    public static Product Create(
        Guid tenantId,
        string name,
        decimal basePrice,
        string? description = null,
        string currency = "PKR",
        string? sku = null,
        string? externalId = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name is required", nameof(name));
        }

        if (basePrice <= 0)
        {
            throw new ArgumentException("Base price must be positive", nameof(basePrice));
        }

        if (!string.IsNullOrWhiteSpace(currency) && currency.Length != 3)
        {
            throw new ArgumentException("Currency must be 3-letter ISO code", nameof(currency));
        }

        return new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            BasePrice = basePrice,
            Currency = string.IsNullOrWhiteSpace(currency) ? "PKR" : currency.Trim().ToUpperInvariant(),
            Sku = sku?.Trim(),
            ExternalId = externalId?.Trim(),
            TotalStock = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };
    }

    public void AddVariant(string size, string? color, int stock, decimal? priceOverride = null)
    {
        if (string.IsNullOrWhiteSpace(size))
        {
            throw new ArgumentException("Size is required for variant");
        }

        var variant = ProductVariant.Create(Id, TenantId!.Value, size.Trim(), color?.Trim(), stock, priceOverride);
        Variants.Add(variant);
        UpdateTotalStock();
    }

    public void UpdateStock(int newStock)
    {
        if (newStock < 0)
        {
            throw new ArgumentException("Stock cannot be negative");
        }

        TotalStock = newStock;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddImage(string url, string? altText = null, bool isPrimary = false)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Image URL is required");
        }

        var image = ProductImage.Create(Id, TenantId!.Value, url.Trim(), altText?.Trim(), isPrimary);
        Images.Add(image);

        if (isPrimary)
        {
            foreach (var img in Images.Where(i => i != image && i.IsPrimary))
            {
                img.MarkAsNonPrimary();
            }
        }
    }

    private void UpdateTotalStock()
    {
        TotalStock = Variants.Sum(v => v.Stock);
        UpdatedAt = DateTime.UtcNow;
    }
}

public class ProductVariant : Entity<Guid>, ITenantEntity
{
    public Guid ProductId { get; private set; }
    public string Size { get; private set; } = null!;
    public string? Color { get; private set; }
    public int Stock { get; private set; }
    public decimal? PriceOverride { get; private set; }

    private ProductVariant()
    {
    }

    public static ProductVariant Create(
        Guid productId,
        Guid tenantId,
        string size,
        string? color,
        int stock,
        decimal? priceOverride)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId required");
        }

        if (stock < 0)
        {
            throw new ArgumentException("Stock cannot be negative");
        }

        return new ProductVariant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            Size = size,
            Color = color,
            Stock = stock,
            PriceOverride = priceOverride
        };
    }

    public void UpdateStock(int delta)
    {
        var newStock = Stock + delta;
        if (newStock < 0)
        {
            throw new ArgumentException("Stock cannot go negative");
        }

        Stock = newStock;
    }
}

public class ProductImage : Entity<Guid>, ITenantEntity
{
    public Guid ProductId { get; private set; }
    public string Url { get; private set; } = null!;
    public string? AltText { get; private set; }
    public bool IsPrimary { get; private set; }

    private ProductImage()
    {
    }

    public static ProductImage Create(Guid productId, Guid tenantId, string url, string? altText, bool isPrimary)
    {
        return new ProductImage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            Url = url,
            AltText = altText,
            IsPrimary = isPrimary
        };
    }

    public void MarkAsNonPrimary()
    {
        IsPrimary = false;
    }
}


