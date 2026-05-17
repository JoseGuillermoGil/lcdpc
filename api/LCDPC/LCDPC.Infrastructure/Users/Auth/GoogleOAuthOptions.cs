namespace LCDPC.Infrastructure.Users.Auth;

public sealed class GoogleOAuthOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RedirectUri { get; set; }
    public string Scope { get; set; } = "openid email profile";
}
