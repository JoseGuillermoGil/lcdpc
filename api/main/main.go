package main

import (
	"context"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/joho/godotenv"
	"github.com/lcdpc/lcdpc-go/configs"
	"github.com/lcdpc/lcdpc-go/internal/auth"
	"github.com/lcdpc/lcdpc-go/internal/branch"
	"github.com/lcdpc/lcdpc-go/internal/brand"
	"github.com/lcdpc/lcdpc-go/internal/category"
	"github.com/lcdpc/lcdpc-go/internal/db"
	"github.com/lcdpc/lcdpc-go/internal/email"
	httpserver "github.com/lcdpc/lcdpc-go/internal/http"
	"github.com/lcdpc/lcdpc-go/internal/order"
	"github.com/lcdpc/lcdpc-go/internal/pricing"
	"github.com/lcdpc/lcdpc-go/internal/rbac"
	"github.com/lcdpc/lcdpc-go/internal/staff"
	"github.com/lcdpc/lcdpc-go/internal/sync"
	"github.com/lcdpc/lcdpc-go/internal/systemconfig"
)

func main() {
	_ = godotenv.Load()

	ctx := context.Background()
	cfg := configs.Load()

	if err := os.MkdirAll("static/img", 0755); err != nil {
		slog.Error("failed to create static directory", "error", err)
		os.Exit(1)
	}

	pool, err := db.Connect(ctx, cfg.DatabaseURL)
	if err != nil {
		slog.Error("failed to connect to database", "error", err)
		os.Exit(1)
	}
	defer pool.Close()

	slog.Info("database connected")

	if err := db.RunMigrations(cfg.DatabaseURL); err != nil {
		slog.Error("failed to run migrations", "error", err)
		os.Exit(1)
	}

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
	switch cfg.EmailDriver {
	case "smtp":
		emailSvc = email.NewSMTPSender(cfg.SMTPHost, cfg.SMTPPort, cfg.SMTPUsername, cfg.SMTPPassword, cfg.SMTPFrom)
		slog.Info("email driver configured", "driver", "smtp", "host", cfg.SMTPHost, "port", cfg.SMTPPort)
	case "resend":
		if cfg.ResendAPIKey != "" {
			emailSvc = email.NewResendSender(cfg.ResendAPIKey, cfg.ResendFrom)
			slog.Info("email driver configured", "driver", "resend")
		} else {
			emailSvc = &email.NoopSender{}
			slog.Warn("RESEND_APITOKEN not set, emails will not be sent")
		}
	default:
		emailSvc = &email.NoopSender{}
		slog.Warn("unknown EMAIL_DRIVER, falling back to noop", "driver", cfg.EmailDriver)
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
	brandSvc := brand.NewService(pool)
	categorySvc := category.NewService(pool)
	staffSvc := staff.NewService(pool, rbacStore)
	syncSvc := sync.NewService(pool)
	rbacSvc := rbac.NewService(pool, rbacStore)
	orderSvc := order.NewService(pool)
	systemConfigSvc := systemconfig.NewService(pool)

	router := httpserver.NewServer(cfg, pool, authSvc, oauth2Svc, keySvc, pricingSvc, branchSvc, brandSvc, categorySvc, staffSvc, syncSvc, rbacStore, rbacSvc, orderSvc, systemConfigSvc)

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

