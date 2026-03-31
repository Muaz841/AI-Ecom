using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;
using TenantEntity = EcomAI.Platform.Business.Entities.Tenant;

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

    public DbSet<TenantEntity> Tenants { get; set; } = null!;
    public DbSet<ClientSecrets> ClientsSecrets { get; set; } = null!;
    public DbSet<Message> Messages { get; set; } = null!;
    public DbSet<ConversationThread> ConversationThreads { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<ProductVariant> ProductVariants { get; set; } = null!;
    public DbSet<ProductImage> ProductImages { get; set; } = null!;
    public DbSet<ScheduledPost> ScheduledPosts { get; set; } = null!;
    public DbSet<AppLog> AppLogs { get; set; } = null!;
    public DbSet<UserAccount> UserAccounts { get; set; } = null!;
    public DbSet<UserRefreshToken> UserRefreshTokens { get; set; } = null!;
    public DbSet<UserPasswordResetToken> UserPasswordResetTokens { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public DbSet<UserRole> UserRoles { get; set; } = null!;
    public DbSet<MetaChannelConnection> MetaChannelConnections { get; set; } = null!;
    public DbSet<MetaChannelAsset> MetaChannelAssets { get; set; } = null!;
    public DbSet<MetaOAuthState> MetaOAuthStates { get; set; } = null!;
    public DbSet<PlatformMetaConfig> PlatformMetaConfigs { get; set; } = null!;
    public DbSet<PlatformAiConfig> PlatformAiConfigs { get; set; } = null!;
    public DbSet<TenantAIProfile> TenantAIProfiles { get; set; } = null!;
    public DbSet<PosePrescript> PosePrescripts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply tenant filter to all ITenantEntity types EXCEPT Permission (which is global).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(e => typeof(ITenantEntity).IsAssignableFrom(e.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(BuildTenantFilterExpression(entityType.ClrType));
        }

        modelBuilder.Entity<TenantEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.BusinessName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.IsHost).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.TenantId);
            entity.ToTable("Tenants");
        });

        modelBuilder.Entity<ClientSecrets>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MetaPageId).HasMaxLength(200);
            entity.Property(e => e.WhatsAppBusinessAccountId).HasMaxLength(200);
            entity.Property(e => e.TenantRefId).IsRequired();
            entity.HasIndex(e => e.TenantRefId).IsUnique();
            entity.HasIndex(e => e.TenantId);
            entity.HasOne<TenantEntity>()
                .WithMany()
                .HasForeignKey(e => e.TenantRefId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable("ClientsSecrets");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Platform).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Direction)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<EcomAI.Platform.Business.MessageDirection>(v, ignoreCase: true));
            entity.Property(e => e.MessageType)
                .IsRequired()
                .HasMaxLength(50)
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<EcomAI.Platform.Business.MessageType>(v, ignoreCase: true));
            entity.Property(e => e.DeliveryStatus)
                .HasMaxLength(50)
                .HasConversion(
                    v => v.HasValue ? v.Value.ToString().ToLowerInvariant() : null,
                    v => string.IsNullOrEmpty(v) ? null : Enum.Parse<EcomAI.Platform.Business.DeliveryStatus>(v, ignoreCase: true));
            entity.Property(e => e.ExternalMessageId).HasMaxLength(200);
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
            entity.Property(e => e.LastMessageDirection)
                .HasMaxLength(20)
                .HasConversion(
                    v => v.HasValue ? v.Value.ToString().ToLowerInvariant() : null,
                    v => string.IsNullOrEmpty(v) ? null : Enum.Parse<EcomAI.Platform.Business.MessageDirection>(v, ignoreCase: true));
            entity.Property(e => e.AssignmentMode)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<EcomAI.Platform.Business.AssignmentMode>(v, ignoreCase: true));
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

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(320);
            entity.Property(e => e.NormalizedEmail).IsRequired().HasMaxLength(320);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            // Unique per tenant (NULL = host user, GUID = tenant user)
            entity.HasIndex(e => new { e.TenantId, e.NormalizedEmail }).IsUnique();
            entity.HasIndex(e => e.TenantId);
            entity.HasMany(e => e.RefreshTokens).WithOne().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.PasswordResetTokens).WithOne().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.UserRoles).WithOne().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Cascade);
            // FK to Tenant is optional — host users have TenantId = null
            entity.HasOne<TenantEntity>()
                .WithMany()
                .HasForeignKey("TenantId")
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserRefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(256);
            entity.Property(e => e.RevokedReason).HasMaxLength(500);
            entity.HasIndex(e => new { e.TenantId, e.UserAccountId });
            entity.HasIndex(e => e.TokenHash).IsUnique();
        });

        modelBuilder.Entity<UserPasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => new { e.TenantId, e.UserAccountId });
            entity.HasIndex(e => e.TokenHash).IsUnique();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(1000);
            // TenantId=null for host roles; uniqueness is (TenantId, Code)
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
        });

        // Permission is GLOBAL — TenantId always null, no tenant filter applied.
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.TenantId).IsRequired(false);
            // Globally unique by code — TenantId is always null
            entity.HasIndex(e => e.Code).IsUnique();
            entity.ToTable("Permissions");
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.RoleId, e.PermissionId }).IsUnique();
            entity.HasOne<Role>().WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Permission>().WithMany().HasForeignKey(e => e.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.UserAccountId, e.RoleId }).IsUnique();
            entity.HasOne<UserAccount>().WithMany(x => x.UserRoles).HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Role>().WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MetaChannelConnection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Channel).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(30);
            entity.Property(e => e.ExternalBusinessId).HasMaxLength(200);
            entity.Property(e => e.ExternalAccountId).HasMaxLength(200);
            entity.Property(e => e.AccessTokenCiphertext).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property(e => e.RefreshTokenCiphertext).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ScopesCsv).HasMaxLength(2000);
            entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.HasIndex(e => new { e.TenantId, e.Channel }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Status });
        });

        modelBuilder.Entity<MetaChannelAsset>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Channel).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AssetType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ExternalId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ExternalName).HasMaxLength(300);
            entity.Property(e => e.PageAccessTokenCiphertext).HasColumnType("nvarchar(max)");
            // One connection owns its assets; unique asset per (tenant, connection, assetType, externalId)
            entity.HasIndex(e => new { e.TenantId, e.ConnectionId, e.AssetType, e.ExternalId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Channel, e.IsActive });
            // Fast tenant lookup by external ID for webhook routing
            entity.HasIndex(e => new { e.ExternalId, e.Channel, e.IsActive });
            entity.HasOne<MetaChannelConnection>()
                .WithMany()
                .HasForeignKey(e => e.ConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable("MetaChannelAssets");
        });

        modelBuilder.Entity<MetaOAuthState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Channel).IsRequired().HasMaxLength(50);
            entity.Property(e => e.State).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ReturnUrl).HasMaxLength(2000);
            entity.HasIndex(e => e.State).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Channel, e.ExpiresAtUtc });
        });

        // Singleton platform-level Meta app config — no tenant filter, single row.
        modelBuilder.Entity<PlatformMetaConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AppId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.AppSecretProtected).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property(e => e.LoginConfigurationId).HasMaxLength(200);
            entity.Property(e => e.GraphVersion).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CallbackBaseUrl).HasMaxLength(500);
            entity.ToTable("PlatformMetaConfigs");
        });

        // Singleton platform-level AI provider config — no tenant filter, single row.
        // NOTE: PlatformAiConfig inherits TenantId from the base entity and therefore
        // receives the global EF query filter. The repository MUST call IgnoreQueryFilters()
        // to bypass it — otherwise the row is invisible in tenant request contexts.
        modelBuilder.Entity<PlatformAiConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Store enum as its string name so the DB column stays human-readable ("Gemini", "OpenAI", "Ollama").
            entity.Property(e => e.ActiveProvider).IsRequired().HasMaxLength(50).HasConversion<string>();
            entity.Property(e => e.OllamaEndpoint).IsRequired().HasMaxLength(500);
            entity.Property(e => e.OllamaModel).IsRequired().HasMaxLength(200);
            entity.Property(e => e.OpenAIModel).IsRequired().HasMaxLength(200);
            entity.Property(e => e.OpenAIApiKeyProtected).HasColumnType("nvarchar(max)");
            entity.Property(e => e.GeminiModel).IsRequired().HasMaxLength(200);
            entity.Property(e => e.GeminiApiKeyProtected).HasColumnType("nvarchar(max)");
            entity.Property(e => e.VisionModelName).HasMaxLength(200);
            entity.Property(e => e.ImageGenerationModelName).HasMaxLength(200);
            entity.Property(e => e.MessagingModelName).HasMaxLength(200);
            entity.ToTable("PlatformAiConfigs");
        });

        // Per-tenant pose library — tenant-scoped, soft-delete via IsActive.
        modelBuilder.Entity<PosePrescript>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PoseScript).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property(e => e.ReferenceImagePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedByUserId).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasIndex(e => new { e.TenantId, e.IsActive });
            entity.HasOne<TenantEntity>()
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable("PosePrescripts");
        });

        // Per-tenant AI persona profile — one row per tenant, subject to EF tenant filter.
        modelBuilder.Entity<TenantAIProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.SystemPrompt).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property(e => e.Tone).HasMaxLength(100);
            entity.Property(e => e.Language).HasMaxLength(50);
            entity.Property(e => e.BrandRules).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ForbiddenTopics).HasColumnType("nvarchar(max)");
            entity.Property(e => e.DefaultResponseStyle).HasColumnType("nvarchar(max)");
            entity.Property(e => e.PoseExtractionPrompt).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ImageGenerationPrompt).HasColumnType("nvarchar(max)");
            entity.Property(e => e.AiCallsPerHourLimit).HasDefaultValue(200);
            entity.Property(e => e.Version).HasDefaultValue(1);
            // One profile per tenant
            entity.HasIndex(e => e.TenantId).IsUnique();
            entity.HasOne<TenantEntity>()
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable("TenantAIProfiles");
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
