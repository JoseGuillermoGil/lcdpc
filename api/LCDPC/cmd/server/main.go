package main

import (
	"context"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/lcdpc/lcdpc-go/configs"
	"github.com/lcdpc/lcdpc-go/internal/auth"
	"github.com/lcdpc/lcdpc-go/internal/branch"
	"github.com/lcdpc/lcdpc-go/internal/db"
	"github.com/lcdpc/lcdpc-go/internal/email"
	httpserver "github.com/lcdpc/lcdpc-go/internal/http"
	"github.com/lcdpc/lcdpc-go/internal/order"
	"github.com/lcdpc/lcdpc-go/internal/pricing"
	"github.com/lcdpc/lcdpc-go/internal/rbac"
	"github.com/lcdpc/lcdpc-go/internal/sync"
)

func main() {
	ctx := context.Background()
	cfg := configs.Load()

	pool, err := db.Connect(ctx, cfg.DatabaseURL)
	if err != nil {
		slog.Error("failed to connect to database", "error", err)
		os.Exit(1)
	}
	defer pool.Close()

	slog.Info("database connected")

	if err := db.Seed(ctx, pool, db.SeedConfig{
		SuperUserPassword:         cfg.SuperUserPassword,
		SuperUserAlias:            cfg.SuperUserAlias,
		SuperUserEmail:            cfg.SuperUserEmail,
		SuperUserFirstName:        cfg.SuperUserFirstName,
		SuperUserLastName:         cfg.SuperUserLastName,
		SuperUserIdentityDocument: cfg.SuperUserIdentityDocument,
		SuperUserWhatsAppPhone:    cfg.SuperUserWhatsAppPhone,
		SuperUserFullAddress:      cfg.SuperUserFullAddress,
	}); err != nil {
		slog.Error("failed to seed database", "error", err)
		os.Exit(1)
	}

	keySvc, err := auth.NewKeyService(cfg.PasetoKeyPath)
	if err != nil {
		slog.Error("failed to initialize key service", "error", err)
		os.Exit(1)
	}

	rbacStore := rbac.NewStore()
	if err := rbacStore.LoadFromDB(ctx, pool); err != nil {
		slog.Error("failed to load RBAC store", "error", err)
		os.Exit(1)
	}

	var emailSvc email.Sender
	if cfg.ResendAPIKey != "" {
		emailSvc = email.NewResendSender(cfg.ResendAPIKey, cfg.ResendFrom)
	} else {
		emailSvc = &noopEmailSender{}
		slog.Warn("RESEND_APITOKEN not set, emails will not be sent")
	}

	authSvc := auth.NewService(pool, emailSvc, keySvc, auth.Config{
		PasswordResetTTLMinutes:       cfg.PasswordResetTTLMinutes,
		RevokeSessionsOnPasswordReset: cfg.RevokeSessionsOnPasswordReset,
		OAuth2Issuer:                  cfg.OAuth2Issuer,
		OAuth2Audience:                cfg.OAuth2Audience,
		AccessTokenTTLMin:             cfg.OAuth2AccessTokenTTLMin,
	}, rbacStore)

	oauth2Svc := auth.NewOAuth2Service(pool, keySvc, auth.OAuth2Config{
		AccessTokenTTLMin:   cfg.OAuth2AccessTokenTTLMin,
		RefreshTokenTTLDays: cfg.OAuth2RefreshTokenTTLDays,
		AuthCodeTTLMinutes:  cfg.OAuth2AuthCodeTTLMinutes,
		Issuer:              cfg.OAuth2Issuer,
		Audience:            cfg.OAuth2Audience,
	})

	pricingSvc := pricing.NewService(pool)
	branchSvc := branch.NewService(pool)
	syncSvc := sync.NewService(pool)
	rbacSvc := rbac.NewService(pool, rbacStore)
	orderSvc := order.NewService(pool)

	router := httpserver.NewServer(cfg, pool, authSvc, oauth2Svc, keySvc, pricingSvc, branchSvc, syncSvc, rbacStore, rbacSvc, orderSvc)

	addr := ":" + cfg.Port
	srv := &http.Server{
		Addr:         addr,
		Handler:      router,
		ReadTimeout:  15 * time.Second,
		WriteTimeout: 15 * time.Second,
		IdleTimeout:  60 * time.Second,
	}

	go func() {
		sigCh := make(chan os.Signal, 1)
		signal.Notify(sigCh, syscall.SIGINT, syscall.SIGTERM)
		<-sigCh
		slog.Info("shutting down...")
		shutdownCtx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
		defer cancel()
		if err := srv.Shutdown(shutdownCtx); err != nil {
			slog.Error("server shutdown error", "error", err)
		}
	}()

	slog.Info("server starting", "addr", addr)
	if err := srv.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		slog.Error("server error", "error", err)
		os.Exit(1)
	}
}

type noopEmailSender struct{}

func (n *noopEmailSender) SendOtpAsync(ctx context.Context, to, otpCode string) error {
	slog.Info("OTP email (noop)", "to", to, "otp", otpCode)
	return nil
}

func (n *noopEmailSender) SendPasswordResetAsync(ctx context.Context, to, resetToken string) error {
	slog.Info("Password reset email (noop)", "to", to)
	return nil
}
