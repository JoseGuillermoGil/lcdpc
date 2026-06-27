package http

import (
	"encoding/json"
	"net/http"

	"github.com/go-chi/chi/v5"
	chimw "github.com/go-chi/chi/v5/middleware"
	"github.com/jackc/pgx/v5/pgxpool"

	"github.com/lcdpc/lcdpc-go/configs"
	"github.com/lcdpc/lcdpc-go/internal/auth"
	"github.com/lcdpc/lcdpc-go/internal/http/handler"
	"github.com/lcdpc/lcdpc-go/internal/http/middleware"
	"github.com/lcdpc/lcdpc-go/internal/pricing"
	"github.com/lcdpc/lcdpc-go/internal/sede"
	"github.com/lcdpc/lcdpc-go/internal/sync"
)

func NewServer(
	cfg *configs.Config,
	pool *pgxpool.Pool,
	authSvc *auth.Service,
	oauth2Svc *auth.OAuth2Service,
	keySvc *auth.KeyService,
	pricingSvc *pricing.Service,
	sedeSvc *sede.Service,
	syncSvc *sync.Service,
) *chi.Mux {
	r := chi.NewRouter()

	r.Use(chimw.Logger)
	r.Use(chimw.Recoverer)
	r.Use(chimw.RealIP)
	r.Use(middleware.CORS(cfg.CORSAllowedOrigins))
	r.Use(middleware.NoCache())

	authH := handler.NewAuthHandler(authSvc)
	oauth2H := handler.NewOAuth2Handler(oauth2Svc)
	productoH := handler.NewProductoHandler(pricingSvc)
	comboH := handler.NewComboHandler(pricingSvc)
	precioH := handler.NewPrecioHandler(pricingSvc)
	sedeH := handler.NewSedeHandler(sedeSvc)
	syncH := handler.NewSyncHandler(syncSvc)
	healthH := handler.NewHealthHandler(pool)

	// Public
	r.Get("/", func(w http.ResponseWriter, r *http.Request) {
		w.Write([]byte(`{"service":"LCDPC.API","status":"ok"}`))
	})
	r.Get("/health", healthH.Ready)
	r.Get("/api/health", healthH.Health)

	// Discovery
	r.Get("/.well-known/openid-configuration", func(w http.ResponseWriter, r *http.Request) {
		discovery := map[string]interface{}{
			"issuer":                            cfg.OAuth2Issuer,
			"authorization_endpoint":            cfg.OAuth2Issuer + "/oauth2/authorize",
			"token_endpoint":                    cfg.OAuth2Issuer + "/oauth2/token",
			"introspection_endpoint":            cfg.OAuth2Issuer + "/oauth2/introspect",
			"revocation_endpoint":               cfg.OAuth2Issuer + "/oauth2/revoke",
			"response_types_supported":          []string{"code"},
			"grant_types_supported":             []string{"authorization_code", "refresh_token"},
			"code_challenge_methods_supported":  []string{"S256"},
			"subject_types_supported":           []string{"public"},
		}
		w.Header().Set("Content-Type", "application/json")
		json.NewEncoder(w).Encode(discovery)
	})

	// OAuth2 endpoints
	r.Route("/oauth2", func(r chi.Router) {
		r.Get("/authorize", oauth2H.Authorize)
		r.Post("/token", oauth2H.Token)
		r.Post("/introspect", oauth2H.Introspect)
		r.Post("/revoke", oauth2H.Revoke)
	})

	// Auth
	r.Route("/api/v1/auth", func(r chi.Router) {
		r.Post("/register/start", authH.RegisterStart)
		r.Post("/register/verify-email", authH.RegisterVerifyEmail)
		r.Post("/register/profile", authH.RegisterProfile)
		r.Post("/login", authH.Login)
		r.Post("/forgot-password", authH.ForgotPassword)
		r.Post("/reset-password", authH.ResetPassword)
		r.Get("/security-policy", authH.GetSecurityPolicy)

		r.Group(func(r chi.Router) {
			r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
			r.Use(middleware.RequireAuth())

			r.Get("/me", authH.Me)
			r.Post("/refresh", authH.Refresh)
			r.Post("/logout", authH.Logout)

			r.Group(func(r chi.Router) {
				r.Use(middleware.RequireRoles("admin_global"))
				r.Put("/security-policy", authH.UpdateSecurityPolicy)
			})
		})
	})

	// Productos
	r.Route("/api/v1/productos", func(r chi.Router) {
		r.Get("/", productoH.List)
		r.Get("/{id}", productoH.GetByID)

		r.Group(func(r chi.Router) {
			r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
			r.Use(middleware.RequireAuth())
			r.Use(middleware.RequireRoles("admin_global", "admin_sede"))

			r.Post("/", productoH.Create)
			r.Put("/{id}", productoH.Update)

			r.Group(func(r chi.Router) {
				r.Use(middleware.RequireRoles("admin_global"))
				r.Delete("/{id}", productoH.Delete)
			})
		})
	})

	// Combos
	r.Route("/api/v1/combos", func(r chi.Router) {
		r.Get("/", comboH.List)
		r.Get("/{id}", comboH.GetByID)

		r.Group(func(r chi.Router) {
			r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
			r.Use(middleware.RequireAuth())
			r.Use(middleware.RequireRoles("admin_global", "admin_sede"))

			r.Post("/", comboH.Create)
			r.Put("/{id}", comboH.Update)
			r.Post("/{id}/publicar", comboH.Publicar)
			r.Post("/{id}/pausar", comboH.Pausar)

			r.Group(func(r chi.Router) {
				r.Use(middleware.RequireRoles("admin_global"))
				r.Delete("/{id}", comboH.Delete)
			})
		})
	})

	// Precios
	r.Route("/api/v1/precios", func(r chi.Router) {
		r.Get("/{id}", precioH.GetByID)
		r.Get("/producto/{id}", precioH.ListByProductoID)
		r.Get("/sede/{id}", precioH.ListBySedeID)

		r.Group(func(r chi.Router) {
			r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
			r.Use(middleware.RequireAuth())
			r.Use(middleware.RequireRoles("admin_global", "admin_sede"))

			r.Post("/", precioH.Create)
			r.Put("/{id}", precioH.Update)

			r.Group(func(r chi.Router) {
				r.Use(middleware.RequireRoles("admin_global"))
				r.Delete("/{id}", precioH.Delete)
			})
		})
	})

	// Sedes
	r.Route("/api/v1/sedes", func(r chi.Router) {
		r.Get("/", sedeH.List)
		r.Post("/", sedeH.Create)
	})

	// Sync (API Key protected)
	r.Route("/api/v1/sync", func(r chi.Router) {
		r.Use(middleware.APIKeyAuth(pool))
		r.Post("/productos", syncH.SyncProductos)
		r.Post("/combos", syncH.SyncCombos)
	})

	return r
}
