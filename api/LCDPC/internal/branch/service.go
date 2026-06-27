package branch

import (
	"context"
	"fmt"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
)

type Service struct {
	pool *pgxpool.Pool
}

func NewService(pool *pgxpool.Pool) *Service {
	return &Service{pool: pool}
}

type Branch struct {
	ID                    uuid.UUID  `json:"id"`
	StoreName             string     `json:"store_name"`
	TaxID                 string     `json:"tax_id"`
	Address               string     `json:"address"`
	ContactPhone          string     `json:"contact_phone"`
	SecondaryContactPhone *string    `json:"secondary_contact_phone"`
	BusinessHours         string     `json:"business_hours"`
	CreatedAtUtc           time.Time  `json:"created_at_utc"`
	UpdatedAtUtc           time.Time  `json:"updated_at_utc"`
}

type CreateBranchRequest struct {
	StoreName             string `json:"store_name" validate:"required"`
	TaxID                 string `json:"tax_id" validate:"required"`
	Address               string `json:"address" validate:"required"`
	ContactPhone          string `json:"contact_phone" validate:"required"`
	SecondaryContactPhone string `json:"secondary_contact_phone"`
	BusinessHours         string `json:"business_hours" validate:"required"`
}

func (s *Service) Create(ctx context.Context, req CreateBranchRequest) (*Branch, error) {
	branch := &Branch{}
	var secPhone *string
	if req.SecondaryContactPhone != "" {
		secPhone = &req.SecondaryContactPhone
	}

	err := s.pool.QueryRow(ctx, `
		INSERT INTO branches (id, store_name, tax_id, address, contact_phone, secondary_contact_phone, business_hours, created_at_utc, updated_at_utc)
		VALUES ($1, $2, $3, $4, $5, $6, $7, now(), now())
		RETURNING id, store_name, tax_id, address, contact_phone, secondary_contact_phone, business_hours, created_at_utc, updated_at_utc
	`, uuid.New(), req.StoreName, req.TaxID, req.Address, req.ContactPhone, secPhone, req.BusinessHours).Scan(
		&branch.ID, &branch.StoreName, &branch.TaxID, &branch.Address, &branch.ContactPhone,
		&branch.SecondaryContactPhone, &branch.BusinessHours, &branch.CreatedAtUtc, &branch.UpdatedAtUtc,
	)
	if err != nil {
		return nil, fmt.Errorf("create branch: %w", err)
	}
	return branch, nil
}

func (s *Service) List(ctx context.Context) ([]Branch, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT id, store_name, tax_id, address, contact_phone, secondary_contact_phone, business_hours, created_at_utc, updated_at_utc
		FROM branches ORDER BY store_name
	`)
	if err != nil {
		return nil, fmt.Errorf("list branches: %w", err)
	}
	defer rows.Close()

	branches := make([]Branch, 0)
	for rows.Next() {
		var b Branch
		if err := rows.Scan(&b.ID, &b.StoreName, &b.TaxID, &b.Address, &b.ContactPhone,
			&b.SecondaryContactPhone, &b.BusinessHours, &b.CreatedAtUtc, &b.UpdatedAtUtc); err != nil {
			return nil, fmt.Errorf("scan branch: %w", err)
		}
		branches = append(branches, b)
	}
	return branches, nil
}

func (s *Service) GetByID(ctx context.Context, id uuid.UUID) (*Branch, error) {
	branch := &Branch{}
	err := s.pool.QueryRow(ctx, `
		SELECT id, store_name, tax_id, address, contact_phone, secondary_contact_phone, business_hours, created_at_utc, updated_at_utc
		FROM branches WHERE id = $1
	`, id).Scan(
		&branch.ID, &branch.StoreName, &branch.TaxID, &branch.Address, &branch.ContactPhone,
		&branch.SecondaryContactPhone, &branch.BusinessHours, &branch.CreatedAtUtc, &branch.UpdatedAtUtc,
	)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get branch: %w", err)
	}
	return branch, nil
}
