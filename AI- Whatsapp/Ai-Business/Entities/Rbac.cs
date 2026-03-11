using System;

namespace EcomAI.Platform.Business.Entities;

// Permissions are GLOBAL (TenantId = null) — system-defined, shared across all tenants.
// They do NOT implement ITenantEntity so the EF tenant query filter is NOT applied to them.
public class Permission : Entity<Guid>
{
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Permission()
    {
    }

    public static Permission Create(string name, string code, string? description = null, bool isSystem = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Permission name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Permission code is required.", nameof(code));
        }

        return new Permission
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            Name = name.Trim(),
            Code = code.Trim().ToLowerInvariant(),
            Description = description?.Trim(),
            IsSystem = isSystem,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string code, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Name and code are required.");
        }

        Name = name.Trim();
        Code = code.Trim().ToLowerInvariant();
        Description = description?.Trim();
    }
}

// Roles are TENANT-SPECIFIC (TenantId = tenantGuid) or HOST-SCOPED (TenantId = null).
public class Role : Entity<Guid>, ITenantEntity
{
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Role()
    {
    }

    public static Role Create(Guid? tenantId, string name, string code, string? description = null, bool isSystem = false)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty Guid. Use null for host roles.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Role name and code are required.");
        }

        return new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Code = code.Trim().ToLowerInvariant(),
            Description = description?.Trim(),
            IsSystem = isSystem,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string code, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Role name and code are required.");
        }

        Name = name.Trim();
        Code = code.Trim().ToLowerInvariant();
        Description = description?.Trim();
    }
}

public class RolePermission : Entity<Guid>, ITenantEntity
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private RolePermission()
    {
    }

    public static RolePermission Create(Guid? tenantId, Guid roleId, Guid permissionId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty Guid. Use null for host role-permissions.", nameof(tenantId));
        }

        if (roleId == Guid.Empty || permissionId == Guid.Empty)
        {
            throw new ArgumentException("RoleId and PermissionId are required.");
        }

        return new RolePermission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RoleId = roleId,
            PermissionId = permissionId,
            CreatedAt = DateTime.UtcNow
        };
    }
}

public class UserRole : Entity<Guid>, ITenantEntity
{
    public Guid UserAccountId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private UserRole()
    {
    }

    public static UserRole Create(Guid? tenantId, Guid userAccountId, Guid roleId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty Guid. Use null for host user-roles.", nameof(tenantId));
        }

        if (userAccountId == Guid.Empty || roleId == Guid.Empty)
        {
            throw new ArgumentException("UserAccountId and RoleId are required.");
        }

        return new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserAccountId = userAccountId,
            RoleId = roleId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
