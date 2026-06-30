package sync

import (
	"context"
	"fmt"

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

type SyncProductRequest struct {
	ProductID               uuid.UUID  `json:"product_id"`
	Name                    string     `json:"name"`
	Sku                     string     `json:"sku"`
	WholesaleCommercialType string     `json:"wholesale_commercial_type"`
	IsActive                bool       `json:"is_active"`
	BaseUnitID              *uuid.UUID `json:"base_unit_id"`
	Stock                   *int       `json:"stock"`
	StockAvailable          *int       `json:"stock_available"`
	StockBlocked            *int       `json:"stock_blocked"`
}

type SyncBundleRequest struct {
	BundleID                 uuid.UUID        `json:"bundle_id"`
	Code                     string           `json:"code"`
	Name                     string           `json:"name"`
	Status                   string           `json:"status"`
	BranchID                 *uuid.UUID       `json:"branch_id"`
	TotalPrice               float64          `json:"total_price"`
	TotalPriceCurrency       string           `json:"total_price_currency"`
	PromotionalPrice         *float64         `json:"promotional_price"`
	PromotionalPriceCurrency *string          `json:"promotional_price_currency"`
	Items                    []SyncBundleItem `json:"items"`
	Stock                    *int             `json:"stock"`
	StockAvailable           *int             `json:"stock_available"`
	StockBlocked             *int             `json:"stock_blocked"`
}

type SyncBundleItem struct {
	ProductID uuid.UUID `json:"product_id"`
	Quantity  float64   `json:"quantity"`
}

type SyncResult struct {
	Processed int `json:"processed"`
	Errors    int `json:"errors"`
}

func (s *Service) SyncProducts(ctx context.Context, products []SyncProductRequest) (*SyncResult, error) {
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	result := &SyncResult{}
	for _, p := range products {
		_, err := tx.Exec(ctx, `
			INSERT INTO products (product_id, name, sku, wholesale_commercial_type, is_active, base_unit_id, stock, stock_available, stock_blocked)
			VALUES ($1, $2, $3, $4, $5, $6, COALESCE($7, 0), COALESCE($8, 0), COALESCE($9, 0))
			ON CONFLICT (sku) DO UPDATE SET
				name = EXCLUDED.name,
				wholesale_commercial_type = EXCLUDED.wholesale_commercial_type,
				is_active = EXCLUDED.is_active,
				base_unit_id = EXCLUDED.base_unit_id,
				stock = EXCLUDED.stock,
				stock_available = EXCLUDED.stock_available,
				stock_blocked = EXCLUDED.stock_blocked
		`, p.ProductID, p.Name, p.Sku, p.WholesaleCommercialType, p.IsActive, p.BaseUnitID, p.Stock, p.StockAvailable, p.StockBlocked)
		if err != nil {
			result.Errors++
			continue
		}
		result.Processed++
	}

	if err := tx.Commit(ctx); err != nil {
		return nil, fmt.Errorf("commit: %w", err)
	}

	return result, nil
}

func (s *Service) SyncBundles(ctx context.Context, bundles []SyncBundleRequest) (*SyncResult, error) {
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	result := &SyncResult{}
	for _, b := range bundles {
		_, err := tx.Exec(ctx, `
			INSERT INTO bundles (bundle_id, code, name, status, branch_id, total_price, total_price_currency, promotional_price, promotional_price_currency, stock, stock_available, stock_blocked)
			VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, COALESCE($10, 0), COALESCE($11, 0), COALESCE($12, 0))
			ON CONFLICT (code) DO UPDATE SET
				name = EXCLUDED.name,
				status = EXCLUDED.status,
				branch_id = EXCLUDED.branch_id,
				total_price = EXCLUDED.total_price,
				total_price_currency = EXCLUDED.total_price_currency,
				promotional_price = EXCLUDED.promotional_price,
				promotional_price_currency = EXCLUDED.promotional_price_currency,
				stock = EXCLUDED.stock,
				stock_available = EXCLUDED.stock_available,
				stock_blocked = EXCLUDED.stock_blocked
		`, b.BundleID, b.Code, b.Name, b.Status, b.BranchID,
			b.TotalPrice, b.TotalPriceCurrency, b.PromotionalPrice, b.PromotionalPriceCurrency, b.Stock, b.StockAvailable, b.StockBlocked)
		if err != nil {
			result.Errors++
			continue
		}

		// Replace bundle items
		tx.Exec(ctx, `DELETE FROM bundle_items WHERE bundle_id = $1`, b.BundleID)
		for _, item := range b.Items {
			_, err := tx.Exec(ctx, `
				INSERT INTO bundle_items (id, bundle_id, product_id, quantity)
				VALUES ($1, $2, $3, $4)
			`, uuid.New(), b.BundleID, item.ProductID, item.Quantity)
			if err != nil {
				result.Errors++
			}
		}

		result.Processed++
	}

	if err := tx.Commit(ctx); err != nil {
		return nil, fmt.Errorf("commit: %w", err)
	}

	return result, nil
}

func (s *Service) UpdateProductImage(ctx context.Context, id uuid.UUID, img string) (string, error) {
	var oldImg *string
	err := s.pool.QueryRow(ctx, `SELECT img FROM products WHERE product_id = $1`, id).Scan(&oldImg)
	if err == pgx.ErrNoRows {
		return "", fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return "", fmt.Errorf("get product: %w", err)
	}

	var newImg interface{}
	if img != "" {
		newImg = img
	}
	_, err = s.pool.Exec(ctx, `UPDATE products SET img = $2 WHERE product_id = $1`, id, newImg)
	if err != nil {
		return "", fmt.Errorf("update product image: %w", err)
	}

	if oldImg != nil && *oldImg != "" {
		return *oldImg, nil
	}
	return "", nil
}

func (s *Service) UpdateBundleImage(ctx context.Context, id uuid.UUID, img string) (string, error) {
	var oldImg *string
	err := s.pool.QueryRow(ctx, `SELECT img FROM bundles WHERE bundle_id = $1`, id).Scan(&oldImg)
	if err == pgx.ErrNoRows {
		return "", fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return "", fmt.Errorf("get bundle: %w", err)
	}

	var newImg interface{}
	if img != "" {
		newImg = img
	}
	_, err = s.pool.Exec(ctx, `UPDATE bundles SET img = $2 WHERE bundle_id = $1`, id, newImg)
	if err != nil {
		return "", fmt.Errorf("update bundle image: %w", err)
	}

	if oldImg != nil && *oldImg != "" {
		return *oldImg, nil
	}
	return "", nil
}

func (s *Service) UpdateProductImageBySKU(ctx context.Context, sku string, img string) (string, error) {
	var oldImg *string
	err := s.pool.QueryRow(ctx, `SELECT img FROM products WHERE sku = $1`, sku).Scan(&oldImg)
	if err == pgx.ErrNoRows {
		return "", fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return "", fmt.Errorf("get product: %w", err)
	}

	var newImg interface{}
	if img != "" {
		newImg = img
	}
	_, err = s.pool.Exec(ctx, `UPDATE products SET img = $2 WHERE sku = $1`, sku, newImg)
	if err != nil {
		return "", fmt.Errorf("update product image: %w", err)
	}

	if oldImg != nil && *oldImg != "" {
		return *oldImg, nil
	}
	return "", nil
}

func (s *Service) UpdateBundleImageByCode(ctx context.Context, code string, img string) (string, error) {
	var oldImg *string
	err := s.pool.QueryRow(ctx, `SELECT img FROM bundles WHERE code = $1`, code).Scan(&oldImg)
	if err == pgx.ErrNoRows {
		return "", fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return "", fmt.Errorf("get bundle: %w", err)
	}

	var newImg interface{}
	if img != "" {
		newImg = img
	}
	_, err = s.pool.Exec(ctx, `UPDATE bundles SET img = $2 WHERE code = $1`, code, newImg)
	if err != nil {
		return "", fmt.Errorf("update bundle image: %w", err)
	}

	if oldImg != nil && *oldImg != "" {
		return *oldImg, nil
	}
	return "", nil
}
