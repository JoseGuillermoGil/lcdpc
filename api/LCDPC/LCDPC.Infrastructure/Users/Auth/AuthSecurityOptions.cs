namespace LCDPC.Infrastructure.Users.Auth;

public sealed class AuthSecurityOptions
{
    public int AccessTokenTtlMinutes { get; set; } = 60;
    public int RefreshTokenTtlDays { get; set; } = 30;
    public int PasswordResetTtlMinutes { get; set; } = 30;
    public bool RevokeSessionsOnPasswordReset { get; set; } = true;
}

public sealed class JwtTokenOptions
{
    public string Issuer { get; set; } = "LCDPC.API";
    public string Audience { get; set; } = "LCDPC.Web";
    public string SigningKey { get; set; } = "CHANGE-ME-WITH-AT-LEAST-32-CHARS-DEV-ONLY";
}
