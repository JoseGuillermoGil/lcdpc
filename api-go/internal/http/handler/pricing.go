package handler

import (
	"net/http"

	"github.com/go-chi/chi/v5"
	"github.com/google/uuid"
	"github.com/lcdpc/lcdpc-go/internal/http/response"
	"github.com/lcdpc/lcdpc-go/internal/pricing"
)

type ProductoHandler struct {
	svc *pricing.Service
}

func NewProductoHandler(svc *pricing.Service) *ProductoHandler {
	return &ProductoHandler{svc: svc}
}

func (h *ProductoHandler) Create(w http.ResponseWriter, r *http.Request) {
	var req pricing.CreateProductoRequest
	if err := response.Decode(r, &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid"})
		return
	}

	result, err := h.svc.CreateProducto(r.Context(), req)
	if err != nil {
		response.Error(w, http.StatusBadRequest, err.Error())
		return
	}

	response.Created(w, result)
}

func (h *ProductoHandler) GetByID(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	result, err := h.svc.GetProductoByID(r.Context(), id)
	if err != nil {
		response.Error(w, http.StatusNotFound, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *ProductoHandler) List(w http.ResponseWriter, r *http.Request) {
	result, err := h.svc.ListProductos(r.Context())
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *ProductoHandler) Update(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	var req pricing.CreateProductoRequest
	if err := response.Decode(r, &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid"})
		return
	}

	result, err := h.svc.UpdateProducto(r.Context(), id, req)
	if err != nil {
		response.Error(w, http.StatusBadRequest, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *ProductoHandler) Delete(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	if err := h.svc.DeleteProducto(r.Context(), id); err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, map[string]string{"status": "deleted"})
}

// Combo Handler

type ComboHandler struct {
	svc *pricing.Service
}

func NewComboHandler(svc *pricing.Service) *ComboHandler {
	return &ComboHandler{svc: svc}
}

func (h *ComboHandler) Create(w http.ResponseWriter, r *http.Request) {
	var req pricing.CreateComboRequest
	if err := response.Decode(r, &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid"})
		return
	}

	result, err := h.svc.CreateCombo(r.Context(), req)
	if err != nil {
		response.Error(w, http.StatusBadRequest, err.Error())
		return
	}

	response.Created(w, result)
}

func (h *ComboHandler) GetByID(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	result, err := h.svc.GetComboByID(r.Context(), id)
	if err != nil {
		response.Error(w, http.StatusNotFound, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *ComboHandler) List(w http.ResponseWriter, r *http.Request) {
	result, err := h.svc.ListCombos(r.Context())
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *ComboHandler) Update(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	var req pricing.CreateComboRequest
	if err := response.Decode(r, &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid"})
		return
	}

	result, err := h.svc.UpdateCombo(r.Context(), id, req)
	if err != nil {
		response.Error(w, http.StatusBadRequest, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *ComboHandler) Delete(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	if err := h.svc.DeleteCombo(r.Context(), id); err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, map[string]string{"status": "deleted"})
}

func (h *ComboHandler) Publicar(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	if err := h.svc.UpdateComboEstado(r.Context(), id, "Publicado"); err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, map[string]string{"status": "Publicado"})
}

func (h *ComboHandler) Pausar(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	if err := h.svc.UpdateComboEstado(r.Context(), id, "Pausado"); err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, map[string]string{"status": "Pausado"})
}

// Precio Handler

type PrecioHandler struct {
	svc *pricing.Service
}

func NewPrecioHandler(svc *pricing.Service) *PrecioHandler {
	return &PrecioHandler{svc: svc}
}

func (h *PrecioHandler) Create(w http.ResponseWriter, r *http.Request) {
	var req pricing.CreatePrecioRequest
	if err := response.Decode(r, &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid"})
		return
	}

	result, err := h.svc.CreatePrecio(r.Context(), req)
	if err != nil {
		response.Error(w, http.StatusBadRequest, err.Error())
		return
	}

	response.Created(w, result)
}

func (h *PrecioHandler) GetByID(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	result, err := h.svc.GetPrecioByID(r.Context(), id)
	if err != nil {
		response.Error(w, http.StatusNotFound, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *PrecioHandler) ListByProductoID(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	result, err := h.svc.ListPreciosByProductoID(r.Context(), id)
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *PrecioHandler) ListBySedeID(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	result, err := h.svc.ListPreciosBySedeID(r.Context(), id)
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *PrecioHandler) Update(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	var req pricing.CreatePrecioRequest
	if err := response.Decode(r, &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid"})
		return
	}

	result, err := h.svc.UpdatePrecio(r.Context(), id, req)
	if err != nil {
		response.Error(w, http.StatusBadRequest, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *PrecioHandler) Delete(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	if err := h.svc.DeletePrecio(r.Context(), id); err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, map[string]string{"status": "deleted"})
}

func chiURLParam(r *http.Request, key string) string {
	return chi.URLParam(r, key)
}
