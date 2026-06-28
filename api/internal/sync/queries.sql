-- Sync queries (bulk upsert operations)

-- name: BulkUpsertProductos :copyfrom
INSERT INTO productos (producto_id, nombre, sku, tipo_medida_base, tipo_comercial_mayor, unidades_por_caja, unidades_por_bulto, activo)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8);

-- name: BulkUpsertCombos :copyfrom
INSERT INTO combos (combo_id, codigo, nombre, estado, sede_ids_habilitadas, precio_total, precio_total_moneda, precio_promocional, precio_promocional_moneda)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9);

-- name: DeleteAllComboItemsByComboID :exec
DELETE FROM combo_items WHERE combo_id = $1;

-- name: BulkInsertComboItems :copyfrom
INSERT INTO combo_items (id, combo_id, producto_id, cantidad)
VALUES ($1, $2, $3, $4);
