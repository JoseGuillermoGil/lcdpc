package sync

import (
	"context"
	"fmt"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/lcdpc/lcdpc-go/internal/pricing"
)

type Service struct {
	pool *pgxpool.Pool
}

func NewService(pool *pgxpool.Pool) *Service {
	return &Service{pool: pool}
}

type SyncProductRequest struct {
	ProductID      uuid.UUID  `json:"product_id"`
	Name           string     `json:"name"`
	Sku            string     `json:"sku"`
	IsActive       bool       `json:"is_active"`
	BaseUnitID     *uuid.UUID `json:"base_unit_id"`
	Stock          *int       `json:"stock"`
	StockAvailable *int       `json:"stock_available"`
	StockBlocked   *int       `json:"stock_blocked"`
}

type SyncBundleRequest struct {
	BundleID           uuid.UUID         `json:"bundle_id"`
	Code               string            `json:"code"`
	Name               string            `json:"name"`
	Status             string            `json:"status"`
	BranchID           *uuid.UUID        `json:"branch_id"`
	Items              []SyncBundleItem  `json:"items"`
	Prices             []SyncBundlePrice `json:"prices"`
	Stock              *int              `json:"stock"`
	StockAvailable     *int              `json:"stock_available"`
	StockBlocked       *int              `json:"stock_blocked"`
	BlocksProductStock bool              `json:"blocks_product_stock"`
}

type SyncBundlePrice struct {
	PriceCategoryID *uuid.UUID `json:"price_category_id"`
	Amount          float64    `json:"amount"`
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
			INSERT INTO products (product_id, name, sku, is_active, base_unit_id, stock, stock_available, stock_blocked)
			VALUES ($1, $2, $3, $4, $5, COALESCE($6, 0), COALESCE($7, 0), COALESCE($8, 0))
			ON CONFLICT (sku) DO UPDATE SET
				name = EXCLUDED.name,
				is_active = EXCLUDED.is_active,
				base_unit_id = EXCLUDED.base_unit_id,
				stock = EXCLUDED.stock,
				stock_available = EXCLUDED.stock_available,
				stock_blocked = EXCLUDED.stock_blocked
		`, p.ProductID, p.Name, p.Sku, p.IsActive, p.BaseUnitID, p.Stock, p.StockAvailable, p.StockBlocked)
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
		// 1. Check existing bundle state for chain recalculation
		var oldStock int
		var oldBlocksProductStock bool
		err := tx.QueryRow(ctx, `
			SELECT stock, blocks_product_stock FROM bundles WHERE bundle_id = $1 FOR UPDATE
		`, b.BundleID).Scan(&oldStock, &oldBlocksProductStock)
		bundleExists := err != pgx.ErrNoRows

		if bundleExists && oldBlocksProductStock && oldStock > 0 {
			oldItems := make([]pricing.ChainItem, 0)
			rows, qErr := tx.Query(ctx, `SELECT id, bundle_id, product_id, quantity FROM bundle_items WHERE bundle_id = $1`, b.BundleID)
			if qErr == nil {
				for rows.Next() {
					var ci pricing.ChainItem
					var id, bundleID uuid.UUID
					if scanErr := rows.Scan(&id, &bundleID, &ci.ProductID, &ci.Quantity); scanErr == nil {
						oldItems = append(oldItems, ci)
					}
				}
				rows.Close()
			}
			if err := pricing.ReleaseProductStock(ctx, tx, oldItems, oldStock); err != nil {
				result.Errors++
				continue
			}
		}

		// 2. Upsert bundle
		_, err = tx.Exec(ctx, `
			INSERT INTO bundles (bundle_id, code, name, status, branch_id, stock, stock_available, stock_blocked, blocks_product_stock)
			VALUES ($1, $2, $3, $4, $5, COALESCE($6, 0), COALESCE($7, 0), COALESCE($8, 0), $9)
			ON CONFLICT (code) DO UPDATE SET
				name = EXCLUDED.name,
				status = EXCLUDED.status,
				branch_id = EXCLUDED.branch_id,
				stock = EXCLUDED.stock,
				stock_available = EXCLUDED.stock_available,
				stock_blocked = EXCLUDED.stock_blocked,
				blocks_product_stock = EXCLUDED.blocks_product_stock
		`, b.BundleID, b.Code, b.Name, b.Status, b.BranchID, b.Stock, b.StockAvailable, b.StockBlocked, b.BlocksProductStock)
		if err != nil {
			result.Errors++
			continue
		}

		// 3. Replace bundle items
		tx.Exec(ctx, `DELETE FROM bundle_items WHERE bundle_id = $1`, b.BundleID)
		newItems := make([]pricing.ChainItem, 0)
		for _, item := range b.Items {
			_, err := tx.Exec(ctx, `
				INSERT INTO bundle_items (id, bundle_id, product_id, quantity)
				VALUES ($1, $2, $3, $4)
			`, uuid.New(), b.BundleID, item.ProductID, item.Quantity)
			if err != nil {
				result.Errors++
			}
			newItems = append(newItems, pricing.ChainItem{ProductID: item.ProductID, Quantity: item.Quantity})
		}

		// 4. Replace bundle prices
		tx.Exec(ctx, `DELETE FROM bundle_prices WHERE bundle_id = $1`, b.BundleID)
		for _, p := range b.Prices {
			_, err := tx.Exec(ctx, `
				INSERT INTO bundle_prices (id, bundle_id, price_category_id, amount)
				VALUES ($1, $2, $3, $4)
			`, uuid.New(), b.BundleID, p.PriceCategoryID, p.Amount)
			if err != nil {
				result.Errors++
			}
		}

		// 5. Block new stock if chain enabled
		newStock := 0
		if b.Stock != nil {
			newStock = *b.Stock
		}
		if b.BlocksProductStock && newStock > 0 {
			maxStock, chainErr := pricing.MaxBundleStock(ctx, tx, newItems)
			if chainErr != nil {
				result.Errors++
				continue
			}
			if newStock > maxStock {
				result.Errors++
				continue
			}
			if blockErr := pricing.BlockProductStock(ctx, tx, newItems, newStock); blockErr != nil {
				result.Errors++
				continue
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
