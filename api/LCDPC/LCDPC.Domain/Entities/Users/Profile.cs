namespace LCDPC.Domain.Entities.Users;

public class Profile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string IdentityDocument { get; set; } = string.Empty;
    public string? Rif { get; set; }
    public string WhatsAppPhone { get; set; } = string.Empty;
    public string FullAddress { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;

    public string FullName => $"{FirstName} {LastName}".Trim();
}