# LCDPC Monorepo

Repositorio monolítico con backend (`.NET`) y documentación funcional/técnica centralizada.

## Estructura

- `api/LCDPC/` — solución backend en C# (.NET 10, Clean Architecture, EF Core, PostgreSQL).
- `web/` — espacio frontend (en evolución).
- `docs/` — documentación consolidada en raíz.
  - `docs/sdd/` — specs, domain model y tasks por cambio.
  - `docs/backend/` — arquitectura técnica backend.

## Backend rápido

### Requisitos

- .NET SDK 10
- Docker + Docker Compose (opcional)

### Levantar con Docker

```bash
cd api/LCDPC
cp .env.example .env
docker compose up --build
```

- API: `http://localhost:8080`
- Health: `http://localhost:8080/health`
- OpenAPI (dev): `http://localhost:8080/openapi/v1.json`

### Levantar local (sin Docker)

```bash
cd api/LCDPC
dotnet run --project LCDPC.API/LCDPC.API.csproj
```

## Variables de entorno backend

Archivo: `api/LCDPC/.env`

- `AUTH_ACCESS_TOKEN_TTL_MINUTES`
- `AUTH_REFRESH_TOKEN_TTL_DAYS`
- `AUTH_PASSWORD_RESET_TTL_MINUTES`
- `AUTH_REVOKE_SESSIONS_ON_PASSWORD_RESET`
- `AUTH_SUPERUSER_PASSWORD`
- `AUTH_GOOGLE_CLIENT_ID`
- `AUTH_GOOGLE_CLIENT_SECRET`
- `AUTH_GOOGLE_REDIRECT_URI`
- `AUTH_GOOGLE_SCOPE`

## Documentación

- SDD principal: `docs/sdd/README.md`
- Usuarios y administradores:
  - `docs/sdd/changes/usuarios-y-administradores/spec.md`
  - `docs/sdd/changes/usuarios-y-administradores/domain-model.md`
  - `docs/sdd/changes/usuarios-y-administradores/tasks.md`
  - `docs/sdd/changes/usuarios-y-administradores/tasks.frontend.md`
- Blueprint backend fase 1:
  - `docs/backend/architecture/fase-1-blueprint.md`

## Superusuario de pruebas

- Alias funcional: `superperro`
- Email: `nemoxgil@gmail.com`
- Nombre: `Jose Guillermo Gil Valderrama`
- Cédula: `V24276018`
- Password: `AUTH_SUPERUSER_PASSWORD` (default: `SuperPerro123!`)
