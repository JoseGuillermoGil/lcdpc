package handler

import (
	"net/http"

	"github.com/lcdpc/lcdpc-go/internal/http/response"
	"github.com/lcdpc/lcdpc-go/internal/sede"
)

type SedeHandler struct {
	svc *sede.Service
}

func NewSedeHandler(svc *sede.Service) *SedeHandler {
	return &SedeHandler{svc: svc}
}

func (h *SedeHandler) Create(w http.ResponseWriter, r *http.Request) {
	var req sede.CreateSedeRequest
	if err := response.Decode(r, &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid"})
		return
	}

	result, err := h.svc.Create(r.Context(), req)
	if err != nil {
		response.Error(w, http.StatusBadRequest, err.Error())
		return
	}

	response.Created(w, result)
}

func (h *SedeHandler) List(w http.ResponseWriter, r *http.Request) {
	result, err := h.svc.List(r.Context())
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, result)
}
