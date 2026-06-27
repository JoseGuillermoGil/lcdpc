-- Admin queries (for admin operations)

-- name: ListUsersWithProfiles :many
SELECT u.id, u.email, u.onboarding_status, u.status, u.created_at_utc,
       p.first_name, p.last_name, p.identity_document, p.whatsapp_phone
FROM users u
LEFT JOIN profiles p ON p.user_id = u.id
ORDER BY u.created_at_utc DESC
LIMIT $1 OFFSET $2;

-- name: AdminUpdateUserStatus :exec
UPDATE users SET status = $2 WHERE id = $1;

-- name: AdminAssignRole :exec
INSERT INTO user_role_assignments (id, user_id, role_id, sede_ids, active, created_at_utc)
VALUES ($1, $2, $3, $4, $5, $6);

-- name: AdminGetUserRoleAssignments :many
SELECT ura.id, ura.role_id, r.code as role_code, ura.sede_ids, ura.active
FROM user_role_assignments ura
JOIN roles r ON r.id = ura.role_id
WHERE ura.user_id = $1;

-- name: GetRolesByUserID :many
SELECT r.code
FROM user_role_assignments ura
JOIN roles r ON r.id = ura.role_id
WHERE ura.user_id = $1 AND ura.active = true;
