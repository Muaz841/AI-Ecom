using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Persistence;

public class PlatformDbContext : DbContext
{
    private static readonly MethodInfo TenantAccessorMethod =
        typeof(PlatformDbContext).GetMethod(nameof(GetCurrentTenantId), BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Failed to resolve tenant accessor method.");

    private readonly ICurrentTenantAccessor _tenantAccessor;

    public PlatformDbContext(
        DbContextOptions<PlatformDbContext> options,
        ICurrentTenantAccessor tenantAccessor)
        : base(options)
    {
        _tenantAccessor = tenantAccessor;
    }

    public DbSet<Client> Clients { get; set; } = null!;
    public DbSet<Message> Messages { get; set; } = null!;
    public DbSet<ConversationThread> ConversationThreads { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<ProductVariant> ProductVariants { get; set; } = null!;
    public DbSet<ProductImage> ProductImages { get; set; } = null!;
    public DbSet<ScheduledPost> ScheduledPosts { get; set; } = null!;
    public DbSet<AppLog> AppLogs { get; set; } = null!;

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
            entity.HasIndex(e => e.TenantId);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Platform).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Direction).IsRequired().HasMaxLength(20);
            entity.Property(e => e.MessageType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ExternalMessageId).HasMaxLength(200);
            entity.Property(e => e.DeliveryStatus).HasMaxLength(50);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.RawPayloadJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(e => new { e.TenantId, e.ReceivedAt });
            entity.HasIndex(e => new { e.TenantId, e.ConversationThreadId, e.ReceivedAt });
            entity.HasIndex(e => new { e.TenantId, e.ExternalMessageId });
        });

        modelBuilder.Entity<ConversationThread>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Platform).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CustomerIdentifier).IsRequired().HasMaxLength(200);
            entity.Property(e => e.BusinessIdentifier).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CustomerDisplayName).HasMaxLength(200);
            entity.Property(e => e.LastMessagePreview).HasMaxLength(500);
            entity.Property(e => e.LastMessageDirection).HasMaxLength(20);
            entity.Property(e => e.AssignmentMode).HasMaxLength(20);
            entity.HasIndex(e => new { e.TenantId, e.Platform, e.CustomerIdentifier, e.BusinessIdentifier }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.LastMessageAt });
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(300);
            entity.Property(e => e.BasePrice).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).HasDefaultValue("PKR");
            entity.HasMany(e => e.Variants).WithOne().HasForeignKey(v => v.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Images).WithOne().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.TenantId, e.Name });
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PriceOverride).HasPrecision(18, 2);
            entity.HasIndex(e => new { e.TenantId, e.ProductId });
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.ProductId });
        });

        modelBuilder.Entity<AppLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Direction).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Channel).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Operation).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Endpoint).HasMaxLength(500);
            entity.Property(e => e.ErrorMessage).HasMaxLength(4000);
            entity.Property(e => e.CorrelationId).HasMaxLength(200);
            entity.Property(e => e.RequestPayload).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ResponsePayload).HasColumnType("nvarchar(max)");
            entity.HasIndex(e => new { e.TenantId, e.CreatedAt });
        });

        modelBuilder.Entity<ScheduledPost>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Platform).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(5000);
            entity.Property(e => e.MediaUrl).HasMaxLength(2000);
            entity.Property(e => e.MetaPostId).HasMaxLength(200);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasIndex(e => new { e.TenantId, e.Status, e.ScheduledFor });
            entity.HasIndex(e => new { e.TenantId, e.CreatedAt });
        });
    }

    private Guid? GetCurrentTenantId()
    {
        return _tenantAccessor.GetCurrentTenantId();
    }

    private LambdaExpression BuildTenantFilterExpression(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var tenantIdProperty = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            new[] { typeof(Guid?) },
            parameter,
            Expression.Constant("TenantId"));

        var tenantIdNullable = Expression.Call(Expression.Constant(this), TenantAccessorMethod);
        var hasValue = Expression.Property(tenantIdNullable, nameof(Nullable<Guid>.HasValue));
        var noTenant = Expression.Not(hasValue);
        var equal = Expression.Equal(tenantIdProperty, tenantIdNullable);
        var body = Expression.OrElse(noTenant, equal);

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

        return await base.SaveChangesAsync(cancellationToken);
    }
}
