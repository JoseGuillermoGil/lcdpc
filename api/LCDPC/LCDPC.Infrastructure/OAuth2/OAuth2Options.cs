namespace LCDPC.Infrastructure.OAuth2;

public sealed class OAuth2Options
{
    public string Issuer { get; set; } = "http://localhost:8080";
    public string Audience { get; set; } = "lcdpc-api";
    public int AccessTokenTtlMinutes { get; set; } = 60;
    public int RefreshTokenTtlDays { get; set; } = 30;
    public int AuthorizationCodeTtlMinutes { get; set; } = 10;
    public string? RsaKeyPath { get; set; }
    public List<OAuth2ClientOptions> Clients { get; set; } = [];
}

public sealed class OAuth2ClientOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string[] RedirectUris { get; set; } = [];
    public string[] GrantTypes { get; set; } = [];
    public bool RequirePkce { get; set; }
    public string AllowedScopes { get; set; } = string.Empty;
}
