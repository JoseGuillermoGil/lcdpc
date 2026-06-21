using LCDPC.Application.Users.Auth;
using Resend;

namespace LCDPC.Infrastructure.Email;

public class ResendEmailService(IResend resend) : IEmailService
{
    private static readonly string FromAddress =
        Environment.GetEnvironmentVariable("RESEND_FROM_ADDRESS")
        ?? "LCDPC <onboarding@resend.dev>";

    public async Task SendOtpAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage
        {
            From = FromAddress,
            Subject = "Código de verificación - LCDPC",
            HtmlBody = BuildOtpHtml(otpCode)
        };
        message.To.Add(toEmail);

        await resend.EmailSendAsync(message, cancellationToken);
    }

    public async Task SendPasswordResetAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage
        {
            From = FromAddress,
            Subject = "Restablecimiento de contraseña - LCDPC",
            HtmlBody = BuildPasswordResetHtml(resetToken)
        };
        message.To.Add(toEmail);

        await resend.EmailSendAsync(message, cancellationToken);
    }

    private static string BuildOtpHtml(string otpCode) =>
        $"""
        <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 24px;">
            <h2 style="color: #333;">Verificación de correo electrónico</h2>
            <p>Tu código de verificación es:</p>
            <p style="font-size: 32px; font-weight: bold; letter-spacing: 8px; text-align: center; padding: 16px; background: #f4f4f4; border-radius: 8px;">
                {otpCode}
            </p>
            <p style="color: #666; font-size: 14px;">Este código expira en 2 minutos. Si no solicitaste este registro, puedes ignorar este correo.</p>
        </div>
        """;

    private static string BuildPasswordResetHtml(string resetToken) =>
        $"""
        <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 24px;">
            <h2 style="color: #333;">Restablecimiento de contraseña</h2>
            <p>Has solicitado restablecer tu contraseña. Usa el siguiente token:</p>
            <p style="font-size: 14px; font-weight: bold; text-align: center; padding: 12px; background: #f4f4f4; border-radius: 8px; word-break: break-all;">
                {resetToken}
            </p>
            <p style="color: #666; font-size: 14px;">Este token expira en 30 minutos. Si no solicitaste este cambio, puedes ignorar este correo.</p>
        </div>
        """;
}
