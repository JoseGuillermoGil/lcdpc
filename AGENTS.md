# AGENTS

High-signal guidance for OpenCode sessions in this repo.

## What is actually here

- Monorepo with three real work areas:
  - `api/LCDPC/` — Go backend, PASETO v2.local auth, pgx + PostgreSQL.
  - `web/` — Angular 20 standalone app with signals, PrimeNG 20, SCSS, pnpm.
  - `docs/` — architecture + SDD artifacts that drive feature work.

## Fast commands

- Backend with Docker (recommended when auth/db matters):
  - `cd api/LCDPC && cp .env.example .env && docker compose up --build`
- Backend local:
  - `cd api/LCDPC && go run cmd/server/main.go`
- Backend tests:
  - `cd api/LCDPC && go test ./...`
- Backend build:
  - `cd api/LCDPC && go build -o lcdpc-server cmd/server/main.go`
- Frontend:
  - `cd web && pnpm install`
  - `cd web && pnpm start`
  - `cd web && pnpm test`
  - `cd web && pnpm build`

## Read these before editing

- `README.md` — repo entrypoint and env vars.
- `docs/backend/architecture/fase-1-blueprint.md` — authoritative layer rules.
- `docs/sdd/README.md` + matching `docs/sdd/changes/<change>/` artifacts — required before feature work; update the tasks checklist after implementation.
- `/.agents/skills/primeng/SKILL.md` — load this before PrimeNG/UI work.
- `/.agents/skills/golang/SKILL.md` — load this before Go backend work.

## Architecture rules you should not break

- Feature-based packages in `internal/` (no Clean Architecture layering).
- No repository wrappers around sqlc — use `*db.Queries` directly.
- Interfaces are defined in the consuming package, not the implementing package.
- Auth tokens are PASETO v2.local (XChaCha20-Poly1305). The payload is encrypted and opaque to clients.
- Cookies `lcdpc_at` and `lcdpc_rt` are HTTP-only for legacy compatibility.
- Role checks use `RequirePermission(store, "resource:action")` middleware; preserve that flow when adding protected endpoints.
- CORS must keep `AllowCredentials()` for auth to work cross-origin.
- Seeders run on startup (superuser, oauth2 client, api token). Do not introduce migration runners unless asked.
- API responses use JSend format (`{"status":"success","data":{}}`).

## Backend facts agents often guess wrong

- Auth uses PASETO v2.local, NOT JWT. Do not add JWT libraries or `/.well-known/jwks.json`.
- A valid PASETO token is enough for auth — no DB lookup needed for access token validity.
- Refresh token rotation with family-based theft detection is implemented in `internal/auth/oauth2.go`.
- Password hashing is PBKDF2-SHA256, 100k iterations, compatible with the previous C# hashes.
- The OAuth2 server is custom-built (authorize, token, introspect, revoke). It is NOT using `golang.org/x/oauth2` as a server.
- `golang.org/x/oauth2` is used only as a Google OAuth **client**.
- Package structure: `internal/auth/` (auth + OAuth2), `internal/pricing/` (productos, combos, precios), `internal/sede/`, `internal/sync/`, `internal/email/`, `internal/db/`, `internal/rbac/` (RBAC store + CRUD).

## Frontend facts agents often guess wrong

- The real frontend root is `web/`, not `api/LCDPC/web/`.
- `web/` is not scaffold-only: it already has standalone components, route wiring, PrimeNG theme setup, and active auth/register flows.
- Package-manager rule: use `pnpm` only for JS/TS work unless the user explicitly approves otherwise.
- Existing route/UI language is Spanish (`/autenticacion`, `/registro`); extend the current app vocabulary instead of anglicizing it mid-feature.

## Repo gotchas

- No `.github/` workflows are present; do not invent CI expectations.
- `docs/` contains SDD artifacts; read them before feature work.
- `go_plan_finalized.md` contains the migration analysis (C# vs Go trade-offs).
