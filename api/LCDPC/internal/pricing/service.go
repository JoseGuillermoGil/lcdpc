package pricing

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

// Product

type Product struct {
	ProductID            uuid.UUID `json:"product_id"`
	Name                 string    `json:"name"`
	Sku                  string    `json:"sku"`
	BaseMeasureType      string    `json:"base_measure_type"`
	WholesaleCommercialType string `json:"wholesale_commercial_type"`
	UnitsPerBox          *int      `json:"units_per_box"`
	UnitsPerBundle       *int      `json:"units_per_bundle"`
	IsActive             bool      `json:"is_active"`
	Img                  *string   `json:"img"`
}

type CreateProductRequest struct {
	Name                 string `json:"name" validate:"required"`
	Sku                  string `json:"sku" validate:"required"`
	BaseMeasureType      string `json:"base_measure_type" validate:"required"`
	WholesaleCommercialType string `json:"wholesale_commercial_type" validate:"required"`
	UnitsPerBox          *int   `json:"units_per_box"`
	UnitsPerBundle       *int   `json:"units_per_bundle"`
	Img                  *string `json:"img"`
}

func (s *Service) CreateProduct(ctx context.Context, req CreateProductRequest) (*Product, error) {
	if req.BaseMeasureType == "Unidad" {
		if req.UnitsPerBox == nil || *req.UnitsPerBox <= 0 {
			return nil, fmt.Errorf("units_per_box is required for unit-based products")
		}
		if req.UnitsPerBundle == nil || *req.UnitsPerBundle <= 0 {
			return nil, fmt.Errorf("units_per_bundle is required for unit-based products")
		}
		if *req.UnitsPerBundle < *req.UnitsPerBox {
			return nil, fmt.Errorf("units_per_bundle must be >= units_per_box")
		}
	}

	p := &Product{}
	err := s.pool.QueryRow(ctx, `
		INSERT INTO products (product_id, name, sku, base_measure_type, wholesale_commercial_type, units_per_box, units_per_bundle, is_active, img)
		VALUES ($1, $2, $3, $4, $5, $6, $7, true, $8)
		RETURNING product_id, name, sku, base_measure_type, wholesale_commercial_type, units_per_box, units_per_bundle, is_active, img
	`, uuid.New(), req.Name, req.Sku, req.BaseMeasureType, req.WholesaleCommercialType, req.UnitsPerBox, req.UnitsPerBundle, req.Img).Scan(
		&p.ProductID, &p.Name, &p.Sku, &p.BaseMeasureType, &p.WholesaleCommercialType, &p.UnitsPerBox, &p.UnitsPerBundle, &p.IsActive, &p.Img,
	)
	if err != nil {
		return nil, fmt.Errorf("create product: %w", err)
	}
	return p, nil
}

func (s *Service) GetProductByID(ctx context.Context, id uuid.UUID) (*Product, error) {
	p := &Product{}
	err := s.pool.QueryRow(ctx, `
		SELECT product_id, name, sku, base_measure_type, wholesale_commercial_type, units_per_box, units_per_bundle, is_active, img
		FROM products WHERE product_id = $1
	`, id).Scan(&p.ProductID, &p.Name, &p.Sku, &p.BaseMeasureType, &p.WholesaleCommercialType, &p.UnitsPerBox, &p.UnitsPerBundle, &p.IsActive, &p.Img)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get product: %w", err)
	}
	return p, nil
}

func (s *Service) ListProducts(ctx context.Context) ([]Product, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT product_id, name, sku, base_measure_type, wholesale_commercial_type, units_per_box, units_per_bundle, is_active, img
		FROM products ORDER BY name
	`)
	if err != nil {
		return nil, fmt.Errorf("list products: %w", err)
	}
	defer rows.Close()

	products := make([]Product, 0)
	for rows.Next() {
		var p Product
		if err := rows.Scan(&p.ProductID, &p.Name, &p.Sku, &p.BaseMeasureType, &p.WholesaleCommercialType, &p.UnitsPerBox, &p.UnitsPerBundle, &p.IsActive, &p.Img); err != nil {
			return nil, fmt.Errorf("scan product: %w", err)
		}
		products = append(products, p)
	}
	return products, nil
}

func (s *Service) UpdateProduct(ctx context.Context, id uuid.UUID, req CreateProductRequest) (*Product, error) {
	p := &Product{}
	err := s.pool.QueryRow(ctx, `
		UPDATE products SET name = $2, sku = $3, base_measure_type = $4, wholesale_commercial_type = $5, units_per_box = $6, units_per_bundle = $7, img = $8
		WHERE product_id = $1
		RETURNING product_id, name, sku, base_measure_type, wholesale_commercial_type, units_per_box, units_per_bundle, is_active, img
	`, id, req.Name, req.Sku, req.BaseMeasureType, req.WholesaleCommercialType, req.UnitsPerBox, req.UnitsPerBundle, req.Img).Scan(
		&p.ProductID, &p.Name, &p.Sku, &p.BaseMeasureType, &p.WholesaleCommercialType, &p.UnitsPerBox, &p.UnitsPerBundle, &p.IsActive, &p.Img,
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

// Bundle

type Bundle struct {
	BundleID                uuid.UUID   `json:"bundle_id"`
	Code                    string      `json:"code"`
	Name                    string      `json:"name"`
	Status                  string      `json:"status"`
	EnabledBranchIDs        []uuid.UUID `json:"enabled_branch_ids"`
	TotalPrice              float64     `json:"total_price"`
	TotalPriceCurrency      string      `json:"total_price_currency"`
	PromotionalPrice        *float64    `json:"promotional_price"`
	PromotionalPriceCurrency *string   `json:"promotional_price_currency"`
	Items                   []BundleItem `json:"items"`
	Img                     *string     `json:"img"`
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
	EnabledBranchIDs []uuid.UUID     `json:"enabled_branch_ids"`
	Img              *string         `json:"img"`
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
		INSERT INTO bundles (bundle_id, code, name, status, enabled_branch_ids, total_price, total_price_currency, img)
		VALUES ($1, $2, $3, 'Draft', $4, 0, 'USD', $5)
		RETURNING bundle_id, code, name, status, enabled_branch_ids, total_price, total_price_currency, promotional_price, promotional_price_currency, img
	`, uuid.New(), req.Code, req.Name, req.EnabledBranchIDs, req.Img).Scan(
		&bundle.BundleID, &bundle.Code, &bundle.Name, &bundle.Status, &bundle.EnabledBranchIDs,
		&bundle.TotalPrice, &bundle.TotalPriceCurrency, &bundle.PromotionalPrice, &bundle.PromotionalPriceCurrency, &bundle.Img,
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
		SELECT bundle_id, code, name, status, enabled_branch_ids, total_price, total_price_currency, promotional_price, promotional_price_currency, img
		FROM bundles WHERE bundle_id = $1
	`, id).Scan(
		&bundle.BundleID, &bundle.Code, &bundle.Name, &bundle.Status, &bundle.EnabledBranchIDs,
		&bundle.TotalPrice, &bundle.TotalPriceCurrency, &bundle.PromotionalPrice, &bundle.PromotionalPriceCurrency, &bundle.Img,
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

func (s *Service) ListBundles(ctx context.Context) ([]Bundle, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT bundle_id, code, name, status, enabled_branch_ids, total_price, total_price_currency, promotional_price, promotional_price_currency, img
		FROM bundles ORDER BY name
	`)
	if err != nil {
		return nil, fmt.Errorf("list bundles: %w", err)
	}
	defer rows.Close()

	bundles := make([]Bundle, 0)
	for rows.Next() {
		var b Bundle
		if err := rows.Scan(&b.BundleID, &b.Code, &b.Name, &b.Status, &b.EnabledBranchIDs,
			&b.TotalPrice, &b.TotalPriceCurrency, &b.PromotionalPrice, &b.PromotionalPriceCurrency, &b.Img); err != nil {
			return nil, fmt.Errorf("scan bundle: %w", err)
		}
		bundles = append(bundles, b)
	}
	return bundles, nil
}

func (s *Service) UpdateBundle(ctx context.Context, id uuid.UUID, req CreateBundleRequest) (*Bundle, error) {
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	bundle := &Bundle{}
	err = tx.QueryRow(ctx, `
		UPDATE bundles SET code = $2, name = $3, enabled_branch_ids = $4, img = $5
		WHERE bundle_id = $1
		RETURNING bundle_id, code, name, status, enabled_branch_ids, total_price, total_price_currency, promotional_price, promotional_price_currency, img
	`, id, req.Code, req.Name, req.EnabledBranchIDs, req.Img).Scan(
		&bundle.BundleID, &bundle.Code, &bundle.Name, &bundle.Status, &bundle.EnabledBranchIDs,
		&bundle.TotalPrice, &bundle.TotalPriceCurrency, &bundle.PromotionalPrice, &bundle.PromotionalPriceCurrency, &bundle.Img,
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

	_, err = s.pool.Exec(ctx, `UPDATE bundles SET img = $2 WHERE bundle_id = $1`, id, img)
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

// Price

type ProductBranchPrice struct {
	PriceID                 uuid.UUID  `json:"id"`
	ProductID               uuid.UUID  `json:"product_id"`
	BranchID                uuid.UUID  `json:"branch_id"`
	Price1Unit              float64    `json:"price1_unit"`
	Price1Currency          string     `json:"price1_currency"`
	Price2BoxBundlePiece    float64    `json:"price2_box_bundle_piece"`
	Price2Currency          string     `json:"price2_currency"`
	Price3WholesaleFrom2    float64    `json:"price3_wholesale_from2"`
	Price3Currency          string     `json:"price3_currency"`
	Price4Wholesale         *float64   `json:"price4_wholesale"`
	Price4Currency          *string    `json:"price4_currency"`
	Price4RequiresAgreement bool       `json:"price4_requires_agreement"`
	ValidFrom               time.Time  `json:"valid_from"`
	ValidUntil              *time.Time `json:"valid_until"`
}

type CreatePriceRequest struct {
	ProductID               uuid.UUID  `json:"product_id" validate:"required"`
	BranchID                uuid.UUID  `json:"branch_id" validate:"required"`
	Price1Unit              float64    `json:"price1_unit" validate:"required"`
	Price2BoxBundlePiece    float64    `json:"price2_box_bundle_piece" validate:"required"`
	Price3WholesaleFrom2    float64    `json:"price3_wholesale_from2" validate:"required"`
	Price4Wholesale         *float64   `json:"price4_wholesale"`
	Price4RequiresAgreement bool       `json:"price4_requires_agreement"`
	ValidFrom               time.Time  `json:"valid_from" validate:"required"`
	ValidUntil              *time.Time `json:"valid_until"`
}

func (s *Service) CreatePrice(ctx context.Context, req CreatePriceRequest) (*ProductBranchPrice, error) {
	p := &ProductBranchPrice{}
	currency := "USD"
	err := s.pool.QueryRow(ctx, `
		INSERT INTO product_branch_prices (id, product_id, branch_id, price1_unit, price1_currency, price2_box_bundle_piece, price2_currency, price3_wholesale_from2, price3_currency, price4_wholesale, price4_currency, price4_requires_agreement, valid_from, valid_until)
		VALUES ($1, $2, $3, $4, $5, $6, $5, $7, $5, $8, $5, $9, $10, $11)
		RETURNING id, product_id, branch_id, price1_unit, price1_currency, price2_box_bundle_piece, price2_currency, price3_wholesale_from2, price3_currency, price4_wholesale, price4_currency, price4_requires_agreement, valid_from, valid_until
	`, uuid.New(), req.ProductID, req.BranchID, req.Price1Unit, currency, req.Price2BoxBundlePiece,
		req.Price3WholesaleFrom2, req.Price4Wholesale, req.Price4RequiresAgreement, req.ValidFrom, req.ValidUntil).Scan(
		&p.PriceID, &p.ProductID, &p.BranchID, &p.Price1Unit, &p.Price1Currency,
		&p.Price2BoxBundlePiece, &p.Price2Currency, &p.Price3WholesaleFrom2, &p.Price3Currency,
		&p.Price4Wholesale, &p.Price4Currency, &p.Price4RequiresAgreement, &p.ValidFrom, &p.ValidUntil,
	)
	if err != nil {
		return nil, fmt.Errorf("create price: %w", err)
	}
	return p, nil
}

func (s *Service) GetPriceByID(ctx context.Context, id uuid.UUID) (*ProductBranchPrice, error) {
	p := &ProductBranchPrice{}
	err := s.pool.QueryRow(ctx, `
		SELECT id, product_id, branch_id, price1_unit, price1_currency, price2_box_bundle_piece, price2_currency, price3_wholesale_from2, price3_currency, price4_wholesale, price4_currency, price4_requires_agreement, valid_from, valid_until
		FROM product_branch_prices WHERE id = $1
	`, id).Scan(
		&p.PriceID, &p.ProductID, &p.BranchID, &p.Price1Unit, &p.Price1Currency,
		&p.Price2BoxBundlePiece, &p.Price2Currency, &p.Price3WholesaleFrom2, &p.Price3Currency,
		&p.Price4Wholesale, &p.Price4Currency, &p.Price4RequiresAgreement, &p.ValidFrom, &p.ValidUntil,
	)
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
		SELECT id, product_id, branch_id, price1_unit, price1_currency, price2_box_bundle_piece, price2_currency, price3_wholesale_from2, price3_currency, price4_wholesale, price4_currency, price4_requires_agreement, valid_from, valid_until
		FROM product_branch_prices WHERE product_id = $1 ORDER BY valid_from DESC
	`, productID)
	if err != nil {
		return nil, fmt.Errorf("list prices: %w", err)
	}
	defer rows.Close()

	prices := make([]ProductBranchPrice, 0)
	for rows.Next() {
		var p ProductBranchPrice
		if err := rows.Scan(&p.PriceID, &p.ProductID, &p.BranchID, &p.Price1Unit, &p.Price1Currency,
			&p.Price2BoxBundlePiece, &p.Price2Currency, &p.Price3WholesaleFrom2, &p.Price3Currency,
			&p.Price4Wholesale, &p.Price4Currency, &p.Price4RequiresAgreement, &p.ValidFrom, &p.ValidUntil); err != nil {
			return nil, fmt.Errorf("scan price: %w", err)
		}
		prices = append(prices, p)
	}
	return prices, nil
}

func (s *Service) ListPricesByBranchID(ctx context.Context, branchID uuid.UUID) ([]ProductBranchPrice, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT id, product_id, branch_id, price1_unit, price1_currency, price2_box_bundle_piece, price2_currency, price3_wholesale_from2, price3_currency, price4_wholesale, price4_currency, price4_requires_agreement, valid_from, valid_until
		FROM product_branch_prices WHERE branch_id = $1 ORDER BY valid_from DESC
	`, branchID)
	if err != nil {
		return nil, fmt.Errorf("list prices: %w", err)
	}
	defer rows.Close()

	prices := make([]ProductBranchPrice, 0)
	for rows.Next() {
		var p ProductBranchPrice
		if err := rows.Scan(&p.PriceID, &p.ProductID, &p.BranchID, &p.Price1Unit, &p.Price1Currency,
			&p.Price2BoxBundlePiece, &p.Price2Currency, &p.Price3WholesaleFrom2, &p.Price3Currency,
			&p.Price4Wholesale, &p.Price4Currency, &p.Price4RequiresAgreement, &p.ValidFrom, &p.ValidUntil); err != nil {
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
		UPDATE product_branch_prices SET price1_unit = $2, price1_currency = $3, price2_box_bundle_piece = $4, price2_currency = $3, price3_wholesale_from2 = $5, price3_currency = $3, price4_wholesale = $6, price4_currency = $3, price4_requires_agreement = $7, valid_from = $8, valid_until = $9
		WHERE id = $1
		RETURNING id, product_id, branch_id, price1_unit, price1_currency, price2_box_bundle_piece, price2_currency, price3_wholesale_from2, price3_currency, price4_wholesale, price4_currency, price4_requires_agreement, valid_from, valid_until
	`, id, req.Price1Unit, currency, req.Price2BoxBundlePiece, req.Price3WholesaleFrom2,
		req.Price4Wholesale, req.Price4RequiresAgreement, req.ValidFrom, req.ValidUntil).Scan(
		&p.PriceID, &p.ProductID, &p.BranchID, &p.Price1Unit, &p.Price1Currency,
		&p.Price2BoxBundlePiece, &p.Price2Currency, &p.Price3WholesaleFrom2, &p.Price3Currency,
		&p.Price4Wholesale, &p.Price4Currency, &p.Price4RequiresAgreement, &p.ValidFrom, &p.ValidUntil,
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
