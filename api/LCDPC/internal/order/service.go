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

	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	orderID := uuid.New()
	_, err = tx.Exec(ctx, `
		INSERT INTO orders (id, sede_id, client_user_id, status, price_total, total_items, currency, notes, created_at_utc, updated_at_utc)
		VALUES ($1, $2, $3, $4, 0, 0, 'USD', $5, now(), now())
	`, orderID, req.SedeID, req.ClientUserID, StatusPendingReview, nullString(req.Notes))
	if err != nil {
		return nil, fmt.Errorf("insert order: %w", err)
	}

	var priceTotal float64
	var totalItems int

	for _, item := range req.Items {
		subtotal := item.Quantity * item.UnitPrice
		priceTotal += subtotal
		totalItems += int(item.Quantity)

		var productoID, comboID *uuid.UUID
		if item.ItemType == "producto" {
			productoID = &item.ProductoID
		} else {
			comboID = &item.ComboID
		}

		_, err = tx.Exec(ctx, `
			INSERT INTO order_items (id, order_id, item_type, producto_id, combo_id, quantity, unit_price, subtotal, currency)
			VALUES ($1, $2, $3, $4, $5, $6, $7, $8, 'USD')
		`, uuid.New(), orderID, item.ItemType, productoID, comboID, item.Quantity, item.UnitPrice, subtotal)
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
		SELECT id, sede_id, client_user_id, status, price_total, total_items, currency, notes, created_at_utc, updated_at_utc
		FROM orders WHERE id = $1
	`, id).Scan(&o.ID, &o.SedeID, &o.ClientUserID, &o.Status, &o.PriceTotal, &o.TotalItems,
		&o.Currency, &o.Notes, &o.CreatedAtUtc, &o.UpdatedAtUtc)
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

func (s *Service) List(ctx context.Context, filter OrderFilter) ([]Order, error) {
	query := `SELECT id, sede_id, client_user_id, status, price_total, total_items, currency, notes, created_at_utc, updated_at_utc FROM orders WHERE 1=1`
	args := []interface{}{}
	argIdx := 1

	if filter.SedeID != nil {
		query += fmt.Sprintf(" AND sede_id = $%d", argIdx)
		args = append(args, *filter.SedeID)
		argIdx++
	}
	if filter.ClientUserID != nil {
		query += fmt.Sprintf(" AND client_user_id = $%d", argIdx)
		args = append(args, *filter.ClientUserID)
		argIdx++
	}
	if filter.Status != nil {
		query += fmt.Sprintf(" AND status = $%d", argIdx)
		args = append(args, *filter.Status)
		argIdx++
	}

	query += " ORDER BY created_at_utc DESC"

	rows, err := s.pool.Query(ctx, query, args...)
	if err != nil {
		return nil, fmt.Errorf("list orders: %w", err)
	}
	defer rows.Close()

	var orders []Order
	for rows.Next() {
		var o Order
		if err := rows.Scan(&o.ID, &o.SedeID, &o.ClientUserID, &o.Status, &o.PriceTotal, &o.TotalItems,
			&o.Currency, &o.Notes, &o.CreatedAtUtc, &o.UpdatedAtUtc); err != nil {
			return nil, fmt.Errorf("scan order: %w", err)
		}
		orders = append(orders, o)
	}
	return orders, nil
}

func (s *Service) Update(ctx context.Context, id uuid.UUID, req UpdateOrderRequest) (*Order, error) {
	o, err := s.GetByID(ctx, id)
	if err != nil {
		return nil, err
	}

	if !IsEditable(o.Status) {
		return nil, fmt.Errorf("ORDER_NOT_EDITABLE")
	}

	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	if req.Notes != "" || req.Items != nil {
		_, err = tx.Exec(ctx, `UPDATE orders SET notes = $2, updated_at_utc = now() WHERE id = $1`, id, nullString(req.Notes))
		if err != nil {
			return nil, fmt.Errorf("update order: %w", err)
		}
	}

	if req.Items != nil {
		if len(req.Items) == 0 {
			return nil, fmt.Errorf("order must have at least one item")
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

			var productoID, comboID *uuid.UUID
			if item.ItemType == "producto" {
				productoID = &item.ProductoID
			} else {
				comboID = &item.ComboID
			}

			_, err = tx.Exec(ctx, `
				INSERT INTO order_items (id, order_id, item_type, producto_id, combo_id, quantity, unit_price, subtotal, currency)
				VALUES ($1, $2, $3, $4, $5, $6, $7, $8, 'USD')
			`, uuid.New(), id, item.ItemType, productoID, comboID, item.Quantity, item.UnitPrice, subtotal)
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

	if IsTerminal(o.Status) {
		return fmt.Errorf("ORDER_IN_TERMINAL_STATUS")
	}

	_, err = s.pool.Exec(ctx, `UPDATE orders SET status = $2, updated_at_utc = now() WHERE id = $1`, id, StatusCancelledByCustomer)
	if err != nil {
		return fmt.Errorf("cancel order: %w", err)
	}

	_, err = s.pool.Exec(ctx, `
		INSERT INTO order_status_history (id, order_id, from_status, to_status, changed_by_user_id, notes, created_at_utc)
		VALUES ($1, $2, $3, $4, $5, NULL, now())
	`, uuid.New(), id, o.Status, StatusCancelledByCustomer, changedByUserID)
	if err != nil {
		return fmt.Errorf("insert history: %w", err)
	}

	return nil
}

func (s *Service) ChangeStatus(ctx context.Context, id uuid.UUID, req StatusChangeRequest, changedByUserID uuid.UUID) (*Order, error) {
	o, err := s.GetByID(ctx, id)
	if err != nil {
		return nil, err
	}

	if !IsTransitionAllowed(o.Status, req.ToStatus) {
		return nil, fmt.Errorf("INVALID_TRANSITION")
	}

	_, err = s.pool.Exec(ctx, `UPDATE orders SET status = $2, updated_at_utc = now() WHERE id = $1`, id, req.ToStatus)
	if err != nil {
		return nil, fmt.Errorf("update status: %w", err)
	}

	_, err = s.pool.Exec(ctx, `
		INSERT INTO order_status_history (id, order_id, from_status, to_status, changed_by_user_id, notes, created_at_utc)
		VALUES ($1, $2, $3, $4, $5, $6, now())
	`, uuid.New(), id, o.Status, req.ToStatus, changedByUserID, nullString(req.Notes))
	if err != nil {
		return nil, fmt.Errorf("insert history: %w", err)
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

	var entries []StatusHistoryEntry
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
		SELECT id, order_id, item_type, producto_id, combo_id, quantity, unit_price, subtotal, currency
		FROM order_items WHERE order_id = $1
	`, orderID)
	if err != nil {
		return nil, fmt.Errorf("get items: %w", err)
	}
	defer rows.Close()

	var items []OrderItem
	for rows.Next() {
		var i OrderItem
		if err := rows.Scan(&i.ID, &i.OrderID, &i.ItemType, &i.ProductoID, &i.ComboID, &i.Quantity, &i.UnitPrice, &i.Subtotal, &i.Currency); err != nil {
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
