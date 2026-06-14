namespace LCDPC.Domain.Entities.OAuth2;

public class OAuth2RefreshToken
{
    public string TokenHash { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Scope { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public string? PreviousTokenHash { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
