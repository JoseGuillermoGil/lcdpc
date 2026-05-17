namespace LCDPC.Domain.Entities.Users;

public class UserRoleAssignment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid[] SedeIds { get; set; } = Array.Empty<Guid>();
    public bool Active { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}