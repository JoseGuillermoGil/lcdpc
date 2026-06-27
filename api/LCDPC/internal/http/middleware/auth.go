package middleware

import (
	"context"
	"net/http"
	"strings"

	"github.com/lcdpc/lcdpc-go/internal/auth"
)

type contextKey string

const (
	UserIDKey   contextKey = "user_id"
	UserRolesKey contextKey = "user_roles"
	TokenKey    contextKey = "access_token"
)

func PASETOAuth(key []byte, issuer, audience string) func(http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			tokenString := extractToken(r)
			if tokenString == "" {
				next.ServeHTTP(w, r)
				return
			}

			claims, err := auth.ValidatePasetoToken(tokenString, key, issuer, audience)
			if err != nil {
				next.ServeHTTP(w, r)
				return
			}

			ctx := context.WithValue(r.Context(), UserIDKey, claims.Sub)
			ctx = context.WithValue(ctx, UserRolesKey, claims.Roles)
			ctx = context.WithValue(ctx, TokenKey, tokenString)

			next.ServeHTTP(w, r.WithContext(ctx))
		})
	}
}

func RequireAuth() func(http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			userID := r.Context().Value(UserIDKey)
			if userID == nil || userID.(string) == "" {
				http.Error(w, `{"status":"error","message":"unauthorized"}`, http.StatusUnauthorized)
				return
			}
			next.ServeHTTP(w, r)
		})
	}
}

func RequireRoles(roles ...string) func(http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			userRoles, _ := r.Context().Value(UserRolesKey).([]string)

			for _, required := range roles {
				for _, user := range userRoles {
					if user == required {
						next.ServeHTTP(w, r)
						return
					}
				}
			}

			http.Error(w, `{"status":"error","message":"insufficient_permissions"}`, http.StatusForbidden)
		})
	}
}

func extractToken(r *http.Request) string {
	auth := r.Header.Get("Authorization")
	if strings.HasPrefix(auth, "Bearer ") {
		return strings.TrimPrefix(auth, "Bearer ")
	}

	cookie, err := r.Cookie("lcdpc_at")
	if err == nil && cookie.Value != "" {
		return cookie.Value
	}

	return ""
}

func GetUserID(ctx context.Context) string {
	if v, ok := ctx.Value(UserIDKey).(string); ok {
		return v
	}
	return ""
}

func GetUserRoles(ctx context.Context) []string {
	if v, ok := ctx.Value(UserRolesKey).([]string); ok {
		return v
	}
	return nil
}

func GetAccessToken(ctx context.Context) string {
	if v, ok := ctx.Value(TokenKey).(string); ok {
		return v
	}
	return ""
}
