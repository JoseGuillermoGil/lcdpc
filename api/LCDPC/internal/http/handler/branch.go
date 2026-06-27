package handler

import (
	"net/http"

	"github.com/lcdpc/lcdpc-go/internal/branch"
	"github.com/lcdpc/lcdpc-go/internal/http/response"
)

type BranchHandler struct {
	svc *branch.Service
}

func NewBranchHandler(svc *branch.Service) *BranchHandler {
	return &BranchHandler{svc: svc}
}

func (h *BranchHandler) Create(w http.ResponseWriter, r *http.Request) {
	var req branch.CreateBranchRequest
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

func (h *BranchHandler) List(w http.ResponseWriter, r *http.Request) {
	result, err := h.svc.List(r.Context())
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, result)
}
