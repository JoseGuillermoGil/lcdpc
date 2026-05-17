# LCDPC API

API en C# con Clean Architecture y PostgreSQL.

## Estructura

- `LCDPC.Domain`
- `LCDPC.Application`
- `LCDPC.Infrastructure`
- `LCDPC.API`
- `LCDPC.Architecture.Tests`

## Requisitos

- .NET SDK 10
- Docker + Docker Compose (opcional para ejecución containerizada)

## Conexión por defecto

`LCDPC.API/appsettings.json`:

```json
"ConnectionStrings": {
  "LCDPC": "Host=postgres;Port=5432;Database=lcdpc_db;Username=lcdpc;Password=lcdpc123"
}
```

## Levantar con Docker

1. Crear archivo de entorno para auth/session policy:

```bash
cp .env.example .env
```

2. Levantar servicios:

```bash
docker compose up --build
```

- API: `http://localhost:8080`
- Health: `http://localhost:8080/health`
- OpenAPI (dev): `http://localhost:8080/openapi/v1.json`

## Variables de entorno (`.env`)

- `AUTH_ACCESS_TOKEN_TTL_MINUTES`: TTL de access token.
- `AUTH_REFRESH_TOKEN_TTL_DAYS`: TTL de refresh token.
- `AUTH_PASSWORD_RESET_TTL_MINUTES`: TTL de token de recuperación.
- `AUTH_REVOKE_SESSIONS_ON_PASSWORD_RESET`: revocar sesiones activas al resetear password (`true/false`).
- `AUTH_SUPERUSER_PASSWORD`: contraseña del superusuario de pruebas.
- `AUTH_GOOGLE_CLIENT_ID`: client id OAuth de Google.
- `AUTH_GOOGLE_CLIENT_SECRET`: client secret OAuth de Google.
- `AUTH_GOOGLE_REDIRECT_URI`: callback backend de Google OAuth.
- `AUTH_GOOGLE_SCOPE`: scopes solicitados a Google (default `openid email profile`).

## Endpoints de autenticación (v1)

- `POST /api/v1/auth/login`
- `GET /api/v1/auth/register/google`
- `GET /api/v1/auth/register/google/callback`
- `GET /api/v1/auth/me`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout`
- `POST /api/v1/auth/forgot-password`
- `POST /api/v1/auth/reset-password`
- `GET /api/v1/auth/security-policy`
- `PUT /api/v1/auth/security-policy` (requiere rol `admin_global` autenticado)

## Superusuario de pruebas (seed automático)

- Alias funcional: `superperro`.
- Login único: se usa el mismo `POST /api/v1/auth/login` para cliente y admin.
- Email: `nemoxgil@gmail.com`.
- Nombre: `Jose Guillermo Gil Valderrama`.
- Cédula: `V24276018`.
- Password: valor de `AUTH_SUPERUSER_PASSWORD` (por defecto `SuperPerro123!`).

## Levantar local (sin Docker)

1. Ajustar `ConnectionStrings:LCDPC` a tu host local de PostgreSQL.
2. Ejecutar la API:

```bash
dotnet run --project LCDPC.API/LCDPC.API.csproj
```

## Notas

- Este starter es base técnica; los módulos de negocio se implementan según SDD.
- No se ejecutaron builds/tests automáticos en este paso para respetar la fase de setup.
