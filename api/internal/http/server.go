package http

import (
	"encoding/json"
	"net/http"

	"github.com/go-chi/chi/v5"
	chimw "github.com/go-chi/chi/v5/middleware"
	"github.com/jackc/pgx/v5/pgxpool"

	"github.com/lcdpc/lcdpc-go/configs"
	"github.com/lcdpc/lcdpc-go/internal/auth"
	"github.com/lcdpc/lcdpc-go/internal/branch"
	"github.com/lcdpc/lcdpc-go/internal/category"
	"github.com/lcdpc/lcdpc-go/internal/http/handler"
	"github.com/lcdpc/lcdpc-go/internal/http/middleware"
	"github.com/lcdpc/lcdpc-go/internal/order"
	"github.com/lcdpc/lcdpc-go/internal/pricing"
	"github.com/lcdpc/lcdpc-go/internal/rbac"
	"github.com/lcdpc/lcdpc-go/internal/sync"
)

func NewServer(
	cfg *configs.Config,
	pool *pgxpool.Pool,
	authSvc *auth.Service,
	oauth2Svc *auth.OAuth2Service,
	keySvc *auth.KeyService,
	pricingSvc *pricing.Service,
	branchSvc *branch.Service,
	categorySvc *category.Service,
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

	// Serve static files with cache + resize support
	staticH := handler.NewStaticHandler("static")
	r.HandleFunc("/static/*", staticH.ServeImage)

	authH := handler.NewAuthHandler(authSvc)
	oauth2H := handler.NewOAuth2Handler(oauth2Svc)
	productH := handler.NewProductHandler(pricingSvc)
	bundleH := handler.NewBundleHandler(pricingSvc)
	priceH := handler.NewPriceHandler(pricingSvc)
	branchH := handler.NewBranchHandler(branchSvc)
	categoryH := handler.NewCategoryHandler(categorySvc)
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

	// Products
	r.Route("/api/v1/products", func(r chi.Router) {
		r.Get("/", productH.List)
		r.Get("/{id}", productH.GetByID)

		r.Group(func(r chi.Router) {
			r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
			r.Use(middleware.RequireAuth())
			r.Use(middleware.RequirePermission(rbacStore, "product:create"))

			r.Post("/", productH.Create)

			r.Group(func(r chi.Router) {
				r.Use(middleware.RequirePermission(rbacStore, "product:update"))
				r.Put("/{id}", productH.Update)
				r.Put("/{id}/image", productH.UpdateImage)
			})

			r.Group(func(r chi.Router) {
				r.Use(middleware.RequirePermission(rbacStore, "product:delete"))
				r.Delete("/{id}", productH.Delete)
			})
		})
	})

	// Bundles
	r.Route("/api/v1/bundles", func(r chi.Router) {
		r.Get("/", bundleH.List)
		r.Get("/{id}", bundleH.GetByID)

		r.Group(func(r chi.Router) {
			r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
			r.Use(middleware.RequireAuth())
			r.Use(middleware.RequirePermission(rbacStore, "bundle:create"))

			r.Post("/", bundleH.Create)
			r.Post("/{id}/publish", bundleH.Publish)
			r.Post("/{id}/pause", bundleH.Pause)

			r.Group(func(r chi.Router) {
				r.Use(middleware.RequirePermission(rbacStore, "bundle:update"))
				r.Put("/{id}", bundleH.Update)
				r.Put("/{id}/image", bundleH.UpdateImage)
			})

			r.Group(func(r chi.Router) {
				r.Use(middleware.RequirePermission(rbacStore, "bundle:delete"))
				r.Delete("/{id}", bundleH.Delete)
			})
		})
	})

	// Prices
	r.Route("/api/v1/prices", func(r chi.Router) {
		r.Get("/{id}", priceH.GetByID)
		r.Get("/product/{id}", priceH.ListByProductID)
		r.Get("/branch/{id}", priceH.ListByBranchID)

		r.Group(func(r chi.Router) {
			r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
			r.Use(middleware.RequireAuth())
			r.Use(middleware.RequirePermission(rbacStore, "price:create"))

			r.Post("/", priceH.Create)
			r.Put("/{id}", priceH.Update)

			r.Group(func(r chi.Router) {
				r.Use(middleware.RequirePermission(rbacStore, "price:delete"))
				r.Delete("/{id}", priceH.Delete)
			})
		})
	})

	// Branches
	r.Route("/api/v1/branches", func(r chi.Router) {
		r.Get("/", branchH.List)

		r.Group(func(r chi.Router) {
			r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
			r.Use(middleware.RequireAuth())
			r.Use(middleware.RequirePermission(rbacStore, "branch:create"))
			r.Post("/", branchH.Create)
		})
	})

	// Categories
	r.Route("/api/v1/categories", func(r chi.Router) {
		r.Get("/", categoryH.List)
		r.Get("/{id}", categoryH.GetByID)

		r.Group(func(r chi.Router) {
			r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
			r.Use(middleware.RequireAuth())
			r.Use(middleware.RequirePermission(rbacStore, "category:create"))
			r.Post("/", categoryH.Create)
		})

		r.Group(func(r chi.Router) {
			r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
			r.Use(middleware.RequireAuth())
			r.Use(middleware.RequirePermission(rbacStore, "category:update"))
			r.Put("/{id}", categoryH.Update)
		})

		r.Group(func(r chi.Router) {
			r.Use(middleware.PASETOAuth(keySvc.Key(), cfg.OAuth2Issuer, cfg.OAuth2Audience))
			r.Use(middleware.RequireAuth())
			r.Use(middleware.RequirePermission(rbacStore, "category:delete"))
			r.Delete("/{id}", categoryH.Delete)
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
		r.Post("/products", syncH.SyncProducts)
		r.Post("/bundles", syncH.SyncBundles)
		r.Post("/products/{sku}/image", syncH.SyncProductImage)
		r.Post("/bundles/{code}/image", syncH.SyncBundleImage)
	})

	return r
}
