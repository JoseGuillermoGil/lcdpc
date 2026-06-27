package sede

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

type Sede struct {
	ID                         uuid.UUID  `json:"id"`
	NombreTienda               string     `json:"nombre_tienda"`
	Rif                        string     `json:"rif"`
	Direccion                  string     `json:"direccion"`
	TelefonoContacto           string     `json:"telefono_contacto"`
	TelefonoContactoSecundario *string    `json:"telefono_contacto_secundario"`
	HorarioAtencion            string     `json:"horario_atencion"`
	CreatedAtUtc               time.Time  `json:"created_at_utc"`
	UpdatedAtUtc               time.Time  `json:"updated_at_utc"`
}

type CreateSedeRequest struct {
	NombreTienda               string `json:"nombre_tienda" validate:"required"`
	Rif                        string `json:"rif" validate:"required"`
	Direccion                  string `json:"direccion" validate:"required"`
	TelefonoContacto           string `json:"telefono_contacto" validate:"required"`
	TelefonoContactoSecundario string `json:"telefono_contacto_secundario"`
	HorarioAtencion            string `json:"horario_atencion" validate:"required"`
}

func (s *Service) Create(ctx context.Context, req CreateSedeRequest) (*Sede, error) {
	sede := &Sede{}
	var secPhone *string
	if req.TelefonoContactoSecundario != "" {
		secPhone = &req.TelefonoContactoSecundario
	}

	err := s.pool.QueryRow(ctx, `
		INSERT INTO sedes (id, nombre_tienda, rif, direccion, telefono_contacto, telefono_contacto_secundario, horario_atencion, created_at_utc, updated_at_utc)
		VALUES ($1, $2, $3, $4, $5, $6, $7, now(), now())
		RETURNING id, nombre_tienda, rif, direccion, telefono_contacto, telefono_contacto_secundario, horario_atencion, created_at_utc, updated_at_utc
	`, uuid.New(), req.NombreTienda, req.Rif, req.Direccion, req.TelefonoContacto, secPhone, req.HorarioAtencion).Scan(
		&sede.ID, &sede.NombreTienda, &sede.Rif, &sede.Direccion, &sede.TelefonoContacto,
		&sede.TelefonoContactoSecundario, &sede.HorarioAtencion, &sede.CreatedAtUtc, &sede.UpdatedAtUtc,
	)
	if err != nil {
		return nil, fmt.Errorf("create sede: %w", err)
	}
	return sede, nil
}

func (s *Service) List(ctx context.Context) ([]Sede, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT id, nombre_tienda, rif, direccion, telefono_contacto, telefono_contacto_secundario, horario_atencion, created_at_utc, updated_at_utc
		FROM sedes ORDER BY nombre_tienda
	`)
	if err != nil {
		return nil, fmt.Errorf("list sedes: %w", err)
	}
	defer rows.Close()

	var sedes []Sede
	for rows.Next() {
		var sede Sede
		if err := rows.Scan(&sede.ID, &sede.NombreTienda, &sede.Rif, &sede.Direccion, &sede.TelefonoContacto,
			&sede.TelefonoContactoSecundario, &sede.HorarioAtencion, &sede.CreatedAtUtc, &sede.UpdatedAtUtc); err != nil {
			return nil, fmt.Errorf("scan sede: %w", err)
		}
		sedes = append(sedes, sede)
	}
	return sedes, nil
}

func (s *Service) GetByID(ctx context.Context, id uuid.UUID) (*Sede, error) {
	sede := &Sede{}
	err := s.pool.QueryRow(ctx, `
		SELECT id, nombre_tienda, rif, direccion, telefono_contacto, telefono_contacto_secundario, horario_atencion, created_at_utc, updated_at_utc
		FROM sedes WHERE id = $1
	`, id).Scan(
		&sede.ID, &sede.NombreTienda, &sede.Rif, &sede.Direccion, &sede.TelefonoContacto,
		&sede.TelefonoContactoSecundario, &sede.HorarioAtencion, &sede.CreatedAtUtc, &sede.UpdatedAtUtc,
	)
	if err == pgx.ErrNoRows {
		return nil, fmt.Errorf("NOT_FOUND")
	}
	if err != nil {
		return nil, fmt.Errorf("get sede: %w", err)
	}
	return sede, nil
}
