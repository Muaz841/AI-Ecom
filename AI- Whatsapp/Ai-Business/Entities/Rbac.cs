using System;

namespace EcomAI.Platform.Business.Entities;

public class Permission : Entity<Guid>, ITenantEntity
{
    public Guid ClientId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Permission()
    {
    }

    public static Permission Create(Guid clientId, string name, string code, string? description = null, bool isSystem = false)
    {
        if (clientId == Guid.Empty)
        {
            throw new ArgumentException("ClientId is required.", nameof(clientId));
        }

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
            TenantId = clientId,
            ClientId = clientId,
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

public class Role : Entity<Guid>, ITenantEntity
{
    public Guid ClientId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Role()
    {
    }

    public static Role Create(Guid clientId, string name, string code, string? description = null, bool isSystem = false)
    {
        if (clientId == Guid.Empty)
        {
            throw new ArgumentException("ClientId is required.", nameof(clientId));
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Role name and code are required.");
        }

        return new Role
        {
            Id = Guid.NewGuid(),
            TenantId = clientId,
            ClientId = clientId,
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
    public Guid ClientId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private RolePermission()
    {
    }

    public static RolePermission Create(Guid clientId, Guid roleId, Guid permissionId)
    {
        if (clientId == Guid.Empty || roleId == Guid.Empty || permissionId == Guid.Empty)
        {
            throw new ArgumentException("ClientId, RoleId and PermissionId are required.");
        }

        return new RolePermission
        {
            Id = Guid.NewGuid(),
            TenantId = clientId,
            ClientId = clientId,
            RoleId = roleId,
            PermissionId = permissionId,
            CreatedAt = DateTime.UtcNow
        };
    }
}

public class UserRole : Entity<Guid>, ITenantEntity
{
    public Guid ClientId { get; private set; }
    public Guid UserAccountId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private UserRole()
    {
    }

    public static UserRole Create(Guid clientId, Guid userAccountId, Guid roleId)
    {
        if (clientId == Guid.Empty || userAccountId == Guid.Empty || roleId == Guid.Empty)
        {
            throw new ArgumentException("ClientId, UserAccountId and RoleId are required.");
        }

        return new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = clientId,
            ClientId = clientId,
            UserAccountId = userAccountId,
            RoleId = roleId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
