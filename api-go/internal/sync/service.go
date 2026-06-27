package sync

import (
	"context"
	"fmt"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5/pgxpool"
)

type Service struct {
	pool *pgxpool.Pool
}

func NewService(pool *pgxpool.Pool) *Service {
	return &Service{pool: pool}
}

type SyncProductoRequest struct {
	ProductoID         uuid.UUID `json:"producto_id"`
	Nombre             string    `json:"nombre"`
	Sku                string    `json:"sku"`
	TipoMedidaBase     string    `json:"tipo_medida_base"`
	TipoComercialMayor string    `json:"tipo_comercial_mayor"`
	UnidadesPorCaja    *int      `json:"unidades_por_caja"`
	UnidadesPorBulto   *int      `json:"unidades_por_bulto"`
	Activo             bool      `json:"activo"`
}

type SyncComboRequest struct {
	ComboID            uuid.UUID   `json:"combo_id"`
	Codigo             string      `json:"codigo"`
	Nombre             string      `json:"nombre"`
	Estado             string      `json:"estado"`
	SedeIdsHabilitadas []uuid.UUID `json:"sede_ids_habilitadas"`
	PrecioTotal        float64     `json:"precio_total"`
	PrecioTotalMoneda  string      `json:"precio_total_moneda"`
	PrecioPromocional  *float64    `json:"precio_promocional"`
	PrecioPromocionalMoneda *string `json:"precio_promocional_moneda"`
	Items              []SyncComboItem `json:"items"`
}

type SyncComboItem struct {
	ProductoID uuid.UUID `json:"producto_id"`
	Cantidad   float64   `json:"cantidad"`
}

type SyncResult struct {
	Processed int `json:"processed"`
	Errors    int `json:"errors"`
}

func (s *Service) SyncProductos(ctx context.Context, productos []SyncProductoRequest) (*SyncResult, error) {
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	result := &SyncResult{}
	for _, p := range productos {
		_, err := tx.Exec(ctx, `
			INSERT INTO productos (producto_id, nombre, sku, tipo_medida_base, tipo_comercial_mayor, unidades_por_caja, unidades_por_bulto, activo)
			VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
			ON CONFLICT (sku) DO UPDATE SET
				nombre = EXCLUDED.nombre,
				tipo_medida_base = EXCLUDED.tipo_medida_base,
				tipo_comercial_mayor = EXCLUDED.tipo_comercial_mayor,
				unidades_por_caja = EXCLUDED.unidades_por_caja,
				unidades_por_bulto = EXCLUDED.unidades_por_bulto,
				activo = EXCLUDED.activo
		`, p.ProductoID, p.Nombre, p.Sku, p.TipoMedidaBase, p.TipoComercialMayor, p.UnidadesPorCaja, p.UnidadesPorBulto, p.Activo)
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

func (s *Service) SyncCombos(ctx context.Context, combos []SyncComboRequest) (*SyncResult, error) {
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	result := &SyncResult{}
	for _, c := range combos {
		_, err := tx.Exec(ctx, `
			INSERT INTO combos (combo_id, codigo, nombre, estado, sede_ids_habilitadas, precio_total, precio_total_moneda, precio_promocional, precio_promocional_moneda)
			VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
			ON CONFLICT (codigo) DO UPDATE SET
				nombre = EXCLUDED.nombre,
				estado = EXCLUDED.estado,
				sede_ids_habilitadas = EXCLUDED.sede_ids_habilitadas,
				precio_total = EXCLUDED.precio_total,
				precio_total_moneda = EXCLUDED.precio_total_moneda,
				precio_promocional = EXCLUDED.precio_promocional,
				precio_promocional_moneda = EXCLUDED.precio_promocional_moneda
		`, c.ComboID, c.Codigo, c.Nombre, c.Estado, c.SedeIdsHabilitadas,
			c.PrecioTotal, c.PrecioTotalMoneda, c.PrecioPromocional, c.PrecioPromocionalMoneda)
		if err != nil {
			result.Errors++
			continue
		}

		// Replace combo items
		tx.Exec(ctx, `DELETE FROM combo_items WHERE combo_id = $1`, c.ComboID)
		for _, item := range c.Items {
			_, err := tx.Exec(ctx, `
				INSERT INTO combo_items (id, combo_id, producto_id, cantidad)
				VALUES ($1, $2, $3, $4)
			`, uuid.New(), c.ComboID, item.ProductoID, item.Cantidad)
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
