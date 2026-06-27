package handler

import (
	"net/http"

	"github.com/google/uuid"
	"github.com/lcdpc/lcdpc-go/internal/auth"
	"github.com/lcdpc/lcdpc-go/internal/http/middleware"
	"github.com/lcdpc/lcdpc-go/internal/http/response"
)

type OAuth2Handler struct {
	oauth2Svc *auth.OAuth2Service
}

func NewOAuth2Handler(oauth2Svc *auth.OAuth2Service) *OAuth2Handler {
	return &OAuth2Handler{oauth2Svc: oauth2Svc}
}

func (h *OAuth2Handler) Authorize(w http.ResponseWriter, r *http.Request) {
	q := r.URL.Query()

	clientID := q.Get("client_id")
	redirectURI := q.Get("redirect_uri")

	if clientID == "" {
		response.Fail(w, http.StatusBadRequest, map[string]string{"error": "invalid_request", "error_description": "client_id is required"})
		return
	}
	if redirectURI == "" {
		response.Fail(w, http.StatusBadRequest, map[string]string{"error": "invalid_request", "error_description": "redirect_uri is required"})
		return
	}

	// Resolve user from JWT/cookie
	var userID *uuid.UUID
	if uidStr := middleware.GetUserID(r.Context()); uidStr != "" {
		if parsed, err := uuid.Parse(uidStr); err == nil {
			userID = &parsed
		}
	}

	result, err := h.oauth2Svc.Authorize(r.Context(), auth.AuthorizeRequest{
		ClientID:            clientID,
		RedirectURI:         redirectURI,
		ResponseType:        q.Get("response_type"),
		Scope:               q.Get("scope"),
		State:               q.Get("state"),
		CodeChallenge:       q.Get("code_challenge"),
		CodeChallengeMethod: q.Get("code_challenge_method"),
	}, userID)
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	if result.Success && result.RedirectURL != "" {
		http.Redirect(w, r, result.RedirectURL, http.StatusFound)
		return
	}

	if result.RedirectURL != "" {
		http.Redirect(w, r, result.RedirectURL, http.StatusFound)
		return
	}

	response.Fail(w, http.StatusBadRequest, map[string]string{
		"error":             result.ErrorCode,
		"error_description": result.ErrorDescription,
	})
}

func (h *OAuth2Handler) Token(w http.ResponseWriter, r *http.Request) {
	if err := r.ParseForm(); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"error": "invalid_request", "error_description": "invalid form data"})
		return
	}

	grantType := r.FormValue("grant_type")
	clientID := r.FormValue("client_id")

	if grantType == "" {
		response.Fail(w, http.StatusBadRequest, map[string]string{"error": "invalid_request", "error_description": "grant_type is required"})
		return
	}
	if clientID == "" {
		response.Fail(w, http.StatusBadRequest, map[string]string{"error": "invalid_client", "error_description": "client_id is required"})
		return
	}

	switch grantType {
	case "authorization_code":
		code := r.FormValue("code")
		redirectURI := r.FormValue("redirect_uri")
		codeVerifier := r.FormValue("code_verifier")

		if code == "" || redirectURI == "" || codeVerifier == "" {
			response.Fail(w, http.StatusBadRequest, map[string]string{"error": "invalid_request", "error_description": "code, redirect_uri, and code_verifier are required"})
			return
		}

		result, errResp := h.oauth2Svc.ExchangeCode(r.Context(), code, codeVerifier, redirectURI, clientID)
		if errResp != nil {
			response.Fail(w, http.StatusBadRequest, errResp)
			return
		}

		w.Header().Set("Content-Type", "application/json")
		w.Header().Set("Cache-Control", "no-store")
		w.Header().Set("Pragma", "no-cache")
		response.Success(w, result)

	case "refresh_token":
		refreshToken := r.FormValue("refresh_token")
		if refreshToken == "" {
			response.Fail(w, http.StatusBadRequest, map[string]string{"error": "invalid_request", "error_description": "refresh_token is required"})
			return
		}

		result, errResp := h.oauth2Svc.RefreshToken(r.Context(), refreshToken, clientID)
		if errResp != nil {
			response.Fail(w, http.StatusBadRequest, errResp)
			return
		}

		w.Header().Set("Content-Type", "application/json")
		w.Header().Set("Cache-Control", "no-store")
		w.Header().Set("Pragma", "no-cache")
		response.Success(w, result)

	default:
		response.Fail(w, http.StatusBadRequest, map[string]string{
			"error":             "unsupported_grant_type",
			"error_description": "Supported grant types: authorization_code, refresh_token",
		})
	}
}

func (h *OAuth2Handler) Introspect(w http.ResponseWriter, r *http.Request) {
	if err := r.ParseForm(); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"error": "invalid_request", "error_description": "invalid form data"})
		return
	}

	token := r.FormValue("token")
	if token == "" {
		response.Fail(w, http.StatusBadRequest, map[string]string{"error": "invalid_request", "error_description": "token is required"})
		return
	}

	result, err := h.oauth2Svc.Introspect(r.Context(), token)
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	w.Header().Set("Content-Type", "application/json")
	w.Header().Set("Cache-Control", "no-store")
	response.Success(w, result)
}

func (h *OAuth2Handler) Revoke(w http.ResponseWriter, r *http.Request) {
	if err := r.ParseForm(); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"error": "invalid_request", "error_description": "invalid form data"})
		return
	}

	token := r.FormValue("token")
	if token == "" {
		response.Fail(w, http.StatusBadRequest, map[string]string{"error": "invalid_request", "error_description": "token is required"})
		return
	}

	// RFC 7009: always return 200
	h.oauth2Svc.Revoke(r.Context(), token)

	w.Header().Set("Content-Type", "application/json")
	response.Success(w, map[string]string{})
}
