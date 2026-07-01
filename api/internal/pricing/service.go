package pricing

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

// Product

type Product struct {
	ProductID      uuid.UUID  `json:"product_id"`
	Name           string     `json:"name"`
	Sku            string     `json:"sku"`
	IsActive       bool       `json:"is_active"`
	Img            *string    `json:"img"`
	BrandID        uuid.UUID  `json:"brand_id"`
	CategoryID     *uuid.UUID `json:"category_id"`
	BranchID       *uuid.UUID `json:"branch_id"`
	BaseUnitID     *uuid.UUID `json:"base_unit_id"`
	Stock          int        `json:"stock"`
	StockAvailable int        `json:"stock_available"`
	StockBlocked   int        `json:"stock_blocked"`
}

type CreateProductRequest struct {
	Name        string     `json:"name" validate:"required"`
	Sku         string     `json:"sku" validate:"required"`
	Img         *string    `json:"img"`
	BrandID     uuid.UUID  `json:"brand_id" validate:"required"`
	CategoryID  *uuid.UUID `json:"category_id"`
	BranchID    *uuid.UUID `json:"branch_id"`
	BaseUnitID  *uuid.UUID `json:"base_unit_id"`
	Stock       *int       `json:"stock"`
	StockAvailable *int    `json:"stock_available"`
	StockBlocked   *int    `json:"stock_blocked"`
}

func (s *Service) CreateProduct(ctx context.Context, req CreateProductRequest) (*Product, error) {
	p := &Product{}
	err := s.pool.QueryRow(ctx, `
		INSERT INTO products (product_id, name, sku, is_active, img, brand_id, category_id, branch_id, base_unit_id, stock, stock_available, stock_blocked)
		VALUES ($1, $2, $3, true, $4, $5, $6, $7, $8, COALESCE($9, 0), COALESCE($9, 0), 0)
		RETURNING product_id, name, sku, is_active, img, brand_id, category_id, branch_id, base_unit_id, stock, stock_available, stock_blocked
	`, uuid.New(), req.Name, req.Sku, req.Img, req.BrandID, req.CategoryID, req.BranchID, req.BaseUnitID, req.Stock).Scan(
		&p.ProductID, &p.Name, &p.Sku, &p.IsActive, &p.Img, &p.BrandID, &p.CategoryID, &p.BranchID, &p.BaseUnitID, &p.Stock, &p.StockAvailable, &p.StockBlocked,
	)
	if err != nil {
		return nil, fmt.Errorf("create product: %w", err)
	}
	return p, nil
}

func (s *Service) GetProductByID(ctx context.Context, id uuid.UUID) (*Product, error) {
	p := &Product{}
	err := s.pool.QueryRow(ctx, `
		SELECT product_id, name, sku, is_active, img, brand_id, category_id, branch_id, base_unit_id, stock, stock_available, stock_blocked
		FROM products WHERE product_id = $1
	`, id).Scan(&p.ProductID, &p.Name, &p.Sku, &p.IsActive, &p.Img, &p.BrandID, &p.CategoryID, &p.BranchID, &p.BaseUnitID, &p.Stock, &p.StockAvailable, &p.StockBlocked)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get product: %w", err)
	}
	return p, nil
}

func (s *Service) ListProducts(ctx context.Context, filter ...ProductFilter) ([]Product, int, error) {
	var f ProductFilter
	if len(filter) > 0 {
		f = filter[0]
	}

	countQuery := `SELECT COUNT(*) FROM products WHERE 1=1`
	dataQuery := `SELECT product_id, name, sku, is_active, img, brand_id, category_id, branch_id, base_unit_id, stock, stock_available, stock_blocked FROM products WHERE 1=1`
	var args []interface{}
	argIdx := 1

	if f.CategoryID != nil {
		countQuery += fmt.Sprintf(` AND category_id = $%d`, argIdx)
		dataQuery += fmt.Sprintf(` AND category_id = $%d`, argIdx)
		args = append(args, *f.CategoryID)
		argIdx++
	}
	if f.Name != nil {
		countQuery += fmt.Sprintf(` AND name ILIKE '%%' || $%d || '%%'`, argIdx)
		dataQuery += fmt.Sprintf(` AND name ILIKE '%%' || $%d || '%%'`, argIdx)
		args = append(args, *f.Name)
		argIdx++
	}
	if f.Sku != nil {
		countQuery += fmt.Sprintf(` AND sku = $%d`, argIdx)
		dataQuery += fmt.Sprintf(` AND sku = $%d`, argIdx)
		args = append(args, *f.Sku)
		argIdx++
	}
	if f.IsActive != nil {
		countQuery += fmt.Sprintf(` AND is_active = $%d`, argIdx)
		dataQuery += fmt.Sprintf(` AND is_active = $%d`, argIdx)
		args = append(args, *f.IsActive)
		argIdx++
	}
	if f.BranchID != nil {
		countQuery += fmt.Sprintf(` AND branch_id = $%d`, argIdx)
		dataQuery += fmt.Sprintf(` AND branch_id = $%d`, argIdx)
		args = append(args, *f.BranchID)
		argIdx++
	}

	var totalCount int
	if err := s.pool.QueryRow(ctx, countQuery, args...).Scan(&totalCount); err != nil {
		return nil, 0, fmt.Errorf("count products: %w", err)
	}

	dataQuery += ` ORDER BY name`

	if len(filter) > 0 {
		limit := f.GetLimit()
		offset := f.GetOffset()
		dataQuery += fmt.Sprintf(` LIMIT $%d OFFSET $%d`, argIdx, argIdx+1)
		args = append(args, limit, offset)
	}

	rows, err := s.pool.Query(ctx, dataQuery, args...)
	if err != nil {
		return nil, 0, fmt.Errorf("list products: %w", err)
	}
	defer rows.Close()

	products := make([]Product, 0)
	for rows.Next() {
		var p Product
		if err := rows.Scan(&p.ProductID, &p.Name, &p.Sku, &p.IsActive, &p.Img, &p.BrandID, &p.CategoryID, &p.BranchID, &p.BaseUnitID, &p.Stock, &p.StockAvailable, &p.StockBlocked); err != nil {
			return nil, 0, fmt.Errorf("scan product: %w", err)
		}
		products = append(products, p)
	}
	return products, totalCount, nil
}

func (s *Service) UpdateProduct(ctx context.Context, id uuid.UUID, req CreateProductRequest) (*Product, error) {
	var oldStock, oldStockAvail, oldStockBlocked int
	err := s.pool.QueryRow(ctx, `
		SELECT stock, stock_available, stock_blocked FROM products WHERE product_id = $1 FOR UPDATE
	`, id).Scan(&oldStock, &oldStockAvail, &oldStockBlocked)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get product for update: %w", err)
	}

	newStock := oldStock
	newStockAvail := oldStockAvail
	newStockBlocked := oldStockBlocked

	if req.Stock != nil {
		newStock = *req.Stock
		delta := newStock - oldStock
		newStockAvail = oldStockAvail + delta

		if newStock < 0 {
			return nil, fmt.Errorf("STOCK_BELOW_ZERO")
		}
		if newStockAvail < 0 {
			return nil, fmt.Errorf("STOCK_AVAILABLE_BELOW_ZERO")
		}
		if oldStockBlocked > newStock {
			return nil, fmt.Errorf("STOCK_BLOCKED_EXCEEDS_STOCK")
		}
	}

	p := &Product{}
	err = s.pool.QueryRow(ctx, `
		UPDATE products SET name = $2, sku = $3, img = $4, brand_id = $5, category_id = $6, branch_id = $7, base_unit_id = $8, stock = $9, stock_available = $10, stock_blocked = $11
		WHERE product_id = $1
		RETURNING product_id, name, sku, is_active, img, brand_id, category_id, branch_id, base_unit_id, stock, stock_available, stock_blocked
	`, id, req.Name, req.Sku, req.Img, req.BrandID, req.CategoryID, req.BranchID, req.BaseUnitID, newStock, newStockAvail, newStockBlocked).Scan(
		&p.ProductID, &p.Name, &p.Sku, &p.IsActive, &p.Img, &p.BrandID, &p.CategoryID, &p.BranchID, &p.BaseUnitID, &p.Stock, &p.StockAvailable, &p.StockBlocked,
	)
	if err != nil {
		return nil, fmt.Errorf("update product: %w", err)
	}
	return p, nil
}

func (s *Service) DeleteProduct(ctx context.Context, id uuid.UUID) error {
	_, err := s.pool.Exec(ctx, `DELETE FROM products WHERE product_id = $1`, id)
	if err != nil {
		return fmt.Errorf("delete product: %w", err)
	}
	return nil
}

func (s *Service) ToggleProductActive(ctx context.Context, id uuid.UUID) (*Product, error) {
	p := &Product{}
	err := s.pool.QueryRow(ctx, `
		UPDATE products SET is_active = NOT is_active
		WHERE product_id = $1
		RETURNING product_id, name, sku, is_active, img, brand_id, category_id, branch_id, base_unit_id, stock, stock_available, stock_blocked
	`, id).Scan(&p.ProductID, &p.Name, &p.Sku, &p.IsActive, &p.Img, &p.BrandID, &p.CategoryID, &p.BranchID, &p.BaseUnitID, &p.Stock, &p.StockAvailable, &p.StockBlocked)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("toggle product active: %w", err)
	}
	return p, nil
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

// Bundle

type Bundle struct {
	BundleID                 uuid.UUID    `json:"bundle_id"`
	Code                     string       `json:"code"`
	Name                     string       `json:"name"`
	Status                   string       `json:"status"`
	BranchID                 *uuid.UUID   `json:"branch_id"`
	TotalPrice               float64      `json:"total_price"`
	TotalPriceCurrency       string       `json:"total_price_currency"`
	PromotionalPrice         *float64     `json:"promotional_price"`
	PromotionalPriceCurrency *string      `json:"promotional_price_currency"`
	Items                    []BundleItem `json:"items"`
	Img                      *string      `json:"img"`
	CategoryID               *uuid.UUID   `json:"category_id"`
	Stock                    int          `json:"stock"`
	StockAvailable           int          `json:"stock_available"`
	StockBlocked             int          `json:"stock_blocked"`
}

type BundleItem struct {
	ID        uuid.UUID `json:"id"`
	BundleID  uuid.UUID `json:"bundle_id"`
	ProductID uuid.UUID `json:"product_id"`
	Quantity  float64   `json:"quantity"`
}

type CreateBundleRequest struct {
	Code             string          `json:"code" validate:"required"`
	Name             string          `json:"name" validate:"required"`
	Items            []BundleItemReq `json:"items" validate:"required"`
	BranchID         *uuid.UUID      `json:"branch_id"`
	Img              *string         `json:"img"`
	CategoryID       *uuid.UUID      `json:"category_id"`
	Stock            *int            `json:"stock"`
	StockAvailable   *int            `json:"stock_available"`
	StockBlocked     *int            `json:"stock_blocked"`
}

type BundleItemReq struct {
	ProductID uuid.UUID `json:"product_id" validate:"required"`
	Quantity  float64   `json:"quantity" validate:"required,gt=0"`
}

func (s *Service) CreateBundle(ctx context.Context, req CreateBundleRequest) (*Bundle, error) {
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	bundle := &Bundle{}
	err = tx.QueryRow(ctx, `
		INSERT INTO bundles (bundle_id, code, name, status, branch_id, total_price, total_price_currency, img, category_id, stock, stock_available, stock_blocked)
		VALUES ($1, $2, $3, 'Draft', $4, 0, 'USD', $5, $6, COALESCE($7, 0), COALESCE($8, 0), COALESCE($9, 0))
		RETURNING bundle_id, code, name, status, branch_id, total_price, total_price_currency, promotional_price, promotional_price_currency, img, category_id, stock, stock_available, stock_blocked
	`, uuid.New(), req.Code, req.Name, req.BranchID, req.Img, req.CategoryID, req.Stock, req.StockAvailable, req.StockBlocked).Scan(
		&bundle.BundleID, &bundle.Code, &bundle.Name, &bundle.Status, &bundle.BranchID,
		&bundle.TotalPrice, &bundle.TotalPriceCurrency, &bundle.PromotionalPrice, &bundle.PromotionalPriceCurrency, &bundle.Img, &bundle.CategoryID, &bundle.Stock, &bundle.StockAvailable, &bundle.StockBlocked,
	)
	if err != nil {
		return nil, fmt.Errorf("create bundle: %w", err)
	}

	for _, item := range req.Items {
		var bi BundleItem
		err = tx.QueryRow(ctx, `
			INSERT INTO bundle_items (id, bundle_id, product_id, quantity)
			VALUES ($1, $2, $3, $4)
			RETURNING id, bundle_id, product_id, quantity
		`, uuid.New(), bundle.BundleID, item.ProductID, item.Quantity).Scan(
			&bi.ID, &bi.BundleID, &bi.ProductID, &bi.Quantity,
		)
		if err != nil {
			return nil, fmt.Errorf("create bundle item: %w", err)
		}
		bundle.Items = append(bundle.Items, bi)
	}

	if err := tx.Commit(ctx); err != nil {
		return nil, fmt.Errorf("commit: %w", err)
	}

	return bundle, nil
}

func (s *Service) GetBundleByID(ctx context.Context, id uuid.UUID) (*Bundle, error) {
	bundle := &Bundle{}
	err := s.pool.QueryRow(ctx, `
		SELECT bundle_id, code, name, status, branch_id, total_price, total_price_currency, promotional_price, promotional_price_currency, img, category_id, stock, stock_available, stock_blocked
		FROM bundles WHERE bundle_id = $1
	`, id).Scan(
		&bundle.BundleID, &bundle.Code, &bundle.Name, &bundle.Status, &bundle.BranchID,
		&bundle.TotalPrice, &bundle.TotalPriceCurrency, &bundle.PromotionalPrice, &bundle.PromotionalPriceCurrency, &bundle.Img, &bundle.CategoryID, &bundle.Stock, &bundle.StockAvailable, &bundle.StockBlocked,
	)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get bundle: %w", err)
	}

	// Load items
	rows, err := s.pool.Query(ctx, `SELECT id, bundle_id, product_id, quantity FROM bundle_items WHERE bundle_id = $1`, id)
	if err != nil {
		return nil, fmt.Errorf("get bundle items: %w", err)
	}
	defer rows.Close()

	for rows.Next() {
		var bi BundleItem
		if err := rows.Scan(&bi.ID, &bi.BundleID, &bi.ProductID, &bi.Quantity); err != nil {
			return nil, fmt.Errorf("scan bundle item: %w", err)
		}
		bundle.Items = append(bundle.Items, bi)
	}

	return bundle, nil
}

func (s *Service) ListBundles(ctx context.Context, filter ...BundleFilter) ([]Bundle, int, error) {
	var f BundleFilter
	if len(filter) > 0 {
		f = filter[0]
	}

	countQuery := `SELECT COUNT(*) FROM bundles WHERE 1=1`
	dataQuery := `SELECT bundle_id, code, name, status, branch_id, total_price, total_price_currency, promotional_price, promotional_price_currency, img, category_id, stock, stock_available, stock_blocked FROM bundles WHERE 1=1`
	var args []interface{}
	argIdx := 1

	if f.CategoryID != nil {
		countQuery += fmt.Sprintf(` AND category_id = $%d`, argIdx)
		dataQuery += fmt.Sprintf(` AND category_id = $%d`, argIdx)
		args = append(args, *f.CategoryID)
		argIdx++
	}
	if f.Name != nil {
		countQuery += fmt.Sprintf(` AND name ILIKE '%%' || $%d || '%%'`, argIdx)
		dataQuery += fmt.Sprintf(` AND name ILIKE '%%' || $%d || '%%'`, argIdx)
		args = append(args, *f.Name)
		argIdx++
	}
	if f.Code != nil {
		countQuery += fmt.Sprintf(` AND code = $%d`, argIdx)
		dataQuery += fmt.Sprintf(` AND code = $%d`, argIdx)
		args = append(args, *f.Code)
		argIdx++
	}
	if f.Status != nil {
		countQuery += fmt.Sprintf(` AND status = $%d`, argIdx)
		dataQuery += fmt.Sprintf(` AND status = $%d`, argIdx)
		args = append(args, *f.Status)
		argIdx++
	}
	if f.BranchID != nil {
		countQuery += fmt.Sprintf(` AND branch_id = $%d`, argIdx)
		dataQuery += fmt.Sprintf(` AND branch_id = $%d`, argIdx)
		args = append(args, *f.BranchID)
		argIdx++
	}

	var totalCount int
	if err := s.pool.QueryRow(ctx, countQuery, args...).Scan(&totalCount); err != nil {
		return nil, 0, fmt.Errorf("count bundles: %w", err)
	}

	dataQuery += ` ORDER BY name`

	if len(filter) > 0 {
		limit := f.GetLimit()
		offset := f.GetOffset()
		dataQuery += fmt.Sprintf(` LIMIT $%d OFFSET $%d`, argIdx, argIdx+1)
		args = append(args, limit, offset)
	}

	rows, err := s.pool.Query(ctx, dataQuery, args...)
	if err != nil {
		return nil, 0, fmt.Errorf("list bundles: %w", err)
	}
	defer rows.Close()

	bundles := make([]Bundle, 0)
	for rows.Next() {
		var b Bundle
		if err := rows.Scan(&b.BundleID, &b.Code, &b.Name, &b.Status, &b.BranchID,
			&b.TotalPrice, &b.TotalPriceCurrency, &b.PromotionalPrice, &b.PromotionalPriceCurrency, &b.Img, &b.CategoryID, &b.Stock, &b.StockAvailable, &b.StockBlocked); err != nil {
			return nil, 0, fmt.Errorf("scan bundle: %w", err)
		}
		bundles = append(bundles, b)
	}
	return bundles, totalCount, nil
}

func (s *Service) UpdateBundle(ctx context.Context, id uuid.UUID, req CreateBundleRequest) (*Bundle, error) {
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	bundle := &Bundle{}
	err = tx.QueryRow(ctx, `
		UPDATE bundles SET code = $2, name = $3, branch_id = $4, img = $5, category_id = $6, stock = $7, stock_available = $8, stock_blocked = $9
		WHERE bundle_id = $1
		RETURNING bundle_id, code, name, status, branch_id, total_price, total_price_currency, promotional_price, promotional_price_currency, img, category_id, stock, stock_available, stock_blocked
	`, id, req.Code, req.Name, req.BranchID, req.Img, req.CategoryID, req.Stock, req.StockAvailable, req.StockBlocked).Scan(
		&bundle.BundleID, &bundle.Code, &bundle.Name, &bundle.Status, &bundle.BranchID,
		&bundle.TotalPrice, &bundle.TotalPriceCurrency, &bundle.PromotionalPrice, &bundle.PromotionalPriceCurrency, &bundle.Img, &bundle.CategoryID, &bundle.Stock, &bundle.StockAvailable, &bundle.StockBlocked,
	)
	if err != nil {
		return nil, fmt.Errorf("update bundle: %w", err)
	}

	// Replace items
	tx.Exec(ctx, `DELETE FROM bundle_items WHERE bundle_id = $1`, id)

	for _, item := range req.Items {
		var bi BundleItem
		err = tx.QueryRow(ctx, `
			INSERT INTO bundle_items (id, bundle_id, product_id, quantity)
			VALUES ($1, $2, $3, $4)
			RETURNING id, bundle_id, product_id, quantity
		`, uuid.New(), bundle.BundleID, item.ProductID, item.Quantity).Scan(
			&bi.ID, &bi.BundleID, &bi.ProductID, &bi.Quantity,
		)
		if err != nil {
			return nil, fmt.Errorf("create bundle item: %w", err)
		}
		bundle.Items = append(bundle.Items, bi)
	}

	if err := tx.Commit(ctx); err != nil {
		return nil, fmt.Errorf("commit: %w", err)
	}

	return bundle, nil
}

func (s *Service) DeleteBundle(ctx context.Context, id uuid.UUID) error {
	_, err := s.pool.Exec(ctx, `DELETE FROM bundles WHERE bundle_id = $1`, id)
	if err != nil {
		return fmt.Errorf("delete bundle: %w", err)
	}
	return nil
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

func (s *Service) UpdateBundleStatus(ctx context.Context, id uuid.UUID, status string) error {
	_, err := s.pool.Exec(ctx, `UPDATE bundles SET status = $2 WHERE bundle_id = $1`, id, status)
	if err != nil {
		return fmt.Errorf("update bundle status: %w", err)
	}
	return nil
}

// Conversion Factor

type ConversionFactor struct {
	ID         uuid.UUID `json:"id"`
	ProductID  uuid.UUID `json:"product_id"`
	FromUnitID uuid.UUID `json:"from_unit_id"`
	ToUnitID   uuid.UUID `json:"to_unit_id"`
	Amount     float64   `json:"amount"`
	Main       bool      `json:"main"`
}

type CreateConversionFactorRequest struct {
	ProductID  uuid.UUID `json:"product_id" validate:"required"`
	FromUnitID uuid.UUID `json:"from_unit_id" validate:"required"`
	ToUnitID   uuid.UUID `json:"to_unit_id" validate:"required"`
	Amount     float64   `json:"amount" validate:"required,gt=0"`
	Main       *bool     `json:"main"`
}

func (s *Service) CreateConversionFactor(ctx context.Context, req CreateConversionFactorRequest) (*ConversionFactor, error) {
	if req.FromUnitID == req.ToUnitID {
		return nil, fmt.Errorf("from_unit_id and to_unit_id must be different")
	}

	var count int
	err := s.pool.QueryRow(ctx, `
		SELECT COUNT(*) FROM conversion_factors 
		WHERE product_id = $1 AND (
			(from_unit_id = $2 AND to_unit_id = $3)
			OR (from_unit_id = $3 AND to_unit_id = $2)
		)
	`, req.ProductID, req.FromUnitID, req.ToUnitID).Scan(&count)
	if err != nil {
		return nil, fmt.Errorf("check duplicate: %w", err)
	}
	if count > 0 {
		return nil, fmt.Errorf("a conversion factor between these units already exists for this product")
	}

	main := false
	if req.Main != nil {
		main = *req.Main
	}

	if main {
		if _, err := s.pool.Exec(ctx, `UPDATE conversion_factors SET main = false WHERE product_id = $1 AND main = true`, req.ProductID); err != nil {
			return nil, fmt.Errorf("unset existing main: %w", err)
		}
	}

	cf := &ConversionFactor{}
	err = s.pool.QueryRow(ctx, `
		INSERT INTO conversion_factors (id, product_id, from_unit_id, to_unit_id, amount, main)
		VALUES ($1, $2, $3, $4, $5, $6)
		RETURNING id, product_id, from_unit_id, to_unit_id, amount, main
	`, uuid.New(), req.ProductID, req.FromUnitID, req.ToUnitID, req.Amount, main).Scan(
		&cf.ID, &cf.ProductID, &cf.FromUnitID, &cf.ToUnitID, &cf.Amount, &cf.Main,
	)
	if err != nil {
		return nil, fmt.Errorf("create conversion factor: %w", err)
	}
	return cf, nil
}

func (s *Service) GetConversionFactorByID(ctx context.Context, id uuid.UUID) (*ConversionFactor, error) {
	cf := &ConversionFactor{}
	err := s.pool.QueryRow(ctx, `
		SELECT id, product_id, from_unit_id, to_unit_id, amount, main
		FROM conversion_factors WHERE id = $1
	`, id).Scan(&cf.ID, &cf.ProductID, &cf.FromUnitID, &cf.ToUnitID, &cf.Amount, &cf.Main)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get conversion factor: %w", err)
	}
	return cf, nil
}

func (s *Service) ListConversionFactorsByProductID(ctx context.Context, productID uuid.UUID) ([]ConversionFactor, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT id, product_id, from_unit_id, to_unit_id, amount, main
		FROM conversion_factors WHERE product_id = $1 ORDER BY amount
	`, productID)
	if err != nil {
		return nil, fmt.Errorf("list conversion factors: %w", err)
	}
	defer rows.Close()

	factors := make([]ConversionFactor, 0)
	for rows.Next() {
		var cf ConversionFactor
		if err := rows.Scan(&cf.ID, &cf.ProductID, &cf.FromUnitID, &cf.ToUnitID, &cf.Amount, &cf.Main); err != nil {
			return nil, fmt.Errorf("scan conversion factor: %w", err)
		}
		factors = append(factors, cf)
	}
	return factors, nil
}

func (s *Service) UpdateConversionFactor(ctx context.Context, id uuid.UUID, req CreateConversionFactorRequest) (*ConversionFactor, error) {
	if req.FromUnitID == req.ToUnitID {
		return nil, fmt.Errorf("from_unit_id and to_unit_id must be different")
	}

	var existingProductID uuid.UUID
	if err := s.pool.QueryRow(ctx, `SELECT product_id FROM conversion_factors WHERE id = $1`, id).Scan(&existingProductID); err != nil {
		return nil, fmt.Errorf("get conversion factor: %w", err)
	}

	var count int
	err := s.pool.QueryRow(ctx, `
		SELECT COUNT(*) FROM conversion_factors 
		WHERE product_id = $1 AND id != $2 AND (
			(from_unit_id = $3 AND to_unit_id = $4)
			OR (from_unit_id = $4 AND to_unit_id = $3)
		)
	`, existingProductID, id, req.FromUnitID, req.ToUnitID).Scan(&count)
	if err != nil {
		return nil, fmt.Errorf("check duplicate: %w", err)
	}
	if count > 0 {
		return nil, fmt.Errorf("a conversion factor between these units already exists for this product")
	}

	main := false
	if req.Main != nil {
		main = *req.Main
	}

	if main {
		if _, err := s.pool.Exec(ctx, `UPDATE conversion_factors SET main = false WHERE product_id = $1 AND main = true AND id != $2`, existingProductID, id); err != nil {
			return nil, fmt.Errorf("unset existing main: %w", err)
		}
	}

	cf := &ConversionFactor{}
	err = s.pool.QueryRow(ctx, `
		UPDATE conversion_factors SET from_unit_id = $2, to_unit_id = $3, amount = $4, main = $5
		WHERE id = $1
		RETURNING id, product_id, from_unit_id, to_unit_id, amount, main
	`, id, req.FromUnitID, req.ToUnitID, req.Amount, main).Scan(
		&cf.ID, &cf.ProductID, &cf.FromUnitID, &cf.ToUnitID, &cf.Amount, &cf.Main,
	)
	if err != nil {
		return nil, fmt.Errorf("update conversion factor: %w", err)
	}
	return cf, nil
}

func (s *Service) DeleteConversionFactor(ctx context.Context, id uuid.UUID) error {
	_, err := s.pool.Exec(ctx, `DELETE FROM conversion_factors WHERE id = $1`, id)
	if err != nil {
		return fmt.Errorf("delete conversion factor: %w", err)
	}
	return nil
}

// Measurement Unit Classification

type MeasurementUnitClassification struct {
	ID   uuid.UUID `json:"id"`
	Name string    `json:"name"`
	Code string    `json:"code"`
}

type CreateMeasurementUnitClassificationRequest struct {
	Name string `json:"name" validate:"required"`
	Code string `json:"code" validate:"required"`
}

func (s *Service) CreateMeasurementUnitClassification(ctx context.Context, req CreateMeasurementUnitClassificationRequest) (*MeasurementUnitClassification, error) {
	muc := &MeasurementUnitClassification{}
	err := s.pool.QueryRow(ctx, `
		INSERT INTO measurement_unit_classifications (id, name, code)
		VALUES ($1, $2, $3)
		RETURNING id, name, code
	`, uuid.New(), req.Name, req.Code).Scan(&muc.ID, &muc.Name, &muc.Code)
	if err != nil {
		return nil, fmt.Errorf("create measurement unit classification: %w", err)
	}
	return muc, nil
}

func (s *Service) GetMeasurementUnitClassificationByID(ctx context.Context, id uuid.UUID) (*MeasurementUnitClassification, error) {
	muc := &MeasurementUnitClassification{}
	err := s.pool.QueryRow(ctx, `
		SELECT id, name, code FROM measurement_unit_classifications WHERE id = $1
	`, id).Scan(&muc.ID, &muc.Name, &muc.Code)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get measurement unit classification: %w", err)
	}
	return muc, nil
}

func (s *Service) ListMeasurementUnitClassifications(ctx context.Context) ([]MeasurementUnitClassification, error) {
	rows, err := s.pool.Query(ctx, `SELECT id, name, code FROM measurement_unit_classifications ORDER BY name`)
	if err != nil {
		return nil, fmt.Errorf("list measurement unit classifications: %w", err)
	}
	defer rows.Close()

	classifications := make([]MeasurementUnitClassification, 0)
	for rows.Next() {
		var muc MeasurementUnitClassification
		if err := rows.Scan(&muc.ID, &muc.Name, &muc.Code); err != nil {
			return nil, fmt.Errorf("scan measurement unit classification: %w", err)
		}
		classifications = append(classifications, muc)
	}
	return classifications, nil
}

func (s *Service) UpdateMeasurementUnitClassification(ctx context.Context, id uuid.UUID, req CreateMeasurementUnitClassificationRequest) (*MeasurementUnitClassification, error) {
	muc := &MeasurementUnitClassification{}
	err := s.pool.QueryRow(ctx, `
		UPDATE measurement_unit_classifications SET name = $2, code = $3
		WHERE id = $1
		RETURNING id, name, code
	`, id, req.Name, req.Code).Scan(&muc.ID, &muc.Name, &muc.Code)
	if err != nil {
		return nil, fmt.Errorf("update measurement unit classification: %w", err)
	}
	return muc, nil
}

func (s *Service) DeleteMeasurementUnitClassification(ctx context.Context, id uuid.UUID) error {
	_, err := s.pool.Exec(ctx, `DELETE FROM measurement_unit_classifications WHERE id = $1`, id)
	if err != nil {
		return fmt.Errorf("delete measurement unit classification: %w", err)
	}
	return nil
}

// Measurement Unit

type MeasurementUnit struct {
	ID             uuid.UUID  `json:"id"`
	Name           string     `json:"name"`
	Code           string     `json:"code"`
	Symbol         *string    `json:"symbol"`
	ClassificationID *uuid.UUID `json:"classification_id"`
}

type CreateMeasurementUnitRequest struct {
	Name             string     `json:"name" validate:"required"`
	Code             string     `json:"code" validate:"required"`
	Symbol           *string    `json:"symbol"`
	ClassificationID *uuid.UUID `json:"classification_id"`
}

func (s *Service) CreateMeasurementUnit(ctx context.Context, req CreateMeasurementUnitRequest) (*MeasurementUnit, error) {
	mu := &MeasurementUnit{}
	err := s.pool.QueryRow(ctx, `
		INSERT INTO measurement_units (id, name, code, symbol, classification_id)
		VALUES ($1, $2, $3, $4, $5)
		RETURNING id, name, code, symbol, classification_id
	`, uuid.New(), req.Name, req.Code, req.Symbol, req.ClassificationID).Scan(&mu.ID, &mu.Name, &mu.Code, &mu.Symbol, &mu.ClassificationID)
	if err != nil {
		return nil, fmt.Errorf("create measurement unit: %w", err)
	}
	return mu, nil
}

func (s *Service) GetMeasurementUnitByID(ctx context.Context, id uuid.UUID) (*MeasurementUnit, error) {
	mu := &MeasurementUnit{}
	err := s.pool.QueryRow(ctx, `
		SELECT id, name, code, symbol, classification_id FROM measurement_units WHERE id = $1
	`, id).Scan(&mu.ID, &mu.Name, &mu.Code, &mu.Symbol, &mu.ClassificationID)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get measurement unit: %w", err)
	}
	return mu, nil
}

func (s *Service) ListMeasurementUnits(ctx context.Context) ([]MeasurementUnit, error) {
	rows, err := s.pool.Query(ctx, `SELECT id, name, code, symbol, classification_id FROM measurement_units ORDER BY name`)
	if err != nil {
		return nil, fmt.Errorf("list measurement units: %w", err)
	}
	defer rows.Close()

	units := make([]MeasurementUnit, 0)
	for rows.Next() {
		var mu MeasurementUnit
		if err := rows.Scan(&mu.ID, &mu.Name, &mu.Code, &mu.Symbol, &mu.ClassificationID); err != nil {
			return nil, fmt.Errorf("scan measurement unit: %w", err)
		}
		units = append(units, mu)
	}
	return units, nil
}

func (s *Service) UpdateMeasurementUnit(ctx context.Context, id uuid.UUID, req CreateMeasurementUnitRequest) (*MeasurementUnit, error) {
	mu := &MeasurementUnit{}
	err := s.pool.QueryRow(ctx, `
		UPDATE measurement_units SET name = $2, code = $3, symbol = $4, classification_id = $5
		WHERE id = $1
		RETURNING id, name, code, symbol, classification_id
	`, id, req.Name, req.Code, req.Symbol, req.ClassificationID).Scan(&mu.ID, &mu.Name, &mu.Code, &mu.Symbol, &mu.ClassificationID)
	if err != nil {
		return nil, fmt.Errorf("update measurement unit: %w", err)
	}
	return mu, nil
}

func (s *Service) DeleteMeasurementUnit(ctx context.Context, id uuid.UUID) error {
	_, err := s.pool.Exec(ctx, `DELETE FROM measurement_units WHERE id = $1`, id)
	if err != nil {
		return fmt.Errorf("delete measurement unit: %w", err)
	}
	return nil
}

// Price Category

type PriceCategory struct {
	ID   uuid.UUID `json:"id"`
	Name string    `json:"name"`
	Code string    `json:"code"`
}

type CreatePriceCategoryRequest struct {
	Name string `json:"name" validate:"required"`
	Code string `json:"code" validate:"required"`
}

func (s *Service) CreatePriceCategory(ctx context.Context, req CreatePriceCategoryRequest) (*PriceCategory, error) {
	pc := &PriceCategory{}
	err := s.pool.QueryRow(ctx, `
		INSERT INTO price_categories (id, name, code)
		VALUES ($1, $2, $3)
		RETURNING id, name, code
	`, uuid.New(), req.Name, req.Code).Scan(&pc.ID, &pc.Name, &pc.Code)
	if err != nil {
		return nil, fmt.Errorf("create price category: %w", err)
	}
	return pc, nil
}

func (s *Service) GetPriceCategoryByID(ctx context.Context, id uuid.UUID) (*PriceCategory, error) {
	pc := &PriceCategory{}
	err := s.pool.QueryRow(ctx, `
		SELECT id, name, code FROM price_categories WHERE id = $1
	`, id).Scan(&pc.ID, &pc.Name, &pc.Code)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get price category: %w", err)
	}
	return pc, nil
}

func (s *Service) ListPriceCategories(ctx context.Context) ([]PriceCategory, error) {
	rows, err := s.pool.Query(ctx, `SELECT id, name, code FROM price_categories ORDER BY name`)
	if err != nil {
		return nil, fmt.Errorf("list price categories: %w", err)
	}
	defer rows.Close()

	categories := make([]PriceCategory, 0)
	for rows.Next() {
		var pc PriceCategory
		if err := rows.Scan(&pc.ID, &pc.Name, &pc.Code); err != nil {
			return nil, fmt.Errorf("scan price category: %w", err)
		}
		categories = append(categories, pc)
	}
	return categories, nil
}

func (s *Service) UpdatePriceCategory(ctx context.Context, id uuid.UUID, req CreatePriceCategoryRequest) (*PriceCategory, error) {
	pc := &PriceCategory{}
	err := s.pool.QueryRow(ctx, `
		UPDATE price_categories SET name = $2, code = $3
		WHERE id = $1
		RETURNING id, name, code
	`, id, req.Name, req.Code).Scan(&pc.ID, &pc.Name, &pc.Code)
	if err != nil {
		return nil, fmt.Errorf("update price category: %w", err)
	}
	return pc, nil
}

func (s *Service) DeletePriceCategory(ctx context.Context, id uuid.UUID) error {
	_, err := s.pool.Exec(ctx, `DELETE FROM price_categories WHERE id = $1`, id)
	if err != nil {
		return fmt.Errorf("delete price category: %w", err)
	}
	return nil
}

// Price

type ProductBranchPrice struct {
	PriceID         uuid.UUID  `json:"id"`
	ProductID       uuid.UUID  `json:"product_id"`
	PriceCategoryID *uuid.UUID `json:"price_category_id"`
	Amount          float64    `json:"amount"`
	Currency        string     `json:"currency"`
}

type CreatePriceRequest struct {
	ProductID       uuid.UUID  `json:"product_id" validate:"required"`
	PriceCategoryID *uuid.UUID `json:"price_category_id"`
	Amount          float64    `json:"amount" validate:"required"`
}

func (s *Service) CreatePrice(ctx context.Context, req CreatePriceRequest) (*ProductBranchPrice, error) {
	currency := "USD"
	p := &ProductBranchPrice{}
	err := s.pool.QueryRow(ctx, `
		INSERT INTO product_branch_prices (id, product_id, price_category_id, amount, currency)
		VALUES ($1, $2, $3, $4, $5)
		RETURNING id, product_id, price_category_id, amount, currency
	`, uuid.New(), req.ProductID, req.PriceCategoryID, req.Amount, currency).Scan(
		&p.PriceID, &p.ProductID, &p.PriceCategoryID, &p.Amount, &p.Currency,
	)
	if err != nil {
		return nil, fmt.Errorf("create price: %w", err)
	}
	return p, nil
}

func (s *Service) GetPriceByID(ctx context.Context, id uuid.UUID) (*ProductBranchPrice, error) {
	p := &ProductBranchPrice{}
	err := s.pool.QueryRow(ctx, `
		SELECT id, product_id, price_category_id, amount, currency
		FROM product_branch_prices WHERE id = $1
	`, id).Scan(&p.PriceID, &p.ProductID, &p.PriceCategoryID, &p.Amount, &p.Currency)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get price: %w", err)
	}
	return p, nil
}

func (s *Service) ListPricesByProductID(ctx context.Context, productID uuid.UUID) ([]ProductBranchPrice, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT id, product_id, price_category_id, amount, currency
		FROM product_branch_prices WHERE product_id = $1
	`, productID)
	if err != nil {
		return nil, fmt.Errorf("list prices: %w", err)
	}
	defer rows.Close()

	prices := make([]ProductBranchPrice, 0)
	for rows.Next() {
		var p ProductBranchPrice
		if err := rows.Scan(&p.PriceID, &p.ProductID, &p.PriceCategoryID, &p.Amount, &p.Currency); err != nil {
			return nil, fmt.Errorf("scan price: %w", err)
		}
		prices = append(prices, p)
	}
	return prices, nil
}

func (s *Service) UpdatePrice(ctx context.Context, id uuid.UUID, req CreatePriceRequest) (*ProductBranchPrice, error) {
	currency := "USD"
	p := &ProductBranchPrice{}
	err := s.pool.QueryRow(ctx, `
		UPDATE product_branch_prices SET price_category_id = $2, amount = $3, currency = $4
		WHERE id = $1
		RETURNING id, product_id, price_category_id, amount, currency
	`, id, req.PriceCategoryID, req.Amount, currency).Scan(
		&p.PriceID, &p.ProductID, &p.PriceCategoryID, &p.Amount, &p.Currency,
	)
	if err != nil {
		return nil, fmt.Errorf("update price: %w", err)
	}
	return p, nil
}

func (s *Service) DeletePrice(ctx context.Context, id uuid.UUID) error {
	_, err := s.pool.Exec(ctx, `DELETE FROM product_branch_prices WHERE id = $1`, id)
	if err != nil {
		return fmt.Errorf("delete price: %w", err)
	}
	return nil
}
