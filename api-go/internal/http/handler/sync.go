package handler

import (
	"net/http"

	"github.com/lcdpc/lcdpc-go/internal/http/response"
	"github.com/lcdpc/lcdpc-go/internal/sync"
)

type SyncHandler struct {
	svc *sync.Service
}

func NewSyncHandler(svc *sync.Service) *SyncHandler {
	return &SyncHandler{svc: svc}
}

func (h *SyncHandler) SyncProductos(w http.ResponseWriter, r *http.Request) {
	var req []sync.SyncProductoRequest
	if err := response.Decode(r, &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid"})
		return
	}

	result, err := h.svc.SyncProductos(r.Context(), req)
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *SyncHandler) SyncCombos(w http.ResponseWriter, r *http.Request) {
	var req []sync.SyncComboRequest
	if err := response.Decode(r, &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid"})
		return
	}

	result, err := h.svc.SyncCombos(r.Context(), req)
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, result)
}
