package handler

import (
	"bytes"
	"encoding/json"
	"fmt"
	"image"
	"image/gif"
	"image/jpeg"
	"image/png"
	"io"
	"mime/multipart"
	"net/http"
	"os"
	"path/filepath"
	"strings"

	"github.com/go-chi/chi/v5"
	"github.com/google/uuid"
	"github.com/lcdpc/lcdpc-go/internal/http/response"
	"github.com/lcdpc/lcdpc-go/internal/pricing"
	"github.com/nfnt/resize"
)

const (
	maxUploadSize = 5 << 20 // 5MB
	staticImgDir  = "static/img"
)

var allowedMimeTypes = map[string]string{
	"image/jpeg": ".jpg",
	"image/png":  ".png",
	"image/webp": ".webp",
	"image/gif":  ".gif",
}

// Product Handler

type ProductHandler struct {
	svc *pricing.Service
}

func NewProductHandler(svc *pricing.Service) *ProductHandler {
	return &ProductHandler{svc: svc}
}

func (h *ProductHandler) Create(w http.ResponseWriter, r *http.Request) {
	r.Body = http.MaxBytesReader(w, r.Body, maxUploadSize)

	if err := r.ParseMultipartForm(maxUploadSize); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid or too large"})
		return
	}

	dataStr := r.FormValue("data")
	if dataStr == "" {
		response.Fail(w, http.StatusBadRequest, map[string]string{"data": "required"})
		return
	}

	var req pricing.CreateProductRequest
	if err := json.Unmarshal([]byte(dataStr), &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"data": "invalid json"})
		return
	}

	file, header, err := r.FormFile("file")
	if err == nil {
		defer file.Close()
		ext, err := validateImageFile(header)
		if err != nil {
			response.Fail(w, http.StatusBadRequest, map[string]string{"file": err.Error()})
			return
		}
		imgPath, err := saveUploadedFile(file, ext)
		if err != nil {
			response.Error(w, http.StatusInternalServerError, "failed to save image")
			return
		}
		req.Img = &imgPath
	}

	result, err := h.svc.CreateProduct(r.Context(), req)
	if err != nil {
		response.Error(w, http.StatusBadRequest, err.Error())
		return
	}

	response.Created(w, result)
}

func (h *ProductHandler) GetByID(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	result, err := h.svc.GetProductByID(r.Context(), id)
	if err != nil {
		response.Error(w, http.StatusNotFound, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *ProductHandler) List(w http.ResponseWriter, r *http.Request) {
	result, err := h.svc.ListProducts(r.Context())
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *ProductHandler) Update(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	r.Body = http.MaxBytesReader(w, r.Body, maxUploadSize)

	if err := r.ParseMultipartForm(maxUploadSize); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid or too large"})
		return
	}

	dataStr := r.FormValue("data")
	if dataStr == "" {
		response.Fail(w, http.StatusBadRequest, map[string]string{"data": "required"})
		return
	}

	var req pricing.CreateProductRequest
	if err := json.Unmarshal([]byte(dataStr), &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"data": "invalid json"})
		return
	}

	file, header, err := r.FormFile("file")
	if err == nil {
		defer file.Close()
		ext, err := validateImageFile(header)
		if err != nil {
			response.Fail(w, http.StatusBadRequest, map[string]string{"file": err.Error()})
			return
		}
		imgPath, err := saveUploadedFile(file, ext)
		if err != nil {
			response.Error(w, http.StatusInternalServerError, "failed to save image")
			return
		}
		req.Img = &imgPath
	}

	result, err := h.svc.UpdateProduct(r.Context(), id, req)
	if err != nil {
		response.Error(w, http.StatusBadRequest, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *ProductHandler) Delete(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	if err := h.svc.DeleteProduct(r.Context(), id); err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, map[string]string{"status": "deleted"})
}

func (h *ProductHandler) UpdateImage(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	r.Body = http.MaxBytesReader(w, r.Body, maxUploadSize)

	if err := r.ParseMultipartForm(maxUploadSize); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"file": "invalid or too large"})
		return
	}

	file, header, err := r.FormFile("file")
	if err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"file": "required"})
		return
	}
	defer file.Close()

	ext, err := validateImageFile(header)
	if err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"file": err.Error()})
		return
	}

	oldImg, err := h.svc.UpdateProductImage(r.Context(), id, "")
	if err != nil {
		response.Error(w, http.StatusNotFound, err.Error())
		return
	}

	var imgPath string
	if oldImg != "" {
		imgPath, err = replaceExistingFile(oldImg, file)
		if err != nil {
			response.Error(w, http.StatusInternalServerError, "failed to save image")
			return
		}
	} else {
		imgPath, err = saveUploadedFile(file, ext)
		if err != nil {
			response.Error(w, http.StatusInternalServerError, "failed to save image")
			return
		}
	}

	_, err = h.svc.UpdateProductImage(r.Context(), id, imgPath)
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, map[string]string{"img": imgPath})
}

// Bundle Handler

type BundleHandler struct {
	svc *pricing.Service
}

func NewBundleHandler(svc *pricing.Service) *BundleHandler {
	return &BundleHandler{svc: svc}
}

func (h *BundleHandler) Create(w http.ResponseWriter, r *http.Request) {
	r.Body = http.MaxBytesReader(w, r.Body, maxUploadSize)

	if err := r.ParseMultipartForm(maxUploadSize); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid or too large"})
		return
	}

	dataStr := r.FormValue("data")
	if dataStr == "" {
		response.Fail(w, http.StatusBadRequest, map[string]string{"data": "required"})
		return
	}

	var req pricing.CreateBundleRequest
	if err := json.Unmarshal([]byte(dataStr), &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"data": "invalid json"})
		return
	}

	file, header, err := r.FormFile("file")
	if err == nil {
		defer file.Close()
		ext, err := validateImageFile(header)
		if err != nil {
			response.Fail(w, http.StatusBadRequest, map[string]string{"file": err.Error()})
			return
		}
		imgPath, err := saveUploadedFile(file, ext)
		if err != nil {
			response.Error(w, http.StatusInternalServerError, "failed to save image")
			return
		}
		req.Img = &imgPath
	}

	result, err := h.svc.CreateBundle(r.Context(), req)
	if err != nil {
		response.Error(w, http.StatusBadRequest, err.Error())
		return
	}

	response.Created(w, result)
}

func (h *BundleHandler) GetByID(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	result, err := h.svc.GetBundleByID(r.Context(), id)
	if err != nil {
		response.Error(w, http.StatusNotFound, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *BundleHandler) List(w http.ResponseWriter, r *http.Request) {
	result, err := h.svc.ListBundles(r.Context())
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *BundleHandler) Update(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	r.Body = http.MaxBytesReader(w, r.Body, maxUploadSize)

	if err := r.ParseMultipartForm(maxUploadSize); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid or too large"})
		return
	}

	dataStr := r.FormValue("data")
	if dataStr == "" {
		response.Fail(w, http.StatusBadRequest, map[string]string{"data": "required"})
		return
	}

	var req pricing.CreateBundleRequest
	if err := json.Unmarshal([]byte(dataStr), &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"data": "invalid json"})
		return
	}

	file, header, err := r.FormFile("file")
	if err == nil {
		defer file.Close()
		ext, err := validateImageFile(header)
		if err != nil {
			response.Fail(w, http.StatusBadRequest, map[string]string{"file": err.Error()})
			return
		}
		imgPath, err := saveUploadedFile(file, ext)
		if err != nil {
			response.Error(w, http.StatusInternalServerError, "failed to save image")
			return
		}
		req.Img = &imgPath
	}

	result, err := h.svc.UpdateBundle(r.Context(), id, req)
	if err != nil {
		response.Error(w, http.StatusBadRequest, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *BundleHandler) Delete(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	if err := h.svc.DeleteBundle(r.Context(), id); err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, map[string]string{"status": "deleted"})
}

func (h *BundleHandler) Publish(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	if err := h.svc.UpdateBundleStatus(r.Context(), id, "Published"); err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, map[string]string{"status": "Published"})
}

func (h *BundleHandler) Pause(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	if err := h.svc.UpdateBundleStatus(r.Context(), id, "Paused"); err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, map[string]string{"status": "Paused"})
}

func (h *BundleHandler) UpdateImage(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	r.Body = http.MaxBytesReader(w, r.Body, maxUploadSize)

	if err := r.ParseMultipartForm(maxUploadSize); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"file": "invalid or too large"})
		return
	}

	file, header, err := r.FormFile("file")
	if err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"file": "required"})
		return
	}
	defer file.Close()

	ext, err := validateImageFile(header)
	if err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"file": err.Error()})
		return
	}

	oldImg, err := h.svc.UpdateBundleImage(r.Context(), id, "")
	if err != nil {
		response.Error(w, http.StatusNotFound, err.Error())
		return
	}

	var imgPath string
	if oldImg != "" {
		imgPath, err = replaceExistingFile(oldImg, file)
		if err != nil {
			response.Error(w, http.StatusInternalServerError, "failed to save image")
			return
		}
	} else {
		imgPath, err = saveUploadedFile(file, ext)
		if err != nil {
			response.Error(w, http.StatusInternalServerError, "failed to save image")
			return
		}
	}

	_, err = h.svc.UpdateBundleImage(r.Context(), id, imgPath)
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, map[string]string{"img": imgPath})
}

// Price Handler

type PriceHandler struct {
	svc *pricing.Service
}

func NewPriceHandler(svc *pricing.Service) *PriceHandler {
	return &PriceHandler{svc: svc}
}

func (h *PriceHandler) Create(w http.ResponseWriter, r *http.Request) {
	var req pricing.CreatePriceRequest
	if err := response.Decode(r, &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid"})
		return
	}

	result, err := h.svc.CreatePrice(r.Context(), req)
	if err != nil {
		response.Error(w, http.StatusBadRequest, err.Error())
		return
	}

	response.Created(w, result)
}

func (h *PriceHandler) GetByID(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	result, err := h.svc.GetPriceByID(r.Context(), id)
	if err != nil {
		response.Error(w, http.StatusNotFound, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *PriceHandler) ListByProductID(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	result, err := h.svc.ListPricesByProductID(r.Context(), id)
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *PriceHandler) ListByBranchID(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	result, err := h.svc.ListPricesByBranchID(r.Context(), id)
	if err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *PriceHandler) Update(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	var req pricing.CreatePriceRequest
	if err := response.Decode(r, &req); err != nil {
		response.Fail(w, http.StatusBadRequest, map[string]string{"body": "invalid"})
		return
	}

	result, err := h.svc.UpdatePrice(r.Context(), id, req)
	if err != nil {
		response.Error(w, http.StatusBadRequest, err.Error())
		return
	}

	response.Success(w, result)
}

func (h *PriceHandler) Delete(w http.ResponseWriter, r *http.Request) {
	idStr := chiURLParam(r, "id")
	id, err := uuid.Parse(idStr)
	if err != nil {
		response.Error(w, http.StatusBadRequest, "invalid id")
		return
	}

	if err := h.svc.DeletePrice(r.Context(), id); err != nil {
		response.Error(w, http.StatusInternalServerError, err.Error())
		return
	}

	response.Success(w, map[string]string{"status": "deleted"})
}

// Helpers

func chiURLParam(r *http.Request, key string) string {
	return chi.URLParam(r, key)
}

func validateImageFile(header *multipart.FileHeader) (string, error) {
	ext := strings.ToLower(filepath.Ext(header.Filename))
	mimeType := header.Header.Get("Content-Type")

	expectedExt, ok := allowedMimeTypes[mimeType]
	if !ok {
		return "", fmt.Errorf("unsupported file type: %s", mimeType)
	}
	if ext != expectedExt && ext != ".jpeg" {
		return "", fmt.Errorf("file extension %s does not match content type %s", ext, mimeType)
	}
	if ext == ".jpeg" {
		ext = ".jpg"
	}

	return ext, nil
}

func saveUploadedFile(file multipart.File, ext string) (string, error) {
	filename := uuid.New().String() + ext
	path := filepath.Join(staticImgDir, filename)

	// Decode the image
	file.Seek(0, 0)
	var img image.Image
	var err error

	switch ext {
	case ".jpg", ".jpeg":
		img, err = jpeg.Decode(file)
	case ".png":
		img, err = png.Decode(file)
	case ".gif":
		img, err = gif.Decode(file)
	default:
		// For unsupported formats, save as-is
		file.Seek(0, 0)
		out, err := os.Create(path)
		if err != nil {
			return "", fmt.Errorf("create file: %w", err)
		}
		defer out.Close()
		if _, err := io.Copy(out, file); err != nil {
			return "", fmt.Errorf("write file: %w", err)
		}
		return "/static/img/" + filename, nil
	}

	if err != nil {
		return "", fmt.Errorf("decode image: %w", err)
	}

	// Resize if larger than 1200px width
	bounds := img.Bounds()
	if bounds.Dx() > 1200 {
		img = resize.Resize(1200, 0, img, resize.Lanczos3)
	}

	// Encode with compression
	var buf bytes.Buffer
	switch ext {
	case ".jpg", ".jpeg":
		err = jpeg.Encode(&buf, img, &jpeg.Options{Quality: 80})
	case ".png":
		encoder := png.Encoder{CompressionLevel: png.BestCompression}
		err = encoder.Encode(&buf, img)
	case ".gif":
		err = gif.Encode(&buf, img, nil)
	}
	if err != nil {
		return "", fmt.Errorf("encode image: %w", err)
	}

	// Write to disk
	if err := os.WriteFile(path, buf.Bytes(), 0644); err != nil {
		return "", fmt.Errorf("write file: %w", err)
	}

	return "/static/img/" + filename, nil
}

func replaceExistingFile(oldImg string, file multipart.File) (string, error) {
	filename := strings.TrimPrefix(oldImg, "/static/img/")
	imgPath := "/static/img/" + filename

	if err := os.Remove(filepath.Join(staticImgDir, filename)); err != nil && !os.IsNotExist(err) {
		// Log but don't fail
	}

	fileOnDisk, err := os.Create(filepath.Join(staticImgDir, filename))
	if err != nil {
		return "", fmt.Errorf("create file: %w", err)
	}
	defer fileOnDisk.Close()

	if _, err := io.Copy(fileOnDisk, file); err != nil {
		return "", fmt.Errorf("write file: %w", err)
	}

	return imgPath, nil
}
