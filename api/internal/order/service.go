package order

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

func (s *Service) Create(ctx context.Context, req CreateOrderRequest, changedByUserID uuid.UUID) (*Order, error) {
	if len(req.Items) == 0 {
		return nil, fmt.Errorf("order must have at least one item")
	}

	for _, item := range req.Items {
		if item.ItemType != "product" && item.ItemType != "bundle" {
			return nil, fmt.Errorf("invalid item_type: %s", item.ItemType)
		}
		if item.ItemType == "product" && item.ProductID == uuid.Nil {
			return nil, fmt.Errorf("product_id is required for product items")
		}
		if item.ItemType == "bundle" && item.BundleID == uuid.Nil {
			return nil, fmt.Errorf("bundle_id is required for bundle items")
		}
		if item.ItemType == "product" {
			if err := s.validateItemPrice(ctx, item.ProductID, item.UnitPrice); err != nil {
				return nil, err
			}
		}
		if item.ItemType == "bundle" {
			if err := s.validateBundlePrice(ctx, item.BundleID, item.UnitPrice); err != nil {
				return nil, err
			}
		}
	}

	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	orderID := uuid.New()

	var displayID string
	err = tx.QueryRow(ctx, `SELECT generate_order_display_id()`).Scan(&displayID)
	if err != nil {
		return nil, fmt.Errorf("generate display_id: %w", err)
	}

	_, err = tx.Exec(ctx, `
		INSERT INTO orders (id, display_id, branch_id, client_user_id, status, price_total, total_items, currency, notes, created_at_utc, updated_at_utc)
		VALUES ($1, $2, $3, $4, $5, 0, 0, 'USD', $6, now(), now())
	`, orderID, displayID, req.BranchID, req.ClientUserID, StatusPendingReview, nullString(req.Notes))
	if err != nil {
		return nil, fmt.Errorf("insert order: %w", err)
	}

	var priceTotal float64
	var totalItems int

	for _, item := range req.Items {
		subtotal := item.Quantity * item.UnitPrice
		priceTotal += subtotal
		totalItems += int(item.Quantity)

		var productID, bundleID *uuid.UUID
		if item.ItemType == "product" {
			productID = &item.ProductID

			var stock, stockAvailable, stockBlocked float64
			err = tx.QueryRow(ctx, `
				SELECT stock, stock_available, stock_blocked
				FROM products WHERE product_id = $1 FOR UPDATE
			`, item.ProductID).Scan(&stock, &stockAvailable, &stockBlocked)
			if err != nil {
				return nil, fmt.Errorf("get product stock: %w", err)
			}

			qty := item.Quantity
			if stockAvailable < qty {
				return nil, fmt.Errorf("INSUFFICIENT_STOCK: product %s has %.4f available, requested %.4f", item.ProductID, stockAvailable, qty)
			}
			if stockBlocked+qty > stock {
				return nil, fmt.Errorf("STOCK_EXCEEDED: product %s stock=%.4f blocked=%.4f requested=%.4f", item.ProductID, stock, stockBlocked, qty)
			}

			_, err = tx.Exec(ctx, `
				UPDATE products
				SET stock_available = stock_available - $1, stock_blocked = stock_blocked + $1
				WHERE product_id = $2
			`, qty, item.ProductID)
			if err != nil {
				return nil, fmt.Errorf("update product stock: %w", err)
			}
		} else {
			bundleID = &item.BundleID
			var bundleStock, bundleAvailable, bundleBlocked float64
			var blocksProductStock bool
			err = tx.QueryRow(ctx, `
				SELECT stock, stock_available, stock_blocked, blocks_product_stock
				FROM bundles WHERE bundle_id = $1 FOR UPDATE
			`, item.BundleID).Scan(&bundleStock, &bundleAvailable, &bundleBlocked, &blocksProductStock)
			if err != nil {
				return nil, fmt.Errorf("get bundle stock: %w", err)
			}
			if blocksProductStock {
				qty := item.Quantity
				if bundleAvailable < qty {
					return nil, fmt.Errorf("INSUFFICIENT_BUNDLE_STOCK: bundle %s has %.4f available, requested %.4f", item.BundleID, bundleAvailable, qty)
				}
				if bundleBlocked+qty > bundleStock {
					return nil, fmt.Errorf("BUNDLE_STOCK_EXCEEDED: bundle %s stock=%.4f blocked=%.4f requested=%.4f", item.BundleID, bundleStock, bundleBlocked, qty)
				}
				_, err = tx.Exec(ctx, `
					UPDATE bundles
					SET stock_available = stock_available - $1, stock_blocked = stock_blocked + $1
					WHERE bundle_id = $2
				`, qty, item.BundleID)
				if err != nil {
					return nil, fmt.Errorf("update bundle stock: %w", err)
				}

				if err := blockBundleProductStock(ctx, tx, item.BundleID, qty); err != nil {
					return nil, err
				}
			}
		}

		_, err = tx.Exec(ctx, `
			INSERT INTO order_items (id, order_id, item_type, product_id, bundle_id, quantity, unit_price, subtotal, currency)
			VALUES ($1, $2, $3, $4, $5, $6, $7, $8, 'USD')
		`, uuid.New(), orderID, item.ItemType, productID, bundleID, item.Quantity, item.UnitPrice, subtotal)
		if err != nil {
			return nil, fmt.Errorf("insert order item: %w", err)
		}
	}

	_, err = tx.Exec(ctx, `
		UPDATE orders SET price_total = $2, total_items = $3, updated_at_utc = now() WHERE id = $1
	`, orderID, priceTotal, totalItems)
	if err != nil {
		return nil, fmt.Errorf("update order totals: %w", err)
	}

	_, err = tx.Exec(ctx, `
		INSERT INTO order_status_history (id, order_id, from_status, to_status, changed_by_user_id, notes, created_at_utc)
		VALUES ($1, $2, NULL, $3, $4, NULL, now())
	`, uuid.New(), orderID, StatusPendingReview, changedByUserID)
	if err != nil {
		return nil, fmt.Errorf("insert status history: %w", err)
	}

	if err := tx.Commit(ctx); err != nil {
		return nil, fmt.Errorf("commit: %w", err)
	}

	return s.GetByID(ctx, orderID)
}

func (s *Service) GetByID(ctx context.Context, id uuid.UUID) (*Order, error) {
	o := &Order{}
	err := s.pool.QueryRow(ctx, `
		SELECT id, display_id, branch_id, client_user_id, status, price_total, total_items, currency, notes, deleted_at, created_at_utc, updated_at_utc
		FROM orders WHERE id = $1 AND deleted_at IS NULL
	`, id).Scan(&o.ID, &o.DisplayID, &o.BranchID, &o.ClientUserID, &o.Status, &o.PriceTotal, &o.TotalItems,
		&o.Currency, &o.Notes, &o.DeletedAt, &o.CreatedAtUtc, &o.UpdatedAtUtc)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("ORDER_NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get order: %w", err)
	}

	o.Items, err = s.getItems(ctx, id)
	if err != nil {
		return nil, err
	}

	return o, nil
}

func (s *Service) List(ctx context.Context, filter OrderFilter) ([]Order, int, error) {
	countQuery := `SELECT COUNT(*) FROM orders WHERE deleted_at IS NULL`
	dataQuery := `SELECT id, display_id, branch_id, client_user_id, status, price_total, total_items, currency, notes, deleted_at, created_at_utc, updated_at_utc FROM orders WHERE deleted_at IS NULL`
	args := []interface{}{}
	argIdx := 1

	if filter.BranchID != nil {
		clause := fmt.Sprintf(" AND branch_id = $%d", argIdx)
		countQuery += clause
		dataQuery += clause
		args = append(args, *filter.BranchID)
		argIdx++
	}
	if filter.ClientUserID != nil {
		clause := fmt.Sprintf(" AND client_user_id = $%d", argIdx)
		countQuery += clause
		dataQuery += clause
		args = append(args, *filter.ClientUserID)
		argIdx++
	}
	if filter.Status != nil {
		clause := fmt.Sprintf(" AND status = $%d", argIdx)
		countQuery += clause
		dataQuery += clause
		args = append(args, *filter.Status)
		argIdx++
	}
	if filter.DisplayID != nil {
		clause := fmt.Sprintf(" AND display_id ILIKE $%d", argIdx)
		countQuery += clause
		dataQuery += clause
		args = append(args, "%"+*filter.DisplayID+"%")
		argIdx++
	}

	var totalCount int
	if err := s.pool.QueryRow(ctx, countQuery, args...).Scan(&totalCount); err != nil {
		return nil, 0, fmt.Errorf("count orders: %w", err)
	}

	dataQuery += " ORDER BY created_at_utc DESC"
	limit := filter.GetLimit()
	offset := filter.GetOffset()
	dataQuery += fmt.Sprintf(" LIMIT $%d OFFSET $%d", argIdx, argIdx+1)
	args = append(args, limit, offset)

	rows, err := s.pool.Query(ctx, dataQuery, args...)
	if err != nil {
		return nil, 0, fmt.Errorf("list orders: %w", err)
	}
	defer rows.Close()

	orders := make([]Order, 0)
	for rows.Next() {
		var o Order
		if err := rows.Scan(&o.ID, &o.DisplayID, &o.BranchID, &o.ClientUserID, &o.Status, &o.PriceTotal, &o.TotalItems,
			&o.Currency, &o.Notes, &o.DeletedAt, &o.CreatedAtUtc, &o.UpdatedAtUtc); err != nil {
			return nil, 0, fmt.Errorf("scan order: %w", err)
		}
		orders = append(orders, o)
	}
	return orders, totalCount, nil
}

func (s *Service) Update(ctx context.Context, id uuid.UUID, req UpdateOrderRequest) (*Order, error) {
	o, err := s.GetByID(ctx, id)
	if err != nil {
		return nil, err
	}

	if !IsEditable(o.Status) {
		return nil, fmt.Errorf("ORDER_NOT_EDITABLE")
	}

	if req.Items != nil {
		if len(req.Items) == 0 {
			return nil, fmt.Errorf("order must have at least one item")
		}
		for _, item := range req.Items {
			if item.ItemType != "product" && item.ItemType != "bundle" {
				return nil, fmt.Errorf("invalid item_type: %s", item.ItemType)
			}
			if item.ItemType == "product" && item.ProductID == uuid.Nil {
				return nil, fmt.Errorf("product_id is required for product items")
			}
			if item.ItemType == "bundle" && item.BundleID == uuid.Nil {
				return nil, fmt.Errorf("bundle_id is required for bundle items")
			}
			if item.ItemType == "product" {
				if err := s.validateItemPrice(ctx, item.ProductID, item.UnitPrice); err != nil {
					return nil, err
				}
			}
			if item.ItemType == "bundle" {
				if err := s.validateBundlePrice(ctx, item.BundleID, item.UnitPrice); err != nil {
					return nil, err
				}
			}
		}
	}

	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	if req.Notes != "" {
		_, err = tx.Exec(ctx, `UPDATE orders SET notes = $2, updated_at_utc = now() WHERE id = $1`, id, req.Notes)
		if err != nil {
			return nil, fmt.Errorf("update notes: %w", err)
		}
	}

	if req.Items != nil {
		if err := releaseBlockedStock(ctx, tx, o.Items); err != nil {
			return nil, err
		}

		_, err = tx.Exec(ctx, `DELETE FROM order_items WHERE order_id = $1`, id)
		if err != nil {
			return nil, fmt.Errorf("delete items: %w", err)
		}

		var priceTotal float64
		var totalItems int

		for _, item := range req.Items {
			subtotal := item.Quantity * item.UnitPrice
			priceTotal += subtotal
			totalItems += int(item.Quantity)

			var productID, bundleID *uuid.UUID
			if item.ItemType == "product" {
				productID = &item.ProductID

				var stock, stockAvailable, stockBlocked float64
				err = tx.QueryRow(ctx, `
					SELECT stock, stock_available, stock_blocked
					FROM products WHERE product_id = $1 FOR UPDATE
				`, item.ProductID).Scan(&stock, &stockAvailable, &stockBlocked)
				if err != nil {
					return nil, fmt.Errorf("get product stock: %w", err)
				}

				qty := item.Quantity
				if stockAvailable < qty {
					return nil, fmt.Errorf("INSUFFICIENT_STOCK: product %s has %.4f available, requested %.4f", item.ProductID, stockAvailable, qty)
				}
				if stockBlocked+qty > stock {
					return nil, fmt.Errorf("STOCK_EXCEEDED: product %s stock=%.4f blocked=%.4f requested=%.4f", item.ProductID, stock, stockBlocked, qty)
				}

				_, err = tx.Exec(ctx, `
					UPDATE products
					SET stock_available = stock_available - $1, stock_blocked = stock_blocked + $1
					WHERE product_id = $2
				`, qty, item.ProductID)
				if err != nil {
					return nil, fmt.Errorf("update product stock: %w", err)
				}
			} else {
				bundleID = &item.BundleID
				var bundleStock, bundleAvailable, bundleBlocked float64
				var blocksProductStock bool
				err = tx.QueryRow(ctx, `
					SELECT stock, stock_available, stock_blocked, blocks_product_stock
					FROM bundles WHERE bundle_id = $1 FOR UPDATE
				`, item.BundleID).Scan(&bundleStock, &bundleAvailable, &bundleBlocked, &blocksProductStock)
				if err != nil {
					return nil, fmt.Errorf("get bundle stock: %w", err)
				}
				if blocksProductStock {
					qty := item.Quantity
					if bundleAvailable < qty {
						return nil, fmt.Errorf("INSUFFICIENT_BUNDLE_STOCK: bundle %s has %.4f available, requested %.4f", item.BundleID, bundleAvailable, qty)
					}
					if bundleBlocked+qty > bundleStock {
						return nil, fmt.Errorf("BUNDLE_STOCK_EXCEEDED: bundle %s stock=%.4f blocked=%.4f requested=%.4f", item.BundleID, bundleStock, bundleBlocked, qty)
					}
					_, err = tx.Exec(ctx, `
						UPDATE bundles
						SET stock_available = stock_available - $1, stock_blocked = stock_blocked + $1
						WHERE bundle_id = $2
					`, qty, item.BundleID)
					if err != nil {
						return nil, fmt.Errorf("update bundle stock: %w", err)
					}

					if err := blockBundleProductStock(ctx, tx, item.BundleID, qty); err != nil {
						return nil, err
					}
				}
			}

			_, err = tx.Exec(ctx, `
				INSERT INTO order_items (id, order_id, item_type, product_id, bundle_id, quantity, unit_price, subtotal, currency)
				VALUES ($1, $2, $3, $4, $5, $6, $7, $8, 'USD')
			`, uuid.New(), id, item.ItemType, productID, bundleID, item.Quantity, item.UnitPrice, subtotal)
			if err != nil {
				return nil, fmt.Errorf("insert order item: %w", err)
			}
		}

		_, err = tx.Exec(ctx, `UPDATE orders SET price_total = $2, total_items = $3, updated_at_utc = now() WHERE id = $1`, id, priceTotal, totalItems)
		if err != nil {
			return nil, fmt.Errorf("update totals: %w", err)
		}
	}

	if err := tx.Commit(ctx); err != nil {
		return nil, fmt.Errorf("commit: %w", err)
	}

	return s.GetByID(ctx, id)
}

func (s *Service) Delete(ctx context.Context, id uuid.UUID, changedByUserID uuid.UUID) error {
	o, err := s.GetByID(ctx, id)
	if err != nil {
		return err
	}

	if o.Status != StatusPendingReview {
		return fmt.Errorf("ORDER_CANNOT_BE_DELETED")
	}

	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	if err := releaseBlockedStock(ctx, tx, o.Items); err != nil {
		return err
	}

	_, err = tx.Exec(ctx, `UPDATE orders SET deleted_at = now(), updated_at_utc = now() WHERE id = $1`, id)
	if err != nil {
		return fmt.Errorf("soft delete order: %w", err)
	}

	return tx.Commit(ctx)
}

func (s *Service) ChangeStatus(ctx context.Context, id uuid.UUID, req StatusChangeRequest, changedByUserID uuid.UUID) (*Order, error) {
	o, err := s.GetByID(ctx, id)
	if err != nil {
		return nil, err
	}

	if !IsTransitionAllowed(o.Status, req.ToStatus) {
		return nil, fmt.Errorf("INVALID_TRANSITION")
	}

	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	if isStockReleaseStatus(req.ToStatus) {
		if err := releaseBlockedStock(ctx, tx, o.Items); err != nil {
			return nil, err
		}
	}

	if req.ToStatus == StatusCompleted {
		if err := completeOrderStock(ctx, tx, o.Items); err != nil {
			return nil, err
		}
	}

	_, err = tx.Exec(ctx, `UPDATE orders SET status = $2, updated_at_utc = now() WHERE id = $1`, id, req.ToStatus)
	if err != nil {
		return nil, fmt.Errorf("update status: %w", err)
	}

	_, err = tx.Exec(ctx, `
		INSERT INTO order_status_history (id, order_id, from_status, to_status, changed_by_user_id, notes, created_at_utc)
		VALUES ($1, $2, $3, $4, $5, $6, now())
	`, uuid.New(), id, o.Status, req.ToStatus, changedByUserID, nullString(req.Notes))
	if err != nil {
		return nil, fmt.Errorf("insert history: %w", err)
	}

	if err := tx.Commit(ctx); err != nil {
		return nil, fmt.Errorf("commit: %w", err)
	}

	return s.GetByID(ctx, id)
}

func (s *Service) GetHistory(ctx context.Context, orderID uuid.UUID) ([]StatusHistoryEntry, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT id, order_id, from_status, to_status, changed_by_user_id, notes, created_at_utc
		FROM order_status_history WHERE order_id = $1 ORDER BY created_at_utc
	`, orderID)
	if err != nil {
		return nil, fmt.Errorf("get history: %w", err)
	}
	defer rows.Close()

	entries := make([]StatusHistoryEntry, 0)
	for rows.Next() {
		var e StatusHistoryEntry
		if err := rows.Scan(&e.ID, &e.OrderID, &e.FromStatus, &e.ToStatus, &e.ChangedByUserID, &e.Notes, &e.CreatedAtUtc); err != nil {
			return nil, fmt.Errorf("scan history: %w", err)
		}
		entries = append(entries, e)
	}
	return entries, nil
}

func (s *Service) getItems(ctx context.Context, orderID uuid.UUID) ([]OrderItem, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT id, order_id, item_type, product_id, bundle_id, quantity, unit_price, subtotal, currency
		FROM order_items WHERE order_id = $1
	`, orderID)
	if err != nil {
		return nil, fmt.Errorf("get items: %w", err)
	}
	defer rows.Close()

	items := make([]OrderItem, 0)
	for rows.Next() {
		var i OrderItem
		if err := rows.Scan(&i.ID, &i.OrderID, &i.ItemType, &i.ProductID, &i.BundleID, &i.Quantity, &i.UnitPrice, &i.Subtotal, &i.Currency); err != nil {
			return nil, fmt.Errorf("scan item: %w", err)
		}
		items = append(items, i)
	}
	return items, nil
}

func nullString(s string) interface{} {
	if s == "" {
		return nil
	}
	return s
}

func (s *Service) validateItemPrice(ctx context.Context, productID uuid.UUID, unitPrice float64) error {
	var count int
	err := s.pool.QueryRow(ctx, `
		SELECT COUNT(*) FROM product_branch_prices WHERE product_id = $1
	`, productID).Scan(&count)
	if err != nil {
		return fmt.Errorf("check prices: %w", err)
	}
	if count == 0 {
		return nil
	}

	var exists bool
	err = s.pool.QueryRow(ctx, `
		SELECT EXISTS(SELECT 1 FROM product_branch_prices WHERE product_id = $1 AND amount = $2)
	`, productID, unitPrice).Scan(&exists)
	if err != nil {
		return fmt.Errorf("validate price: %w", err)
	}
	if !exists {
		return fmt.Errorf("PRICE_MISMATCH: product %s unit_price %.2f does not match any registered price", productID, unitPrice)
	}
	return nil
}

func (s *Service) validateBundlePrice(ctx context.Context, bundleID uuid.UUID, unitPrice float64) error {
	var count int
	err := s.pool.QueryRow(ctx, `
		SELECT COUNT(*) FROM bundle_prices WHERE bundle_id = $1
	`, bundleID).Scan(&count)
	if err != nil {
		return fmt.Errorf("check bundle prices: %w", err)
	}
	if count == 0 {
		return nil
	}

	var exists bool
	err = s.pool.QueryRow(ctx, `
		SELECT EXISTS(SELECT 1 FROM bundle_prices WHERE bundle_id = $1 AND amount = $2)
	`, bundleID, unitPrice).Scan(&exists)
	if err != nil {
		return fmt.Errorf("validate bundle price: %w", err)
	}
	if !exists {
		return fmt.Errorf("PRICE_MISMATCH: bundle %s unit_price %.2f does not match any registered price", bundleID, unitPrice)
	}
	return nil
}

func isStockReleaseStatus(status string) bool {
	return status == StatusCancelledByCustomer || status == StatusRejectedByValidation
}

func blockBundleProductStock(ctx context.Context, tx pgx.Tx, bundleID uuid.UUID, bundleQty float64) error {
	rows, err := tx.Query(ctx, `
		SELECT product_id, quantity FROM bundle_items WHERE bundle_id = $1
	`, bundleID)
	if err != nil {
		return fmt.Errorf("get bundle items for stock block: %w", err)
	}
	type bundleItem struct {
		productID uuid.UUID
		qty       float64
	}
	var items []bundleItem
	for rows.Next() {
		var productID uuid.UUID
		var itemQty float64
		if scanErr := rows.Scan(&productID, &itemQty); scanErr != nil {
			rows.Close()
			return fmt.Errorf("scan bundle item for stock block: %w", scanErr)
		}
		items = append(items, bundleItem{productID: productID, qty: itemQty * bundleQty})
	}
	rows.Close()

	for _, bi := range items {
		if bi.qty > 0 {
			var stock, stockAvailable, stockBlocked float64
			err := tx.QueryRow(ctx, `
				SELECT stock, stock_available, stock_blocked
				FROM products WHERE product_id = $1 FOR UPDATE
			`, bi.productID).Scan(&stock, &stockAvailable, &stockBlocked)
			if err != nil {
				return fmt.Errorf("get product stock for bundle block: %w", err)
			}
			if stockAvailable < bi.qty {
				return fmt.Errorf("INSUFFICIENT_STOCK: product %s has %.4f available, bundle needs %.4f", bi.productID, stockAvailable, bi.qty)
			}
			if stockBlocked+bi.qty > stock {
				return fmt.Errorf("STOCK_EXCEEDED: product %s stock=%.4f blocked=%.4f bundle needs %.4f", bi.productID, stock, stockBlocked, bi.qty)
			}
			_, err = tx.Exec(ctx, `
				UPDATE products
				SET stock_available = stock_available - $1, stock_blocked = stock_blocked + $1
				WHERE product_id = $2
			`, bi.qty, bi.productID)
			if err != nil {
				return fmt.Errorf("block product stock for bundle: %w", err)
			}
		}
	}
	return nil
}

func releaseBlockedStock(ctx context.Context, tx pgx.Tx, items []OrderItem) error {
	for _, item := range items {
		switch item.ItemType {
		case "product":
			if item.ProductID == nil {
				continue
			}
			qty := item.Quantity
			_, err := tx.Exec(ctx, `
				UPDATE products
				SET stock_available = stock_available + $1, stock_blocked = stock_blocked - $1
				WHERE product_id = $2
			`, qty, *item.ProductID)
			if err != nil {
				return fmt.Errorf("release stock for product %s: %w", *item.ProductID, err)
			}
		case "bundle":
			if item.BundleID == nil {
				continue
			}
			qty := item.Quantity
			var blocksProductStock bool
			err := tx.QueryRow(ctx, `
				SELECT blocks_product_stock FROM bundles WHERE bundle_id = $1 FOR UPDATE
			`, *item.BundleID).Scan(&blocksProductStock)
			if err != nil {
				continue
			}
			if blocksProductStock {
				_, err = tx.Exec(ctx, `
					UPDATE bundles
					SET stock_available = stock_available + $1, stock_blocked = stock_blocked - $1
					WHERE bundle_id = $2
				`, qty, *item.BundleID)
				if err != nil {
					return fmt.Errorf("release stock for bundle %s: %w", *item.BundleID, err)
				}

				bundleItems, qErr := tx.Query(ctx, `
					SELECT product_id, quantity FROM bundle_items WHERE bundle_id = $1
				`, *item.BundleID)
				if qErr != nil {
					return fmt.Errorf("get bundle items for release: %w", qErr)
				}
				type bundleItem struct {
					productID uuid.UUID
					qty       float64
				}
				var bItems []bundleItem
				for bundleItems.Next() {
					var productID uuid.UUID
					var itemQty float64
					if scanErr := bundleItems.Scan(&productID, &itemQty); scanErr != nil {
						bundleItems.Close()
						return fmt.Errorf("scan bundle item for release: %w", scanErr)
					}
					bItems = append(bItems, bundleItem{productID: productID, qty: itemQty * qty})
				}
				bundleItems.Close()
				for _, bi := range bItems {
					if bi.qty > 0 {
						_, updateErr := tx.Exec(ctx, `
							UPDATE products
							SET stock_available = stock_available + $1, stock_blocked = stock_blocked - $1
							WHERE product_id = $2
						`, bi.qty, bi.productID)
						if updateErr != nil {
							return fmt.Errorf("release product stock for %s: %w", bi.productID, updateErr)
						}
					}
				}
			}
		}
	}
	return nil
}

func completeOrderStock(ctx context.Context, tx pgx.Tx, items []OrderItem) error {
	for _, item := range items {
		switch item.ItemType {
		case "product":
			if item.ProductID == nil {
				continue
			}
			qty := item.Quantity
			// Permanently reduce stock + release blocked
			_, err := tx.Exec(ctx, `
				UPDATE products
				SET stock = stock - $1, stock_blocked = stock_blocked - $1
				WHERE product_id = $2
			`, qty, *item.ProductID)
			if err != nil {
				return fmt.Errorf("complete stock for product %s: %w", *item.ProductID, err)
			}
		case "bundle":
			if item.BundleID == nil {
				continue
			}
			qty := item.Quantity
			var blocksProductStock bool
			var bundleStock float64
			err := tx.QueryRow(ctx, `
				SELECT blocks_product_stock, stock FROM bundles WHERE bundle_id = $1 FOR UPDATE
			`, *item.BundleID).Scan(&blocksProductStock, &bundleStock)
			if err != nil {
				continue
			}
			if blocksProductStock {
				// Permanently reduce bundle stock + release blocked
				_, err = tx.Exec(ctx, `
					UPDATE bundles
					SET stock = stock - $1, stock_blocked = stock_blocked - $1
					WHERE bundle_id = $2
				`, qty, *item.BundleID)
				if err != nil {
					return fmt.Errorf("complete stock for bundle %s: %w", *item.BundleID, err)
				}

				// Reduce product stock proportionally
				bundleItems, qErr := tx.Query(ctx, `
					SELECT product_id, quantity FROM bundle_items WHERE bundle_id = $1
				`, *item.BundleID)
				if qErr != nil {
					return fmt.Errorf("get bundle items: %w", qErr)
				}
				type bundleItem struct {
					productID uuid.UUID
					qty       float64
				}
				var bItems []bundleItem
				for bundleItems.Next() {
					var productID uuid.UUID
					var itemQty float64
					if scanErr := bundleItems.Scan(&productID, &itemQty); scanErr != nil {
						bundleItems.Close()
						return fmt.Errorf("scan bundle item: %w", scanErr)
					}
					bItems = append(bItems, bundleItem{productID: productID, qty: itemQty * qty})
				}
				bundleItems.Close()
				for _, bi := range bItems {
					if bi.qty > 0 {
						_, updateErr := tx.Exec(ctx, `
							UPDATE products
							SET stock = stock - $1, stock_blocked = stock_blocked - $1
							WHERE product_id = $2
						`, bi.qty, bi.productID)
						if updateErr != nil {
							return fmt.Errorf("reduce product stock for %s: %w", bi.productID, updateErr)
						}
					}
				}
			}
		}
	}
	return nil
}
