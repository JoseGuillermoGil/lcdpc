# Plan de Migración: C# (.NET 10) → Go

## 1. Contexto y Motivación

### Estado actual
- **Stack:** .NET 10, EF Core 10, PostgreSQL 16, Clean Architecture (4 proyectos)
- **Endpoints:** ~40 endpoints REST (auth, OAuth2, productos, combos, precios, sedes, sync, admin)
- **Auth:** OAuth 2.0 completo (RFC 6749/7636/7662/7009) + compatibilidad legacy con cookies
- **DB:** 20 tablas, seeders, sin migraciones en runtime (usa `EnsureCreated`)
- **Tests:** xUnit, ~16 archivos de prueba

### Por qué migrar a Go
- Rendimiento: Go compila a binario nativo, menor latencia, menor uso de memoria
- Simplicidad: Lenguaje más simple, menos abstracciones, más fácil de mantener
- Deployment: Binario estático, imágenes Docker más pequeñas (~15MB vs ~200MB)
- Ecosistema: Excelente soporte para APIs REST, PostgreSQL, JWT
- Concurrencia: Goroutines nativas para manejo concurrente
- Contratación: Mayor pool de desarrolladores Go disponibles

---

## 2. Análisis del Código Actual

### 2.1 Arquitectura (Clean Architecture)

```
LCDPC.Domain          → Entidades, Value Objects, Enums, Servicios de dominio (sin dependencias)
LCDPC.Application     → Interfaces, DTOs (depende solo de Domain)
LCDPC.Infrastructure  → EF Core, PostgreSQL, implementaciones (depende de Application + Domain)
LCDPC.API             → Controllers, Security, DI (depende de los tres)
```

### 2.2 Entidades de Dominio (20 tablas)

| Categoría | Entidades |
|-----------|-----------|
| **Usuarios** | User, Profile, Role, UserRoleAssignment, UserSession, PasswordResetToken, AuthSecurityPolicy, AuditLog, RegistrationFlow, ApiResource, RoleResourcePermission, ApiToken |
| **Pricing** | Producto, Combo, ComboItem, PrecioProductoSede, Carrito, LineaCarrito, Orden |
| **OAuth2** | OAuth2Client, OAuth2AuthorizationCode, OAuth2RefreshToken |

### 2.3 Value Objects
- `Money` (Amount decimal, Currency string) - Usado en precios
- `CantidadComercial` (Cantidad decimal, UnidadCompra enum)
- `UmbralEmpaque` (UnidadesPorCaja int, UnidadesPorBulto int)

### 2.4 Enums
- UserStatus (Active, Suspended, Deactivated)
- EstadoCombo (Borrador, Publicado, Pausado)
- EstadoCarrito (Abierto, Confirmado, Cancelado)
- EstadoOrden (Borrador, Confirmada, Cancelada)
- PriceTier (Precio1-4)
- TipoMedidaBase (Unidad, Gramos, Kilo)
- TipoComercialMayor (Pieza, Caja, Bulto)
- UnidadCompra (Unidad, Caja, Bulto, Pieza, Gramos, Kilo)
- OrigenPrecioLinea (Precio1-4, ComboPromocional, Referencial)

### 2.5 Servicios de Aplicación (10 interfaces)
- `IRegistrationFlowService` → Registro, login, me, password reset, security policy, Google OAuth
- `IEmailService` → Envío de OTP y emails de reset
- `IOAuth2AuthorizationService` → Authorize, code exchange, refresh, introspect, revoke
- `IOAuth2TokenService` → Generar/validar JWT (RS256) y refresh tokens
- `IOAuth2ClientService` → Lookup de clientes, validación de redirect URIs y scopes
- `IOAuth2KeyService` → Gestión de claves RSA, exportación JWKS
- `IGoogleOAuthService` → Generación de URL de Google, intercambio de código
- `IProductoService` → CRUD de productos
- `IComboService` → CRUD + publicar/pausar combos
- `IPrecioProductoSedeService` → CRUD de precios por sede
- `ISyncService` → Sincronización masiva de productos y combos

### 2.6 Controladores (10 controllers)
- `AuthController` → 12 endpoints (registro, login, OAuth, password reset, security policy)
- `OAuth2Controller` → 5 endpoints (authorize, token, introspect, revoke, Google callback)
- `DiscoveryController` → 2 endpoints (OIDC discovery, JWKS)
- `AdminUsersController` → 4 endpoints (listar, cambiar estado, asignar roles, forzar reset)
- `ProductosController` → 5 endpoints (CRUD)
- `CombosController` → 7 endpoints (CRUD + publicar/pausar)
- `PreciosController` → 6 endpoints (CRUD)
- `SedesController` → 2 endpoints (crear, listar)
- `SyncController` → 2 endpoints (sync productos, sync combos)
- `HealthController` → 1 endpoint (health check)

### 2.7 Seguridad
- **JWT RS256:** Firma RSA-4096, JWKS, OIDC discovery
- **Refresh tokens:** Rotación con detección de robo (familias de tokens)
- **Legacy cookies:** `lcdpc_at`, `lcdpc_rt` (HttpOnly, Secure, SameSite)
- **API Keys:** Para endpoints de sync (`X-API-Key`)
- **Roles:** `RequireRoles` attribute (admin_global, admin_sede, cliente)
- **Password hashing:** PBKDF2-SHA256, 100,000 iteraciones
- **Rate limiting:** Por IP, sliding window

---

## 3. Estructura del Proyecto Go

```
lcdpc-go/
├── cmd/
│   └── server/
│       └── main.go                    # Entry point, DI, startup
├── internal/
│   ├── domain/                        # Entidades, Value Objects, Enums (sin dependencias)
│   │   ├── entities/
│   │   │   ├── user/
│   │   │   │   ├── user.go
│   │   │   │   ├── profile.go
│   │   │   │   ├── role.go
│   │   │   │   ├── user_role_assignment.go
│   │   │   │   ├── user_session.go
│   │   │   │   ├── password_reset_token.go
│   │   │   │   ├── auth_security_policy.go
│   │   │   │   ├── audit_log.go
│   │   │   │   ├── registration_flow.go
│   │   │   │   ├── api_resource.go
│   │   │   │   ├── role_resource_permission.go
│   │   │   │   └── api_token.go
│   │   │   ├── pricing/
│   │   │   │   ├── producto.go
│   │   │   │   ├── combo.go
│   │   │   │   ├── combo_item.go
│   │   │   │   ├── precio_producto_sede.go
│   │   │   │   ├── carrito.go
│   │   │   │   ├── linea_carrito.go
│   │   │   │   └── orden.go
│   │   │   └── oauth2/
│   │   │       ├── client.go
│   │   │       ├── authorization_code.go
│   │   │       └── refresh_token.go
│   │   ├── valueobjects/
│   │   │   ├── money.go
│   │   │   ├── cantidad_comercial.go
│   │   │   └── umbral_empaque.go
│   │   ├── enums/
│   │   │   ├── user_status.go
│   │   │   ├── estado_combo.go
│   │   │   ├── price_tier.go
│   │   │   ├── tipo_medida_base.go
│   │   │   └── ... (otros enums)
│   │   └── services/
│   │       ├── pricing_service.go
│   │       ├── combo_pricing_service.go
│   │       └── unidad_comercial_resolver.go
│   │
│   ├── application/                   # Interfaces y DTOs
│   │   ├── auth/
│   │   │   ├── contracts.go           # DTOs de request/response
│   │   │   └── service.go             # IRegistrationFlowService
│   │   ├── oauth2/
│   │   │   ├── contracts.go
│   │   │   ├── authorization_service.go
│   │   │   ├── token_service.go
│   │   │   ├── client_service.go
│   │   │   ├── key_service.go
│   │   │   └── google_service.go
│   │   ├── email/
│   │   │   └── service.go
│   │   ├── productos/
│   │   │   ├── contracts.go
│   │   │   └── service.go
│   │   ├── combos/
│   │   │   ├── contracts.go
│   │   │   └── service.go
│   │   ├── precios/
│   │   │   ├── contracts.go
│   │   │   └── service.go
│   │   └── sync/
│   │       ├── contracts.go
│   │       └── service.go
│   │
│   ├── infrastructure/                # Implementaciones
│   │   ├── persistence/
│   │   │   ├── postgres.go            # Conexión y configuración
│   │   │   ├── repositories/
│   │   │   │   ├── user_repo.go
│   │   │   │   ├── producto_repo.go
│   │   │   │   ├── combo_repo.go
│   │   │   │   ├── precio_repo.go
│   │   │   │   ├── sede_repo.go
│   │   │   │   └── ... (otros repos)
│   │   │   └── seeders/
│   │   │       ├── super_user.go
│   │   │       ├── oauth2_client.go
│   │   │       └── api_token.go
│   │   ├── auth/
│   │   │   ├── registration_service.go
│   │   │   ├── token_hashing.go
│   │   │   └── password_hashing.go
│   │   ├── oauth2/
│   │   │   ├── authorization_service.go
│   │   │   ├── token_service.go
│   │   │   ├── client_service.go
│   │   │   ├── key_service.go
│   │   │   └── google_service.go
│   │   ├── email/
│   │   │   └── resend_service.go
│   │   ├── productos/
│   │   │   └── service.go
│   │   ├── combos/
│   │   │   └── service.go
│   │   ├── precios/
│   │   │   └── service.go
│   │   └── sync/
│   │       └── service.go
│   │
│   └── api/                           # HTTP handlers, middleware, routes
│       ├── server.go                  # Configuración del servidor HTTP
│       ├── middleware/
│       │   ├── cors.go
│       │   ├── auth.go                # JWT validation, cookie extraction
│       │   ├── roles.go               # Role-based authorization
│       │   ├── api_key.go             # API key validation
│       │   └── rate_limit.go
│       ├── handlers/
│       │   ├── auth.go
│       │   ├── oauth2.go
│       │   ├── discovery.go
│       │   ├── admin_users.go
│       │   ├── productos.go
│       │   ├── combos.go
│       │   ├── precios.go
│       │   ├── sedes.go
│       │   ├── sync.go
│       │   └── health.go
│       └── responses/
│           └── jsend.go               # JSend response helper
│
├── migrations/                        # Migraciones SQL (golang-migrate)
│   ├── 000001_initial_schema.up.sql
│   ├── 000001_initial_schema.down.sql
│   └── ... (migraciones incrementales)
│
├── configs/
│   └── config.go                      # Configuración con viper/env
│
├── pkg/                               # Paquetes compartidos
│   ├── jwt/
│   │   └── jwt.go                     # JWT generation/validation
│   ├── crypto/
│   │   └── crypto.go                  # Hashing, encryption
│   └── httpclient/
│       └── client.go                  # HTTP client para Google OAuth
│
├── tests/
│   ├── integration/
│   │   ├── auth_test.go
│   │   ├── oauth2_test.go
│   │   ├── productos_test.go
│   │   └── ...
│   └── unit/
│       ├── domain/
│       │   └── pricing_test.go
│       └── services/
│           └── registration_test.go
│
├── Dockerfile
├── docker-compose.yml
├── go.mod
├── go.sum
├── .env.example
└── README.md
```

---

## 4. Stack Tecnológico Go

### 4.1 Framework HTTP
**Elección:** `chi` o `echo`
- `chi`: Minimalista, compatible con `net/http` estándar, middleware elegante
- `echo`: Más features built-in, buen rendimiento, documentación completa

**Recomendación:** `chi` por su simplicidad y compatibilidad con el ecosistema Go estándar.

### 4.2 Base de Datos
**Elección:** `pgx` (driver PostgreSQL) + `sqlc` (code generation) o `sqlx`
- `pgx`: Driver nativo de PostgreSQL, mejor rendimiento que `lib/pq`
- `sqlc`: Genera código type-safe desde queries SQL
- `sqlx`: Extensiones a `database/sql` para mapping

**Recomendación:** `pgx` + `sqlc` para queries type-safe.

### 4.3 Migraciones
**Elección:** `golang-migrate/migrate`
- Migraciones SQL up/down
- CLI integrado
- Soporte para PostgreSQL nativo

### 4.4 JWT y Criptografía
**Elección:** `golang-jwt/jwt/v5` + `crypto/rsa` estándar
- Generación y validación de JWT RS256
- JWKS support con `github.com/MicahParks/keyfunc`
- PBKDF2 con `golang.org/x/crypto/pbkdf2`

### 4.5 OAuth2
**Elección:** `golang.org/x/oauth2`
- Google OAuth2 provider integrado
- Token exchange, refresh
- Claims parsing

### 4.6 Configuración
**Elección:** `spf13/viper` o `envconfig`
- Variables de entorno
- Archivos .env
- Defaults

**Recomendación:** `spf13/viper` por su flexibilidad.

### 4.7 Logging
**Elección:** `zerolog` o `slog` (Go 1.21+)
- `slog`: Logger estructurado estándar (Go 1.21+)
- `zerolog`: Zero-allocation JSON logger

**Recomendación:** `slog` por ser estándar.

### 4.8 Testing
**Elección:** `testify` + `testcontainers-go`
- `testify`: Assertions y mocks
- `testcontainers-go`: Containers Docker para tests de integración

### 4.9 Email
**Elección:** `resend-go` (cliente oficial de Resend)
- API compatible con el servicio actual

### 4.10 Validación
**Elección:** `go-playground/validator`
- Validación de structs con tags
- Mensajes de error personalizables

---

## 5. Plan de Migración por Fases

### Fase 0: Preparación (1-2 días)
- [ ] Crear estructura de directorios
- [ ] Inicializar `go.mod` con dependencias
- [ ] Configurar Dockerfile y docker-compose
- [ ] Configurar variables de entorno
- [ ] Crear migraciones SQL desde el esquema actual
- [ ] Configurar CI básico (lint, test)

### Fase 1: Domain Layer (2-3 días)
- [ ] Migrar enums (UserStatus, EstadoCombo, PriceTier, etc.)
- [ ] Migrar value objects (Money, CantidadComercial, UmbralEmpaque)
- [ ] Migrar entidades de dominio (User, Profile, Role, Producto, Combo, etc.)
- [ ] Migrar servicios de dominio (PricingService, ComboPricingService, UnidadComercialResolver)
- [ ] Tests unitarios de dominio

### Fase 2: Application Layer (1-2 días)
- [ ] Definir interfaces de servicios
- [ ] Definir DTOs de request/response
- [ ] Definir contratos de repositorio
- [ ] Documentar reglas de negocio

### Fase 3: Infrastructure Layer - Persistencia (3-4 días)
- [ ] Configurar conexión PostgreSQL con `pgx`
- [ ] Implementar repositorio de usuarios
- [ ] Implementar repositorio de productos
- [ ] Implementar repositorio de combos
- [ ] Implementar repositorio de precios
- [ ] Implementar repositorio de sedes
- [ ] Implementar repositorio de OAuth2
- [ ] Implementar seeders
- [ ] Tests de integración con base de datos

### Fase 4: Infrastructure Layer - Servicios (3-4 días)
- [ ] Implementar `TokenHashing` y `PasswordHashing`
- [ ] Implementar `RegistrationFlowService`
- [ ] Implementar `OAuth2TokenService` (JWT RS256)
- [ ] Implementar `OAuth2AuthorizationService`
- [ ] Implementar `OAuth2ClientService`
- [ ] Implementar `RsaKeyService` (JWKS)
- [ ] Implementar `GoogleOAuthService`
- [ ] Implementar `ResendEmailService`
- [ ] Implementar `ProductoService`, `ComboService`, `PrecioProductoSedeService`
- [ ] Implementar `SyncService`
- [ ] Tests de integración de servicios

### Fase 5: API Layer - Middleware (2-3 días)
- [ ] Implementar middleware CORS
- [ ] Implementar middleware de autenticación JWT
- [ ] Implementar middleware de roles (`RequireRoles`)
- [ ] Implementar middleware de API Key
- [ ] Implementar middleware de rate limiting
- [ ] Implementar helper de respuestas JSend
- [ ] Tests de middleware

### Fase 6: API Layer - Handlers (4-5 días)
- [ ] Implementar `AuthHandler` (12 endpoints)
  - POST `/api/v1/auth/register/start`
  - POST `/api/v1/auth/register/verify-email`
  - POST `/api/v1/auth/register/profile`
  - POST `/api/v1/auth/login`
  - GET `/api/v1/auth/register/google`
  - GET `/api/v1/auth/register/google/callback`
  - GET `/api/v1/auth/me`
  - POST `/api/v1/auth/refresh`
  - POST `/api/v1/auth/logout`
  - POST `/api/v1/auth/forgot-password`
  - POST `/api/v1/auth/reset-password`
  - GET `/api/v1/auth/security-policy`
  - PUT `/api/v1/auth/security-policy`
- [ ] Implementar `OAuth2Handler` (5 endpoints)
  - GET `/oauth2/authorize`
  - POST `/oauth2/token`
  - POST `/oauth2/introspect`
  - POST `/oauth2/revoke`
  - GET `/oauth2/callback/google`
- [ ] Implementar `DiscoveryHandler` (2 endpoints)
  - GET `/.well-known/openid-configuration`
  - GET `/.well-known/jwks.json`
- [ ] Implementar `AdminUsersHandler` (4 endpoints)
- [ ] Implementar `ProductosHandler` (5 endpoints)
- [ ] Implementar `CombosHandler` (7 endpoints)
- [ ] Implementar `PreciosHandler` (6 endpoints)
- [ ] Implementar `SedesHandler` (2 endpoints)
- [ ] Implementar `SyncHandler` (2 endpoints)
- [ ] Implementar `HealthHandler` (1 endpoint)
- [ ] Tests de integración de endpoints

### Fase 7: Testing y Validación (2-3 días)
- [ ] Tests de integración end-to-end
- [ ] Tests de compatibilidad con frontend existente
- [ ] Validación de todos los endpoints con curl/Postman
- [ ] Validación de autenticación (OAuth2 + legacy cookies)
- [ ] Validación de roles y permisos
- [ ] Performance benchmarks

### Fase 8: Deployment y Documentación (1-2 días)
- [ ] Actualizar docker-compose para Go
- [ ] Configurar variables de entorno
- [ ] Documentar diferencias con versión C#
- [ ] Documentar proceso de build y deploy
- [ ] Crear script de migración de datos (si es necesario)

---

## 6. Mapeo de Tecnologías

| C# (.NET 10) | Go | Notas |
|---------------|-----|-------|
| ASP.NET Core | `chi` + `net/http` | Router minimalista |
| Entity Framework Core | `pgx` + `sqlc` | Queries type-safe |
| PostgreSQL (Npgsql) | `pgx/v5` | Driver nativo |
| JWT (System.IdentityModel) | `golang-jwt/jwt/v5` | RS256 support |
| RSA (System.Security.Cryptography) | `crypto/rsa` | Estándar Go |
| DI (IServiceCollection) | Manual / Wire | Go no tiene DI framework |
| Options Pattern | Structs + constructors | Más simple |
| IAsyncActionFilter | Middleware | Go usa middleware chain |
| TypeFilterAttribute | Funciones middleware | Composición funcional |
| xUnit | `testify` + `testing` | Testing estándar |
| Resend (NuGet) | `resend-go` | Cliente oficial |
| Scalar (OpenAPI) | `swaggo/swag` | Swagger generation |
| Health Checks | `health` handler custom | Simple |
| CORS | `rs/cors` | Middleware |
| Rate Limiting | `ulule/limiter` | Token bucket |
| Configuration | `spf13/viper` | Flexible |
| Logging | `slog` (Go 1.21+) | Structured logging estándar |

---

## 7. Diferencias Arquitectónicas Clave

### 7.1 Dependency Injection
**C#:** Framework de DI integrado (`IServiceCollection`, `AddScoped`, `AddSingleton`)
**Go:** Inyección manual en `main.go` o usar `google/wire` para code generation

```go
// main.go - DI manual
db := persistence.NewPostgresDB(cfg)
userRepo := repositories.NewUserRepo(db)
authService := auth.NewRegistrationService(userRepo, emailService)
authHandler := handlers.NewAuthHandler(authService)
```

### 7.2 ORM vs SQL
**C#:** EF Core con LINQ, change tracking, migrations
**Go:** `sqlc` genera código Go desde queries SQL, sin ORM

```sql
-- queries/user.sql
-- name: GetUserByEmail :one
SELECT * FROM users WHERE email = $1;
```

```go
// Generated by sqlc
func (q *Queries) GetUserByEmail(ctx context.Context, email string) (User, error) {
    // ...
}
```

### 7.3 Error Handling
**C#:** Exceptions (`throw new InvalidOperationException`)
**Go:** Error values (`return nil, fmt.Errorf("...")`)

```go
// Go pattern
if err != nil {
    return nil, fmt.Errorf("failed to get user: %w", err)
}
```

### 7.4 Async/Await
**C#:** `async/await` con `Task<T>`
**Go:** Goroutines + channels + `context.Context`

```go
// Go pattern
ctx := context.Background()
result, err := service.DoSomething(ctx, input)
```

### 7.5 Modelado de Entidades
**C#:** Clases con propiedades, navigation properties, lazy loading
**Go:** Structs planos, sin navigation properties

```go
type User struct {
    ID               uuid.UUID
    Email            string
    PasswordHash     string
    EmailVerifiedAt  *time.Time
    OnboardingStatus string
    Status           UserStatus
    CreatedAt        time.Time
}
```

---

## 8. Riesgos y Mitigaciones

| Riesgo | Impacto | Mitigación |
|--------|---------|------------|
| Pérdida de funcionalidad OAuth2 | Alto | Tests exhaustivos de compatibilidad con frontend |
| Cambio en formato de JWT | Alto | Mantener misma estructura de claims |
| Diferencias en hashing de passwords | Alto | Usar misma implementación PBKDF2 |
| Incompatibilidad con DB existente | Alto | Migraciones SQL incrementales, no recrear DB |
| Pérdida de tests | Medio | Portar tests existentes, agregar nuevos |
| Tiempo de migración mayor al estimado | Medio | Priorizar endpoints críticos, migrar por fases |
| Dificultad para encontrar desarrolladores Go | Bajo | Go es popular, documentación abundante |

---

## 9. Estrategia de Coexistencia

Durante la migración, ambos sistemas pueden coexistir:

1. **Proxy inverso (nginx/traefik):** Redirigir tráfico gradualmente
2. **Feature flags:** Migrar endpoint por endpoint
3. **Base de datos compartida:** Ambos sistemas usan la misma DB
4. **Tests de regresión:** Validar que el comportamiento sea idéntico

---

## 10. Estimación de Tiempo

| Fase | Días | Dependencias |
|------|------|--------------|
| Fase 0: Preparación | 1-2 | Ninguna |
| Fase 1: Domain | 2-3 | Fase 0 |
| Fase 2: Application | 1-2 | Fase 1 |
| Fase 3: Persistencia | 3-4 | Fase 2 |
| Fase 4: Servicios | 3-4 | Fase 3 |
| Fase 5: Middleware | 2-3 | Fase 4 |
| Fase 6: Handlers | 4-5 | Fase 5 |
| Fase 7: Testing | 2-3 | Fase 6 |
| Fase 8: Deployment | 1-2 | Fase 7 |
| **Total** | **19-28 días** | |

---

## 11. Priorización de Endpoints

### Crítico (Fase 6a - primero)
1. Health check
2. Auth: login, me, refresh, logout
3. Auth: registro (start, verify, profile)
4. Productos: CRUD básico
5. Sedes: listar

### Importante (Fase 6b - segundo)
1. OAuth2: authorize, token, introspect, revoke
2. Discovery: OIDC, JWKS
3. Combos: CRUD + publicar/pausar
4. Precios: CRUD
5. Auth: password reset, security policy

### Secundario (Fase 6c - tercero)
1. Admin users: gestión completa
2. Sync: productos, combos
3. Google OAuth
4. Rate limiting avanzado

---

## 12. Comandos de Inicio Rápido

```bash
# Inicializar proyecto Go
mkdir lcdpc-go && cd lcdpc-go
go mod init github.com/lcdpc/lcdpc-go

# Instalar dependencias principales
go get github.com/go-chi/chi/v5
go get github.com/jackc/pgx/v5
go get github.com/golang-jwt/jwt/v5
go get github.com/spf13/viper
go get github.com/google/uuid
go get golang.org/x/crypto/pbkdf2
go get github.com/resend/resend-go/v2

# Crear estructura
mkdir -p cmd/server
mkdir -p internal/{domain,application,infrastructure,api}
mkdir -p internal/domain/{entities,valueobjects,enums,services}
mkdir -p internal/infrastructure/{persistence,auth,oauth2,email}
mkdir -p internal/api/{middleware,handlers,responses}
mkdir -p migrations configs pkg tests

# Ejecutar
go run cmd/server/main.go

# Tests
go test ./...

# Build
go build -o lcdpc-server cmd/server/main.go
```

---

## 13. Checklist de Validación Post-Migración

- [ ] Todos los endpoints responden correctamente
- [ ] Autenticación OAuth2 funciona con frontend existente
- [ ] Legacy cookies funcionan para compatibilidad
- [ ] Roles y permisos se aplican correctamente
- [ ] JWT tiene misma estructura de claims
- [ ] Password hashing es compatible (PBKDF2)
- [ ] Seeders crean datos iniciales correctamente
- [ ] Health checks responden
- [ ] CORS permite credenciales
- [ ] Rate limiting funciona
- [ ] Tests de integración pasan
- [ ] Docker build funciona
- [ ] Performance es igual o mejor que C#

---

## 14. Referencias

- [Chi Router](https://github.com/go-chi/chi)
- [pgx - PostgreSQL Driver](https://github.com/jackc/pgx)
- [sqlc - SQL Code Generator](https://sqlc.dev/)
- [golang-jwt](https://github.com/golang-jwt/jwt)
- [golang-migrate](https://github.com/golang-migrate/migrate)
- [testify](https://github.com/stretchr/testify)
- [Resend Go](https://github.com/resend/resend-go)
- [Go Project Layout](https://github.com/golang-standards/project-layout)
