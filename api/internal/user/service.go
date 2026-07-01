package user

import (
	"context"
	"fmt"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5/pgxpool"
)

const (
	defaultLimit = 10
	maxLimit     = 100
)

type UserModel struct {
	ID        uuid.UUID `json:"id"`
	Email     string    `json:"email"`
	FirstName *string   `json:"first_name"`
	LastName  *string   `json:"last_name"`
	Status    string    `json:"status"`
	CreatedAt time.Time `json:"created_at_utc"`
}

type Service struct {
	pool *pgxpool.Pool
}

func NewService(pool *pgxpool.Pool) *Service {
	return &Service{pool: pool}
}

func (s *Service) List(ctx context.Context, limit, offset int) ([]UserModel, int, error) {
	if limit <= 0 {
		limit = defaultLimit
	}
	if limit > maxLimit {
		limit = maxLimit
	}
	if offset < 0 {
		offset = 0
	}

	var totalCount int
	err := s.pool.QueryRow(ctx, `SELECT COUNT(*) FROM users`).Scan(&totalCount)
	if err != nil {
		return nil, 0, fmt.Errorf("count users: %w", err)
	}

	rows, err := s.pool.Query(ctx, `
		SELECT u.id, u.email, u.status, u.created_at_utc,
		       p.first_name, p.last_name
		FROM users u
		LEFT JOIN profiles p ON p.user_id = u.id
		ORDER BY u.created_at_utc DESC
		LIMIT $1 OFFSET $2
	`, limit, offset)
	if err != nil {
		return nil, 0, fmt.Errorf("list users: %w", err)
	}
	defer rows.Close()

	users := make([]UserModel, 0)
	for rows.Next() {
		var u UserModel
		if err := rows.Scan(&u.ID, &u.Email, &u.Status, &u.CreatedAt, &u.FirstName, &u.LastName); err != nil {
			return nil, 0, fmt.Errorf("scan user: %w", err)
		}
		users = append(users, u)
	}
	return users, totalCount, nil
}

func (s *Service) GetByDocument(ctx context.Context, document string) (*UserModel, error) {
	var u UserModel
	err := s.pool.QueryRow(ctx, `
		SELECT u.id, u.email, u.status, u.created_at_utc,
		       p.first_name, p.last_name
		FROM users u
		LEFT JOIN profiles p ON p.user_id = u.id
		WHERE p.identity_document = $1
	`, document).Scan(&u.ID, &u.Email, &u.Status, &u.CreatedAt, &u.FirstName, &u.LastName)
	if err != nil {
		return nil, fmt.Errorf("user not found by document: %w", err)
	}
	return &u, nil
}

func (s *Service) Search(ctx context.Context, query string, limit, offset int) ([]UserModel, int, error) {
	if limit <= 0 {
		limit = defaultLimit
	}
	if limit > maxLimit {
		limit = maxLimit
	}
	if offset < 0 {
		offset = 0
	}

	countQuery := `
		SELECT COUNT(*)
		FROM users u
		LEFT JOIN profiles p ON p.user_id = u.id
		WHERE u.email ILIKE '%' || $1 || '%'
		   OR p.first_name ILIKE '%' || $1 || '%'
		   OR p.last_name ILIKE '%' || $1 || '%'`

	var totalCount int
	err := s.pool.QueryRow(ctx, countQuery, query).Scan(&totalCount)
	if err != nil {
		return nil, 0, fmt.Errorf("count users: %w", err)
	}

	dataQuery := `
		SELECT u.id, u.email, u.status, u.created_at_utc,
		       p.first_name, p.last_name
		FROM users u
		LEFT JOIN profiles p ON p.user_id = u.id
		WHERE u.email ILIKE '%' || $1 || '%'
		   OR p.first_name ILIKE '%' || $1 || '%'
		   OR p.last_name ILIKE '%' || $1 || '%'
		ORDER BY u.created_at_utc DESC
		LIMIT $2 OFFSET $3`

	rows, err := s.pool.Query(ctx, dataQuery, query, limit, offset)
	if err != nil {
		return nil, 0, fmt.Errorf("search users: %w", err)
	}
	defer rows.Close()

	users := make([]UserModel, 0)
	for rows.Next() {
		var u UserModel
		if err := rows.Scan(&u.ID, &u.Email, &u.Status, &u.CreatedAt, &u.FirstName, &u.LastName); err != nil {
			return nil, 0, fmt.Errorf("scan user: %w", err)
		}
		users = append(users, u)
	}
	return users, totalCount, nil
}
