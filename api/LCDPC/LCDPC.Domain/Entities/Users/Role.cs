namespace LCDPC.Domain.Entities.Users;

public class Role
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ICollection<UserRoleAssignment> Assignments { get; set; } = new List<UserRoleAssignment>();
    public ICollection<RoleResourcePermission> ResourcePermissions { get; set; } = new List<RoleResourcePermission>();
}

public class ApiResource
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ICollection<RoleResourcePermission> RolePermissions { get; set; } = new List<RoleResourcePermission>();
}

public class RoleResourcePermission
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public Guid ResourceId { get; set; }
    public bool CanView { get; set; }
    public bool CanWrite { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
    public bool CanAll { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Role Role { get; set; } = null!;
    public ApiResource Resource { get; set; } = null!;
}