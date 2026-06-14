# AGENTS

High-signal guidance for OpenCode sessions in this repo.

## What is actually here

- Monorepo with three real work areas:
  - `api/LCDPC/` — .NET 10 backend, Clean Architecture, EF Core + PostgreSQL.
  - `web/` — Angular 20 standalone app with signals, PrimeNG 20, SCSS, pnpm.
  - `docs/` — architecture + SDD artifacts that drive feature work.
- There is **no committed** root `opencode.json`, `.github/copilot-instructions.md`, or tracked `.vscode/` workspace config. Root `.vscode/` is gitignored.

## Fast commands

- Backend with Docker (recommended when auth/db matters):
  - `cd api/LCDPC && cp .env.example .env && docker compose up --build`
- Backend local:
  - `cd api/LCDPC && dotnet run --project LCDPC.API/LCDPC.API.csproj`
- Backend tests:
  - `dotnet test api/LCDPC/LCDPC.slnx`
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

## Architecture rules you should not break

- `LCDPC.Domain` must stay dependency-free.
- `LCDPC.Application` may depend only on `LCDPC.Domain`.
- `LCDPC.Infrastructure` may depend on `LCDPC.Application` + `LCDPC.Domain`.
- `LCDPC.API` may depend on the other three.
- Controllers must not talk to repositories directly.
- DI entrypoints are `AddApplicationServices()` and `AddInfrastructureServices(configuration)`.

## Backend facts agents often guess wrong

- Auth is cookie-based JWT: `lcdpc_at` and `lcdpc_rt` are HTTP-only cookies.
- A valid JWT is **not enough**: each request also validates the access-token hash against `UserSessions` in the DB.
- Role checks use `[RequireRoles(...)]`; preserve that flow when adding protected endpoints.
- CORS must keep `AllowCredentials()` for auth to work cross-origin.
- Startup uses `Database.EnsureCreated()` and seeds the superuser on boot. **Do not introduce EF migrations unless the user asks for that change.**
- API responses already use `JSendResponse`; follow the existing response shape in the touched area.

## Frontend facts agents often guess wrong

- The real frontend root is `web/`, not `api/LCDPC/web/`.
- `web/` is not scaffold-only: it already has standalone components, route wiring, PrimeNG theme setup, and active auth/register flows.
- Package-manager rule: use `pnpm` only for JS/TS work unless the user explicitly approves otherwise.
- Existing route/UI language is Spanish (`/autenticacion`, `/registro`); extend the current app vocabulary instead of anglicizing it mid-feature.

## Repo gotchas

- `api/LCDPC/web/` is a duplicate copy of the frontend; edit `web/` unless the user explicitly says otherwise.
- `web_backup_before_move/` is vestigial; ignore it.
- No `.github/` workflows are present; do not invent CI expectations.
