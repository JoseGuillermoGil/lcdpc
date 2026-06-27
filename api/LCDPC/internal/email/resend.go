package email

import (
	"context"
	"fmt"
	"log/slog"

	"github.com/resend/resend-go/v2"
)

type ResendSender struct {
	client *resend.Client
	from   string
}

func NewResendSender(apiKey, from string) *ResendSender {
	return &ResendSender{
		client: resend.NewClient(apiKey),
		from:   from,
	}
}

func (s *ResendSender) SendOtpAsync(ctx context.Context, to, otpCode string) error {
	params := &resend.SendEmailRequest{
		From:    s.from,
		To:      []string{to},
		Subject: "Código de verificación LCDPC",
		Html:    fmt.Sprintf("<p>Tu código de verificación es: <strong>%s</strong></p><p>Este código expira en 10 minutos.</p>", otpCode),
	}

	_, err := s.client.Emails.SendWithContext(ctx, params)
	if err != nil {
		slog.Error("failed to send OTP email", "error", err, "to", to)
		return fmt.Errorf("send otp email: %w", err)
	}

	slog.Info("otp email sent", "to", to)
	return nil
}

func (s *ResendSender) SendPasswordResetAsync(ctx context.Context, to, resetToken string) error {
	params := &resend.SendEmailRequest{
		From:    s.from,
		To:      []string{to},
		Subject: "Restablecimiento de contraseña LCDPC",
		Html:    fmt.Sprintf("<p>Usa este token para restablecer tu contraseña: <strong>%s</strong></p><p>Este token expira pronto.</p>", resetToken),
	}

	_, err := s.client.Emails.SendWithContext(ctx, params)
	if err != nil {
		slog.Error("failed to send password reset email", "error", err, "to", to)
		return fmt.Errorf("send password reset email: %w", err)
	}

	slog.Info("password reset email sent", "to", to)
	return nil
}
