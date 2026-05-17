namespace LCDPC.Infrastructure.Users.Auth;

public sealed class AuthSecurityOptions
{
    public int AccessTokenTtlMinutes { get; set; } = 60;
    public int RefreshTokenTtlDays { get; set; } = 30;
    public int PasswordResetTtlMinutes { get; set; } = 30;
    public bool RevokeSessionsOnPasswordReset { get; set; } = true;
}
