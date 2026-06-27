-- Sede queries

-- name: CreateSede :one
INSERT INTO sedes (id, nombre_tienda, rif, direccion, telefono_contacto, telefono_contacto_secundario, horario_atencion, created_at_utc, updated_at_utc)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
RETURNING id, nombre_tienda, rif, direccion, telefono_contacto, telefono_contacto_secundario, horario_atencion, created_at_utc, updated_at_utc;

-- name: GetSedeByID :one
SELECT id, nombre_tienda, rif, direccion, telefono_contacto, telefono_contacto_secundario, horario_atencion, created_at_utc, updated_at_utc
FROM sedes
WHERE id = $1;

-- name: ListSedes :many
SELECT id, nombre_tienda, rif, direccion, telefono_contacto, telefono_contacto_secundario, horario_atencion, created_at_utc, updated_at_utc
FROM sedes
ORDER BY nombre_tienda;

-- name: UpdateSede :one
UPDATE sedes
SET nombre_tienda = $2, rif = $3, direccion = $4, telefono_contacto = $5, telefono_contacto_secundario = $6, horario_atencion = $7, updated_at_utc = now()
WHERE id = $1
RETURNING id, nombre_tienda, rif, direccion, telefono_contacto, telefono_contacto_secundario, horario_atencion, created_at_utc, updated_at_utc;

-- name: DeleteSede :exec
DELETE FROM sedes WHERE id = $1;
