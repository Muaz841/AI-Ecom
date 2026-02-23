using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EcomAI.Platform.Infrastructure.Persistence;

public class PlatformDbContext : DbContext
{
    private static readonly MethodInfo TenantAccessorMethod =
        typeof(PlatformDbContext).GetMethod(nameof(GetCurrentTenantId), BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Failed to resolve tenant accessor method.");

    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ILogger<PlatformDbContext> _logger;

    public PlatformDbContext(
        DbContextOptions<PlatformDbContext> options,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<PlatformDbContext> logger)
        : base(options)
    {
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    public DbSet<Client> Clients { get; set; } = null!;
    public DbSet<Message> Messages { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<ProductVariant> ProductVariants { get; set; } = null!;
    public DbSet<ProductImage> ProductImages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(e => typeof(ITenantEntity).IsAssignableFrom(e.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(BuildTenantFilterExpression(entityType.ClrType));
        }

        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.BusinessName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MetaAccessToken).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique(false);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Platform).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.RawPayloadJson).HasColumnType("jsonb");
            entity.HasIndex(e => new { e.ClientId, e.ReceivedAt });
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(300);
            entity.Property(e => e.BasePrice).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).HasDefaultValue("PKR");
            entity.HasMany(e => e.Variants).WithOne().HasForeignKey(v => v.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Images).WithOne().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private Guid? GetCurrentTenantId()
    {
        return _tenantAccessor.GetCurrentTenantId();
    }

    private LambdaExpression BuildTenantFilterExpression(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var clientIdProperty = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            new[] { typeof(Guid) },
            parameter,
            Expression.Constant("ClientId"));

        var tenantIdNullable = Expression.Call(Expression.Constant(this), TenantAccessorMethod);
        var hasValue = Expression.Property(tenantIdNullable, nameof(Nullable<Guid>.HasValue));
        var value = Expression.Property(tenantIdNullable, nameof(Nullable<Guid>.Value));
        var equal = Expression.Equal(clientIdProperty, value);
        var body = Expression.AndAlso(hasValue, equal);

        return Expression.Lambda(body, parameter);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Metadata.FindProperty("CreatedAt") != null)
            {
                entry.Property("CreatedAt").CurrentValue = utcNow;
            }

            if (entry.State == EntityState.Modified && entry.Metadata.FindProperty("UpdatedAt") != null)
            {
                entry.Property("UpdatedAt").CurrentValue = utcNow;
            }
        }

        _logger.LogDebug("Saving {Count} changes to database", ChangeTracker.Entries().Count());
        return await base.SaveChangesAsync(cancellationToken);
    }
}
