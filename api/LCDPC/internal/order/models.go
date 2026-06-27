package order

import (
	"time"

	"github.com/google/uuid"
)

type Order struct {
	ID           uuid.UUID     `json:"id"`
	BranchID     uuid.UUID     `json:"branch_id"`
	ClientUserID uuid.UUID     `json:"client_user_id"`
	Status       string        `json:"status"`
	PriceTotal   float64       `json:"price_total"`
	TotalItems   int           `json:"total_items"`
	Currency     string        `json:"currency"`
	Notes        *string       `json:"notes"`
	CreatedAtUtc  time.Time    `json:"created_at_utc"`
	UpdatedAtUtc  time.Time    `json:"updated_at_utc"`
	Items        []OrderItem   `json:"items,omitempty"`
}

type OrderItem struct {
	ID          uuid.UUID  `json:"id"`
	OrderID     uuid.UUID  `json:"order_id"`
	ItemType    string     `json:"item_type"`
	ProductID   *uuid.UUID `json:"product_id"`
	BundleID    *uuid.UUID `json:"bundle_id"`
	Quantity    float64    `json:"quantity"`
	UnitPrice   float64    `json:"unit_price"`
	Subtotal    float64    `json:"subtotal"`
	Currency    string     `json:"currency"`
}

type CreateOrderRequest struct {
	BranchID     uuid.UUID          `json:"branch_id"`
	ClientUserID uuid.UUID          `json:"client_user_id"`
	Notes        string             `json:"notes"`
	Items        []CreateOrderItem  `json:"items"`
}

type CreateOrderItem struct {
	ItemType   string    `json:"item_type"`
	ProductID  uuid.UUID `json:"product_id"`
	BundleID   uuid.UUID `json:"bundle_id"`
	Quantity   float64   `json:"quantity"`
	UnitPrice  float64   `json:"unit_price"`
}

type UpdateOrderRequest struct {
	Notes string            `json:"notes"`
	Items []CreateOrderItem `json:"items"`
}

type StatusChangeRequest struct {
	ToStatus string `json:"to_status"`
	Notes    string `json:"notes"`
}

type StatusHistoryEntry struct {
	ID               uuid.UUID `json:"id"`
	OrderID          uuid.UUID `json:"order_id"`
	FromStatus       *string   `json:"from_status"`
	ToStatus         string    `json:"to_status"`
	ChangedByUserID  uuid.UUID `json:"changed_by_user_id"`
	Notes            *string   `json:"notes"`
	CreatedAtUtc     time.Time `json:"created_at_utc"`
}

type OrderFilter struct {
	BranchID     *uuid.UUID
	ClientUserID *uuid.UUID
	Status       *string
	Limit        *int
	Offset       *int
}
