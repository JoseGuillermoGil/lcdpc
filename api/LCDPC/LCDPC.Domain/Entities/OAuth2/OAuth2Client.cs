namespace LCDPC.Domain.Entities.OAuth2;

public class OAuth2Client
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string[] RedirectUris { get; set; } = [];
    public string[] GrantTypes { get; set; } = [];
    public bool RequirePkce { get; set; }
    public string AllowedScopes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
