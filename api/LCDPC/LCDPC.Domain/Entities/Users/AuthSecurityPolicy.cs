namespace LCDPC.Domain.Entities.Users;

public class AuthSecurityPolicy
{
    public Guid Id { get; set; }
    public int PasswordResetTtlMinutes { get; set; }
    public bool RevokeSessionsOnPasswordReset { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
