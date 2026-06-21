namespace LCDPC.Application.Users.Auth;

public interface IEmailService
{
    Task SendOtpAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default);
}
