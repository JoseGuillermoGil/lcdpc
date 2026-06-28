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
	ProductID            uuid.UUID `json:"product_id"`
	Name                 string    `json:"name"`
	Sku                  string    `json:"sku"`
	BaseMeasureType      string    `json:"base_measure_type"`
	WholesaleCommercialType string `json:"wholesale_commercial_type"`
	UnitsPerBox          *int      `json:"units_per_box"`
	UnitsPerBundle       *int      `json:"units_per_bundle"`
	IsActive             bool      `json:"is_active"`
}

type SyncBundleRequest struct {
	BundleID                uuid.UUID      `json:"bundle_id"`
	Code                    string         `json:"code"`
	Name                    string         `json:"name"`
	Status                  string         `json:"status"`
	EnabledBranchIDs        []uuid.UUID    `json:"enabled_branch_ids"`
	TotalPrice              float64        `json:"total_price"`
	TotalPriceCurrency      string         `json:"total_price_currency"`
	PromotionalPrice        *float64       `json:"promotional_price"`
	PromotionalPriceCurrency *string       `json:"promotional_price_currency"`
	Items                   []SyncBundleItem `json:"items"`
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
			INSERT INTO products (product_id, name, sku, base_measure_type, wholesale_commercial_type, units_per_box, units_per_bundle, is_active)
			VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
			ON CONFLICT (sku) DO UPDATE SET
				name = EXCLUDED.name,
				base_measure_type = EXCLUDED.base_measure_type,
				wholesale_commercial_type = EXCLUDED.wholesale_commercial_type,
				units_per_box = EXCLUDED.units_per_box,
				units_per_bundle = EXCLUDED.units_per_bundle,
				is_active = EXCLUDED.is_active
		`, p.ProductID, p.Name, p.Sku, p.BaseMeasureType, p.WholesaleCommercialType, p.UnitsPerBox, p.UnitsPerBundle, p.IsActive)
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
			INSERT INTO bundles (bundle_id, code, name, status, enabled_branch_ids, total_price, total_price_currency, promotional_price, promotional_price_currency)
			VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
			ON CONFLICT (code) DO UPDATE SET
				name = EXCLUDED.name,
				status = EXCLUDED.status,
				enabled_branch_ids = EXCLUDED.enabled_branch_ids,
				total_price = EXCLUDED.total_price,
				total_price_currency = EXCLUDED.total_price_currency,
				promotional_price = EXCLUDED.promotional_price,
				promotional_price_currency = EXCLUDED.promotional_price_currency
		`, b.BundleID, b.Code, b.Name, b.Status, b.EnabledBranchIDs,
			b.TotalPrice, b.TotalPriceCurrency, b.PromotionalPrice, b.PromotionalPriceCurrency)
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

	_, err = s.pool.Exec(ctx, `UPDATE products SET img = $2 WHERE product_id = $1`, id, img)
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

	_, err = s.pool.Exec(ctx, `UPDATE bundles SET img = $2 WHERE bundle_id = $1`, id, img)
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

	_, err = s.pool.Exec(ctx, `UPDATE products SET img = $2 WHERE sku = $1`, sku, img)
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

	_, err = s.pool.Exec(ctx, `UPDATE bundles SET img = $2 WHERE code = $1`, code, img)
	if err != nil {
		return "", fmt.Errorf("update bundle image: %w", err)
	}

	if oldImg != nil && *oldImg != "" {
		return *oldImg, nil
	}
	return "", nil
}
