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
	"github.com/lcdpc/lcdpc-go/internal/order"
	"github.com/lcdpc/lcdpc-go/internal/pricing"
	"github.com/lcdpc/lcdpc-go/internal/rbac"
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
	rbacStore *rbac.Store,
	rbacSvc *rbac.Service,
	orderSvc *order.Service,
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
	rbacH := rbac.NewHandler(rbacSvc)
	orderH := order.NewHandler(orderSvc)

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
				r.Use(middleware.RequirePermission(rbacStore, "security-policy:update"))
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
			r.Use(middleware.RequirePermission(rbacStore, "product:create"))

			r.Post("/", productoH.Create)
			r.Put("/{id}", productoH.Update)

			r.Group(func(r chi.Router) {
				r.Use(middleware.RequirePermission(rbacStore, "product:delete"))
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
			r.Use(middleware.RequirePermission(rbacStore, "combo:create"))

			r.Post("/", comboH.Create)
			r.Put("/{id}", comboH.Update)
			r.Post("/{id}/publicar", comboH.Publicar)
			r.Post("/{id}/pausar", comboH.Pausar)

			r.Group(func(r chi.Router) {
				r.Use(middleware.RequirePermission(rbacStore, "combo:delete"))
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
			r.Use(middleware.RequirePermission(rbacStore, "price:create"))

			r.Post("/", precioH.Create)
			r.Put("/{id}", precioH.Update)

			r.Group(func(r chi.Router) {
				r.Use(middleware.RequirePermission(rbacStore, "price:delete"))
				r.Delete("/{id}", precioH.Delete)
			})
		})
	})

	// Sedes
	r.Route("/api/v1/sedes", func(r chi.Router) {
		r.Get("/", sedeH.List)

		r.Group(func(r chi.Router) {
			r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
			r.Use(middleware.RequireAuth())
			r.Use(middleware.RequirePermission(rbacStore, "sede:create"))
			r.Post("/", sedeH.Create)
		})
	})

	// RBAC
	r.Route("/api/v1/rbac", func(r chi.Router) {
		r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
		r.Use(middleware.RequireAuth())

		// Resources
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "rbac:resource:view"))
			r.Get("/resources", rbacH.ListResources)
			r.Get("/resources/{id}", rbacH.GetResource)
		})
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "rbac:resource:create"))
			r.Post("/resources", rbacH.CreateResource)
		})
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "rbac:resource:update"))
			r.Put("/resources/{id}", rbacH.UpdateResource)
		})
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "rbac:resource:delete"))
			r.Delete("/resources/{id}", rbacH.DeleteResource)
		})

		// Roles
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "rbac:role:view"))
			r.Get("/roles", rbacH.ListRoles)
			r.Get("/roles/{id}", rbacH.GetRole)
		})
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "rbac:role:create"))
			r.Post("/roles", rbacH.CreateRole)
		})
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "rbac:role:update"))
			r.Put("/roles/{id}", rbacH.UpdateRole)
			r.Post("/roles/{id}/resources", rbacH.AssignResourceToRole)
			r.Delete("/roles/{id}/resources/{resourceId}", rbacH.RemoveResourceFromRole)
		})
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "rbac:role:delete"))
			r.Delete("/roles/{id}", rbacH.DeleteRole)
		})

		// Profiles
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "rbac:profile:view"))
			r.Get("/profiles", rbacH.ListProfiles)
			r.Get("/profiles/{id}", rbacH.GetProfile)
		})
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "rbac:profile:create"))
			r.Post("/profiles", rbacH.CreateProfile)
		})
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "rbac:profile:update"))
			r.Put("/profiles/{id}", rbacH.UpdateProfile)
			r.Post("/profiles/{id}/roles", rbacH.AssignRoleToProfile)
			r.Delete("/profiles/{id}/roles/{roleId}", rbacH.RemoveRoleFromProfile)
		})
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "rbac:profile:delete"))
			r.Delete("/profiles/{id}", rbacH.DeleteProfile)
		})
	})

	// User profile assignment
	r.Route("/api/v1/users", func(r chi.Router) {
		r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
		r.Use(middleware.RequireAuth())
		r.Use(middleware.RequirePermission(rbacStore, "rbac:user:update"))
		r.Put("/{id}/profile", rbacH.AssignProfileToUser)
	})

	// Orders
	r.Route("/api/v1/orders", func(r chi.Router) {
		r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
		r.Use(middleware.RequireAuth())

		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "order:view"))
			r.Get("/", orderH.List)
			r.Get("/{id}", orderH.GetByID)
			r.Get("/{id}/history", orderH.GetHistory)
		})
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "order:create"))
			r.Post("/", orderH.Create)
		})
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "order:update"))
			r.Put("/{id}", orderH.Update)
		})
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "order:delete"))
			r.Delete("/{id}", orderH.Delete)
		})
		r.Group(func(r chi.Router) {
			r.Use(middleware.RequirePermission(rbacStore, "order:status:change"))
			r.Post("/{id}/status", orderH.ChangeStatus)
		})
	})

	// Sync (API Key protected)
	r.Route("/api/v1/sync", func(r chi.Router) {
		r.Use(middleware.APIKeyAuth(pool))
		r.Post("/productos", syncH.SyncProductos)
		r.Post("/combos", syncH.SyncCombos)
	})

	return r
}
