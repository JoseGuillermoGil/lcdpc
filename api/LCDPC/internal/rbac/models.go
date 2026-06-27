package rbac

import (
	"time"

	"github.com/google/uuid"
)

type ResourceResponse struct {
	ID   uuid.UUID `json:"id"`
	Code string    `json:"code"`
}

type CreateResourceRequest struct {
	Code string `json:"code"`
}

type UpdateResourceRequest struct {
	Code string `json:"code"`
}

type RoleResponse struct {
	ID          uuid.UUID       `json:"id"`
	Code        string          `json:"code"`
	Name        string          `json:"name"`
	Description string          `json:"description"`
	Resources   []ResourceEntry `json:"resources"`
}

type CreateRoleRequest struct {
	Code        string `json:"code"`
	Name        string `json:"name"`
	Description string `json:"description"`
}

type UpdateRoleRequest struct {
	Code        string `json:"code"`
	Name        string `json:"name"`
	Description string `json:"description"`
}

type AssignResourceRequest struct {
	ResourceID uuid.UUID `json:"resource_id"`
}

type ProfileResponse struct {
	ID               uuid.UUID   `json:"id"`
	UserID           *uuid.UUID  `json:"user_id,omitempty"`
	FirstName        string      `json:"first_name"`
	LastName         string      `json:"last_name"`
	IdentityDocument string      `json:"identity_document"`
	TaxID            *string     `json:"tax_id"`
	WhatsAppPhone    string      `json:"whatsapp_phone"`
	FullAddress      string      `json:"full_address"`
	Roles            []RoleEntry `json:"roles"`
	CreatedAt        time.Time   `json:"created_at_utc"`
	UpdatedAt        time.Time   `json:"updated_at_utc"`
}

type CreateProfileRequest struct {
	FirstName        string  `json:"first_name"`
	LastName         string  `json:"last_name"`
	IdentityDocument string  `json:"identity_document"`
	TaxID            *string `json:"tax_id"`
	WhatsAppPhone    string  `json:"whatsapp_phone"`
	FullAddress      string  `json:"full_address"`
}

type UpdateProfileRequest struct {
	FirstName        string  `json:"first_name"`
	LastName         string  `json:"last_name"`
	IdentityDocument string  `json:"identity_document"`
	TaxID            *string `json:"tax_id"`
	WhatsAppPhone    string  `json:"whatsapp_phone"`
	FullAddress      string  `json:"full_address"`
}

type AssignRoleRequest struct {
	RoleID uuid.UUID `json:"role_id"`
}

type AssignProfileRequest struct {
	ProfileID uuid.UUID `json:"profile_id"`
}

type RoleEntry struct {
	ID   uuid.UUID `json:"id"`
	Code string    `json:"code"`
	Name string    `json:"name"`
}

type ResourceEntry struct {
	ID   uuid.UUID `json:"id"`
	Code string    `json:"code"`
}
