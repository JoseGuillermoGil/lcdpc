namespace LCDPC.Domain.Entities.Users;

public class RegistrationFlow
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = "pending_email_verification";
    public string OtpCode { get; set; } = string.Empty;
    public string OtpHash { get; set; } = string.Empty;
    public DateTime? VerifiedAtUtc { get; set; }
    public DateTime OtpExpiresAtUtc { get; set; }
    public int OtpAttempts { get; set; }
    public DateTime? OtpBlockedUntilUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}