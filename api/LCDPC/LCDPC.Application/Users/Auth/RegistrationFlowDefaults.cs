namespace LCDPC.Application.Users.Auth;

public static class RegistrationFlowDefaults
{
    public const int OtpTtlMinutes = 10;
    public const int OtpMaxAttempts = 5;
    public const int OtpCooldownMinutes = 10;
}