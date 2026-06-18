namespace LCDPC.Infrastructure.Users.Auth;

public sealed class AuthSecurityOptions
{
    public int PasswordResetTtlMinutes { get; set; } = 30;
    public bool RevokeSessionsOnPasswordReset { get; set; } = true;
}
