# LCDPC Monorepo

Backend y documentación del proyecto LCDPC en un único repositorio. El backend ya opera con **OAuth 2.0 Authorization Server propio** para autenticación web y compatibilidad transitoria con `/api/v1/auth/*`.

## Quick Path

1. Entrá a `api/LCDPC`.
2. Copiá `.env.example` a `.env`.
3. Levantá con Docker o `dotnet run`.
4. Verificá `http://localhost:8080/health` y `http://localhost:8080/openapi/v1.json`.

## Estructura

- `api/LCDPC/` — backend .NET 10, Clean Architecture, EF Core, PostgreSQL.
- `web/` — frontend Angular en transición hacia flujo OAuth 2.0 estándar.
- `docs/` — arquitectura, SDD y especificaciones.

## Backend

### Requisitos

- .NET SDK 10
- Docker + Docker Compose opcional

### Levantar con Docker

```bash
cd api/LCDPC
cp .env.example .env
docker compose up --build
```

### Levantar local

```bash
cd api/LCDPC
dotnet run --project LCDPC.API/LCDPC.API.csproj
```

### URLs útiles

- API: `http://localhost:8080`
- Health: `http://localhost:8080/health`
- OpenAPI (dev): `http://localhost:8080/openapi/v1.json`
- OIDC discovery: `http://localhost:8080/.well-known/openid-configuration`
- JWKS: `http://localhost:8080/.well-known/jwks.json`

## Autenticación actual

La estrategia activa del backend es:

- flujo principal OAuth 2.0 Authorization Code + PKCE por `/oauth2/*`
- access tokens JWT firmados con RS256
- refresh tokens opacos con rotación y revocación por familia
- compatibilidad temporal con `/api/v1/auth/*` mientras el frontend termina la migración

## Variables de entorno backend

Archivo: `api/LCDPC/.env`

### Seguridad y flujos pre-auth

- `AUTH_PASSWORD_RESET_TTL_MINUTES`
- `AUTH_REVOKE_SESSIONS_ON_PASSWORD_RESET`
- `AUTH_SUPERUSER_PASSWORD`
- `AUTH_GOOGLE_CLIENT_ID`
- `AUTH_GOOGLE_CLIENT_SECRET`
- `AUTH_GOOGLE_REDIRECT_URI`
- `AUTH_GOOGLE_SCOPE`

### OAuth 2.0

- `OAUTH2_ISSUER`
- `OAUTH2_AUDIENCE`
- `OAUTH2_ACCESS_TOKEN_TTL_MINUTES`
- `OAUTH2_REFRESH_TOKEN_TTL_DAYS`
- `OAUTH2_AUTHORIZATION_CODE_TTL_MINUTES`
- `OAUTH2_RSA_KEY_PATH`

## Documentación

- SDD principal: `docs/sdd/README.md`
- Change OAuth 2.0:
  - `docs/sdd/changes/oauth2-estandarizacion/proposal.md`
  - `docs/sdd/changes/oauth2-estandarizacion/spec.md`
  - `docs/sdd/changes/oauth2-estandarizacion/tasks.md`
- Usuarios y administradores:
  - `docs/sdd/changes/usuarios-y-administradores/spec.md`
  - `docs/sdd/changes/usuarios-y-administradores/domain-model.md`
  - `docs/sdd/changes/usuarios-y-administradores/tasks.md`
- Blueprint backend:
  - `docs/backend/architecture/fase-1-blueprint.md`

## Superusuario de pruebas

- Alias funcional: `superperro`
- Email: `nemoxgil@gmail.com`
- Nombre: `Jose Guillermo Gil Valderrama`
- Cédula: `V24276018`
- Password: `AUTH_SUPERUSER_PASSWORD` (default: `SuperPerro123!`)
