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

// Producto

type Producto struct {
	ProductoID         uuid.UUID `json:"producto_id"`
	Nombre             string    `json:"nombre"`
	Sku                string    `json:"sku"`
	TipoMedidaBase     string    `json:"tipo_medida_base"`
	TipoComercialMayor string    `json:"tipo_comercial_mayor"`
	UnidadesPorCaja    *int      `json:"unidades_por_caja"`
	UnidadesPorBulto   *int      `json:"unidades_por_bulto"`
	Activo             bool      `json:"activo"`
}

type CreateProductoRequest struct {
	Nombre             string `json:"nombre" validate:"required"`
	Sku                string `json:"sku" validate:"required"`
	TipoMedidaBase     string `json:"tipo_medida_base" validate:"required"`
	TipoComercialMayor string `json:"tipo_comercial_mayor" validate:"required"`
	UnidadesPorCaja    *int   `json:"unidades_por_caja"`
	UnidadesPorBulto   *int   `json:"unidades_por_bulto"`
}

func (s *Service) CreateProducto(ctx context.Context, req CreateProductoRequest) (*Producto, error) {
	if req.TipoMedidaBase == "Unidad" {
		if req.UnidadesPorCaja == nil || *req.UnidadesPorCaja <= 0 {
			return nil, fmt.Errorf("unidadesPorCaja is required for unit-based products")
		}
		if req.UnidadesPorBulto == nil || *req.UnidadesPorBulto <= 0 {
			return nil, fmt.Errorf("unidadesPorBulto is required for unit-based products")
		}
		if *req.UnidadesPorBulto < *req.UnidadesPorCaja {
			return nil, fmt.Errorf("unidadesPorBulto must be >= unidadesPorCaja")
		}
	}

	p := &Producto{}
	err := s.pool.QueryRow(ctx, `
		INSERT INTO productos (producto_id, nombre, sku, tipo_medida_base, tipo_comercial_mayor, unidades_por_caja, unidades_por_bulto, activo)
		VALUES ($1, $2, $3, $4, $5, $6, $7, true)
		RETURNING producto_id, nombre, sku, tipo_medida_base, tipo_comercial_mayor, unidades_por_caja, unidades_por_bulto, activo
	`, uuid.New(), req.Nombre, req.Sku, req.TipoMedidaBase, req.TipoComercialMayor, req.UnidadesPorCaja, req.UnidadesPorBulto).Scan(
		&p.ProductoID, &p.Nombre, &p.Sku, &p.TipoMedidaBase, &p.TipoComercialMayor, &p.UnidadesPorCaja, &p.UnidadesPorBulto, &p.Activo,
	)
	if err != nil {
		return nil, fmt.Errorf("create producto: %w", err)
	}
	return p, nil
}

func (s *Service) GetProductoByID(ctx context.Context, id uuid.UUID) (*Producto, error) {
	p := &Producto{}
	err := s.pool.QueryRow(ctx, `
		SELECT producto_id, nombre, sku, tipo_medida_base, tipo_comercial_mayor, unidades_por_caja, unidades_por_bulto, activo
		FROM productos WHERE producto_id = $1
	`, id).Scan(&p.ProductoID, &p.Nombre, &p.Sku, &p.TipoMedidaBase, &p.TipoComercialMayor, &p.UnidadesPorCaja, &p.UnidadesPorBulto, &p.Activo)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get producto: %w", err)
	}
	return p, nil
}

func (s *Service) ListProductos(ctx context.Context) ([]Producto, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT producto_id, nombre, sku, tipo_medida_base, tipo_comercial_mayor, unidades_por_caja, unidades_por_bulto, activo
		FROM productos ORDER BY nombre
	`)
	if err != nil {
		return nil, fmt.Errorf("list productos: %w", err)
	}
	defer rows.Close()

	var productos []Producto
	for rows.Next() {
		var p Producto
		if err := rows.Scan(&p.ProductoID, &p.Nombre, &p.Sku, &p.TipoMedidaBase, &p.TipoComercialMayor, &p.UnidadesPorCaja, &p.UnidadesPorBulto, &p.Activo); err != nil {
			return nil, fmt.Errorf("scan producto: %w", err)
		}
		productos = append(productos, p)
	}
	return productos, nil
}

func (s *Service) UpdateProducto(ctx context.Context, id uuid.UUID, req CreateProductoRequest) (*Producto, error) {
	p := &Producto{}
	err := s.pool.QueryRow(ctx, `
		UPDATE productos SET nombre = $2, sku = $3, tipo_medida_base = $4, tipo_comercial_mayor = $5, unidades_por_caja = $6, unidades_por_bulto = $7
		WHERE producto_id = $1
		RETURNING producto_id, nombre, sku, tipo_medida_base, tipo_comercial_mayor, unidades_por_caja, unidades_por_bulto, activo
	`, id, req.Nombre, req.Sku, req.TipoMedidaBase, req.TipoComercialMayor, req.UnidadesPorCaja, req.UnidadesPorBulto).Scan(
		&p.ProductoID, &p.Nombre, &p.Sku, &p.TipoMedidaBase, &p.TipoComercialMayor, &p.UnidadesPorCaja, &p.UnidadesPorBulto, &p.Activo,
	)
	if err != nil {
		return nil, fmt.Errorf("update producto: %w", err)
	}
	return p, nil
}

func (s *Service) DeleteProducto(ctx context.Context, id uuid.UUID) error {
	_, err := s.pool.Exec(ctx, `DELETE FROM productos WHERE producto_id = $1`, id)
	if err != nil {
		return fmt.Errorf("delete producto: %w", err)
	}
	return nil
}

// Combo

type Combo struct {
	ComboID              uuid.UUID   `json:"combo_id"`
	Codigo               string      `json:"codigo"`
	Nombre               string      `json:"nombre"`
	Estado               string      `json:"estado"`
	SedeIdsHabilitadas   []uuid.UUID `json:"sede_ids_habilitadas"`
	PrecioTotal          float64     `json:"precio_total"`
	PrecioTotalMoneda    string      `json:"precio_total_moneda"`
	PrecioPromocional    *float64    `json:"precio_promocional"`
	PrecioPromocionalMoneda *string  `json:"precio_promocional_moneda"`
	Items                []ComboItem `json:"items"`
}

type ComboItem struct {
	ID         uuid.UUID `json:"id"`
	ComboID    uuid.UUID `json:"combo_id"`
	ProductoID uuid.UUID `json:"producto_id"`
	Cantidad   float64   `json:"cantidad"`
}

type CreateComboRequest struct {
	Codigo             string      `json:"codigo" validate:"required"`
	Nombre             string      `json:"nombre" validate:"required"`
	Items              []ComboItemReq `json:"items" validate:"required"`
	SedeIdsHabilitadas []uuid.UUID `json:"sede_ids_habilitadas"`
}

type ComboItemReq struct {
	ProductoID uuid.UUID `json:"producto_id" validate:"required"`
	Cantidad   float64   `json:"cantidad" validate:"required,gt=0"`
}

func (s *Service) CreateCombo(ctx context.Context, req CreateComboRequest) (*Combo, error) {
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	combo := &Combo{}
	err = tx.QueryRow(ctx, `
		INSERT INTO combos (combo_id, codigo, nombre, estado, sede_ids_habilitadas, precio_total, precio_total_moneda)
		VALUES ($1, $2, $3, 'Borrador', $4, 0, 'USD')
		RETURNING combo_id, codigo, nombre, estado, sede_ids_habilitadas, precio_total, precio_total_moneda, precio_promocional, precio_promocional_moneda
	`, uuid.New(), req.Codigo, req.Nombre, req.SedeIdsHabilitadas).Scan(
		&combo.ComboID, &combo.Codigo, &combo.Nombre, &combo.Estado, &combo.SedeIdsHabilitadas,
		&combo.PrecioTotal, &combo.PrecioTotalMoneda, &combo.PrecioPromocional, &combo.PrecioPromocionalMoneda,
	)
	if err != nil {
		return nil, fmt.Errorf("create combo: %w", err)
	}

	for _, item := range req.Items {
		var ci ComboItem
		err = tx.QueryRow(ctx, `
			INSERT INTO combo_items (id, combo_id, producto_id, cantidad)
			VALUES ($1, $2, $3, $4)
			RETURNING id, combo_id, producto_id, cantidad
		`, uuid.New(), combo.ComboID, item.ProductoID, item.Cantidad).Scan(
			&ci.ID, &ci.ComboID, &ci.ProductoID, &ci.Cantidad,
		)
		if err != nil {
			return nil, fmt.Errorf("create combo item: %w", err)
		}
		combo.Items = append(combo.Items, ci)
	}

	if err := tx.Commit(ctx); err != nil {
		return nil, fmt.Errorf("commit: %w", err)
	}

	return combo, nil
}

func (s *Service) GetComboByID(ctx context.Context, id uuid.UUID) (*Combo, error) {
	combo := &Combo{}
	err := s.pool.QueryRow(ctx, `
		SELECT combo_id, codigo, nombre, estado, sede_ids_habilitadas, precio_total, precio_total_moneda, precio_promocional, precio_promocional_moneda
		FROM combos WHERE combo_id = $1
	`, id).Scan(
		&combo.ComboID, &combo.Codigo, &combo.Nombre, &combo.Estado, &combo.SedeIdsHabilitadas,
		&combo.PrecioTotal, &combo.PrecioTotalMoneda, &combo.PrecioPromocional, &combo.PrecioPromocionalMoneda,
	)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get combo: %w", err)
	}

	// Load items
	rows, err := s.pool.Query(ctx, `SELECT id, combo_id, producto_id, cantidad FROM combo_items WHERE combo_id = $1`, id)
	if err != nil {
		return nil, fmt.Errorf("get combo items: %w", err)
	}
	defer rows.Close()

	for rows.Next() {
		var ci ComboItem
		if err := rows.Scan(&ci.ID, &ci.ComboID, &ci.ProductoID, &ci.Cantidad); err != nil {
			return nil, fmt.Errorf("scan combo item: %w", err)
		}
		combo.Items = append(combo.Items, ci)
	}

	return combo, nil
}

func (s *Service) ListCombos(ctx context.Context) ([]Combo, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT combo_id, codigo, nombre, estado, sede_ids_habilitadas, precio_total, precio_total_moneda, precio_promocional, precio_promocional_moneda
		FROM combos ORDER BY nombre
	`)
	if err != nil {
		return nil, fmt.Errorf("list combos: %w", err)
	}
	defer rows.Close()

	var combos []Combo
	for rows.Next() {
		var c Combo
		if err := rows.Scan(&c.ComboID, &c.Codigo, &c.Nombre, &c.Estado, &c.SedeIdsHabilitadas,
			&c.PrecioTotal, &c.PrecioTotalMoneda, &c.PrecioPromocional, &c.PrecioPromocionalMoneda); err != nil {
			return nil, fmt.Errorf("scan combo: %w", err)
		}
		combos = append(combos, c)
	}
	return combos, nil
}

func (s *Service) UpdateCombo(ctx context.Context, id uuid.UUID, req CreateComboRequest) (*Combo, error) {
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	combo := &Combo{}
	err = tx.QueryRow(ctx, `
		UPDATE combos SET codigo = $2, nombre = $3, sede_ids_habilitadas = $4
		WHERE combo_id = $1
		RETURNING combo_id, codigo, nombre, estado, sede_ids_habilitadas, precio_total, precio_total_moneda, precio_promocional, precio_promocional_moneda
	`, id, req.Codigo, req.Nombre, req.SedeIdsHabilitadas).Scan(
		&combo.ComboID, &combo.Codigo, &combo.Nombre, &combo.Estado, &combo.SedeIdsHabilitadas,
		&combo.PrecioTotal, &combo.PrecioTotalMoneda, &combo.PrecioPromocional, &combo.PrecioPromocionalMoneda,
	)
	if err != nil {
		return nil, fmt.Errorf("update combo: %w", err)
	}

	// Replace items
	tx.Exec(ctx, `DELETE FROM combo_items WHERE combo_id = $1`, id)

	for _, item := range req.Items {
		var ci ComboItem
		err = tx.QueryRow(ctx, `
			INSERT INTO combo_items (id, combo_id, producto_id, cantidad)
			VALUES ($1, $2, $3, $4)
			RETURNING id, combo_id, producto_id, cantidad
		`, uuid.New(), combo.ComboID, item.ProductoID, item.Cantidad).Scan(
			&ci.ID, &ci.ComboID, &ci.ProductoID, &ci.Cantidad,
		)
		if err != nil {
			return nil, fmt.Errorf("create combo item: %w", err)
		}
		combo.Items = append(combo.Items, ci)
	}

	if err := tx.Commit(ctx); err != nil {
		return nil, fmt.Errorf("commit: %w", err)
	}

	return combo, nil
}

func (s *Service) DeleteCombo(ctx context.Context, id uuid.UUID) error {
	_, err := s.pool.Exec(ctx, `DELETE FROM combos WHERE combo_id = $1`, id)
	if err != nil {
		return fmt.Errorf("delete combo: %w", err)
	}
	return nil
}

func (s *Service) UpdateComboEstado(ctx context.Context, id uuid.UUID, estado string) error {
	_, err := s.pool.Exec(ctx, `UPDATE combos SET estado = $2 WHERE combo_id = $1`, id, estado)
	if err != nil {
		return fmt.Errorf("update combo estado: %w", err)
	}
	return nil
}

// Precio

type PrecioProductoSede struct {
	PrecioID                uuid.UUID `json:"precio_producto_sede_id"`
	ProductoID              uuid.UUID `json:"producto_id"`
	SedeID                  uuid.UUID `json:"sede_id"`
	Precio1Unidad           float64   `json:"precio1_unidad"`
	Precio1Moneda           string    `json:"precio1_moneda"`
	Precio2CajaBultoPieza   float64   `json:"precio2_caja_bulto_pieza"`
	Precio2Moneda           string    `json:"precio2_moneda"`
	Precio3MayorDesde2      float64   `json:"precio3_mayor_desde2"`
	Precio3Moneda           string    `json:"precio3_moneda"`
	Precio4Mayorista        *float64  `json:"precio4_mayorista"`
	Precio4Moneda           *string   `json:"precio4_moneda"`
	Precio4RequiereAcuerdo  bool      `json:"precio4_requiere_acuerdo"`
	VigenteDesde            time.Time `json:"vigente_desde"`
	VigenteHasta            *time.Time `json:"vigente_hasta"`
}

type CreatePrecioRequest struct {
	ProductoID             uuid.UUID  `json:"producto_id" validate:"required"`
	SedeID                 uuid.UUID  `json:"sede_id" validate:"required"`
	Precio1Unidad          float64    `json:"precio1_unidad" validate:"required"`
	Precio2CajaBultoPieza  float64    `json:"precio2_caja_bulto_pieza" validate:"required"`
	Precio3MayorDesde2     float64    `json:"precio3_mayor_desde2" validate:"required"`
	Precio4Mayorista       *float64   `json:"precio4_mayorista"`
	Precio4RequiereAcuerdo bool       `json:"precio4_requiere_acuerdo"`
	VigenteDesde           time.Time  `json:"vigente_desde" validate:"required"`
	VigenteHasta           *time.Time `json:"vigente_hasta"`
}

func (s *Service) CreatePrecio(ctx context.Context, req CreatePrecioRequest) (*PrecioProductoSede, error) {
	p := &PrecioProductoSede{}
	moneda := "USD"
	err := s.pool.QueryRow(ctx, `
		INSERT INTO precios_producto_sede (precio_producto_sede_id, producto_id, sede_id, precio1_unidad, precio1_moneda, precio2_caja_bulto_pieza, precio2_moneda, precio3_mayor_desde2, precio3_moneda, precio4_mayorista, precio4_moneda, precio4_requiere_acuerdo, vigente_desde, vigente_hasta)
		VALUES ($1, $2, $3, $4, $5, $6, $5, $7, $5, $8, $5, $9, $10, $11)
		RETURNING precio_producto_sede_id, producto_id, sede_id, precio1_unidad, precio1_moneda, precio2_caja_bulto_pieza, precio2_moneda, precio3_mayor_desde2, precio3_moneda, precio4_mayorista, precio4_moneda, precio4_requiere_acuerdo, vigente_desde, vigente_hasta
	`, uuid.New(), req.ProductoID, req.SedeID, req.Precio1Unidad, moneda, req.Precio2CajaBultoPieza,
		req.Precio3MayorDesde2, req.Precio4Mayorista, req.Precio4RequiereAcuerdo, req.VigenteDesde, req.VigenteHasta).Scan(
		&p.PrecioID, &p.ProductoID, &p.SedeID, &p.Precio1Unidad, &p.Precio1Moneda,
		&p.Precio2CajaBultoPieza, &p.Precio2Moneda, &p.Precio3MayorDesde2, &p.Precio3Moneda,
		&p.Precio4Mayorista, &p.Precio4Moneda, &p.Precio4RequiereAcuerdo, &p.VigenteDesde, &p.VigenteHasta,
	)
	if err != nil {
		return nil, fmt.Errorf("create precio: %w", err)
	}
	return p, nil
}

func (s *Service) GetPrecioByID(ctx context.Context, id uuid.UUID) (*PrecioProductoSede, error) {
	p := &PrecioProductoSede{}
	err := s.pool.QueryRow(ctx, `
		SELECT precio_producto_sede_id, producto_id, sede_id, precio1_unidad, precio1_moneda, precio2_caja_bulto_pieza, precio2_moneda, precio3_mayor_desde2, precio3_moneda, precio4_mayorista, precio4_moneda, precio4_requiere_acuerdo, vigente_desde, vigente_hasta
		FROM precios_producto_sede WHERE precio_producto_sede_id = $1
	`, id).Scan(
		&p.PrecioID, &p.ProductoID, &p.SedeID, &p.Precio1Unidad, &p.Precio1Moneda,
		&p.Precio2CajaBultoPieza, &p.Precio2Moneda, &p.Precio3MayorDesde2, &p.Precio3Moneda,
		&p.Precio4Mayorista, &p.Precio4Moneda, &p.Precio4RequiereAcuerdo, &p.VigenteDesde, &p.VigenteHasta,
	)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get precio: %w", err)
	}
	return p, nil
}

func (s *Service) ListPreciosByProductoID(ctx context.Context, productoID uuid.UUID) ([]PrecioProductoSede, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT precio_producto_sede_id, producto_id, sede_id, precio1_unidad, precio1_moneda, precio2_caja_bulto_pieza, precio2_moneda, precio3_mayor_desde2, precio3_moneda, precio4_mayorista, precio4_moneda, precio4_requiere_acuerdo, vigente_desde, vigente_hasta
		FROM precios_producto_sede WHERE producto_id = $1 ORDER BY vigente_desde DESC
	`, productoID)
	if err != nil {
		return nil, fmt.Errorf("list precios: %w", err)
	}
	defer rows.Close()

	var precios []PrecioProductoSede
	for rows.Next() {
		var p PrecioProductoSede
		if err := rows.Scan(&p.PrecioID, &p.ProductoID, &p.SedeID, &p.Precio1Unidad, &p.Precio1Moneda,
			&p.Precio2CajaBultoPieza, &p.Precio2Moneda, &p.Precio3MayorDesde2, &p.Precio3Moneda,
			&p.Precio4Mayorista, &p.Precio4Moneda, &p.Precio4RequiereAcuerdo, &p.VigenteDesde, &p.VigenteHasta); err != nil {
			return nil, fmt.Errorf("scan precio: %w", err)
		}
		precios = append(precios, p)
	}
	return precios, nil
}

func (s *Service) ListPreciosBySedeID(ctx context.Context, sedeID uuid.UUID) ([]PrecioProductoSede, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT precio_producto_sede_id, producto_id, sede_id, precio1_unidad, precio1_moneda, precio2_caja_bulto_pieza, precio2_moneda, precio3_mayor_desde2, precio3_moneda, precio4_mayorista, precio4_moneda, precio4_requiere_acuerdo, vigente_desde, vigente_hasta
		FROM precios_producto_sede WHERE sede_id = $1 ORDER BY vigente_desde DESC
	`, sedeID)
	if err != nil {
		return nil, fmt.Errorf("list precios: %w", err)
	}
	defer rows.Close()

	var precios []PrecioProductoSede
	for rows.Next() {
		var p PrecioProductoSede
		if err := rows.Scan(&p.PrecioID, &p.ProductoID, &p.SedeID, &p.Precio1Unidad, &p.Precio1Moneda,
			&p.Precio2CajaBultoPieza, &p.Precio2Moneda, &p.Precio3MayorDesde2, &p.Precio3Moneda,
			&p.Precio4Mayorista, &p.Precio4Moneda, &p.Precio4RequiereAcuerdo, &p.VigenteDesde, &p.VigenteHasta); err != nil {
			return nil, fmt.Errorf("scan precio: %w", err)
		}
		precios = append(precios, p)
	}
	return precios, nil
}

func (s *Service) UpdatePrecio(ctx context.Context, id uuid.UUID, req CreatePrecioRequest) (*PrecioProductoSede, error) {
	moneda := "USD"
	p := &PrecioProductoSede{}
	err := s.pool.QueryRow(ctx, `
		UPDATE precios_producto_sede SET precio1_unidad = $2, precio1_moneda = $3, precio2_caja_bulto_pieza = $4, precio2_moneda = $3, precio3_mayor_desde2 = $5, precio3_moneda = $3, precio4_mayorista = $6, precio4_moneda = $3, precio4_requiere_acuerdo = $7, vigente_desde = $8, vigente_hasta = $9
		WHERE precio_producto_sede_id = $1
		RETURNING precio_producto_sede_id, producto_id, sede_id, precio1_unidad, precio1_moneda, precio2_caja_bulto_pieza, precio2_moneda, precio3_mayor_desde2, precio3_moneda, precio4_mayorista, precio4_moneda, precio4_requiere_acuerdo, vigente_desde, vigente_hasta
	`, id, req.Precio1Unidad, moneda, req.Precio2CajaBultoPieza, req.Precio3MayorDesde2,
		req.Precio4Mayorista, req.Precio4RequiereAcuerdo, req.VigenteDesde, req.VigenteHasta).Scan(
		&p.PrecioID, &p.ProductoID, &p.SedeID, &p.Precio1Unidad, &p.Precio1Moneda,
		&p.Precio2CajaBultoPieza, &p.Precio2Moneda, &p.Precio3MayorDesde2, &p.Precio3Moneda,
		&p.Precio4Mayorista, &p.Precio4Moneda, &p.Precio4RequiereAcuerdo, &p.VigenteDesde, &p.VigenteHasta,
	)
	if err != nil {
		return nil, fmt.Errorf("update precio: %w", err)
	}
	return p, nil
}

func (s *Service) DeletePrecio(ctx context.Context, id uuid.UUID) error {
	_, err := s.pool.Exec(ctx, `DELETE FROM precios_producto_sede WHERE precio_producto_sede_id = $1`, id)
	if err != nil {
		return fmt.Errorf("delete precio: %w", err)
	}
	return nil
}
