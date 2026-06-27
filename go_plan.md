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

### Principios de diseño

1. **Feature-based, no layer-based.** Go no se beneficia de la separación rígida Domain/Application/Infrastructure de C#. Anidar carpetas profundamente (`internal/domain/entities/user/`) va contra las convenciones del lenguaje y lleva a import cycles.
2. **Interfaces en el consumidor.** Las interfaces se definen en el paquete que las usa, no en el que las implementa. No se crean contratos masivos por adelantado.
3. **sqlc es el repositorio.** No se envuelve `Queries` en otra capa de abstracción.
4. **Paquetes planos por dominio.** Cada paquete agrupa sus structs, lógica de negocio y queries.

```
lcdpc-go/
├── cmd/
│   └── server/
│       └── main.go                         # Entry point, wiring, startup
│
├── internal/
│   ├── user/                               # Todo lo de usuarios en un solo paquete
│   │   ├── models.go                       # User, Profile, Role, UserRoleAssignment, etc.
│   │   ├── service.go                      # Lógica de negocio (registro, login, password reset)
│   │   ├── queries.sql                     # Queries SQL para sqlc
│   │   ├── service_test.go
│   │   └── password.go                     # PBKDF2 hashing
│   │
│   ├── auth/                               # Autenticación y sesiones
│   │   ├── models.go                       # UserSession, RegistrationFlow, PasswordResetToken
│   │   ├── service.go                      # RegistrationFlowService, login, me, refresh
│   │   ├── token.go                        # JWT generation/validation, token hashing
│   │   ├── oauth2_server.go                # OAuth2 authorization server (authorize, token, introspect, revoke)
│   │   ├── jwks.go                         # RSA key management, JWKS export
│   │   ├── google.go                       # Google OAuth client
│   │   ├── queries.sql
│   │   └── service_test.go
│   │
│   ├── pricing/                            # Productos, combos, precios
│   │   ├── models.go                       # Producto, Combo, ComboItem, PrecioProductoSede
│   │   ├── producto_service.go             # CRUD productos
│   │   ├── combo_service.go                # CRUD combos + publicar/pausar
│   │   ├── precio_service.go               # CRUD precios
│   │   ├── pricing.go                      # PricingService, ComboPricingService
│   │   ├── queries.sql
│   │   └── service_test.go
│   │
│   ├── sede/                               # Sedes
│   │   ├── models.go
│   │   ├── service.go
│   │   ├── queries.sql
│   │   └── service_test.go
│   │
│   ├── sync/                               # Sincronización masiva
│   │   ├── service.go
│   │   ├── queries.sql
│   │   └── service_test.go
│   │
│   ├── admin/                              # Endpoints administrativos
│   │   ├── handler.go                      # AdminUsers handler
│   │   ├── queries.sql
│   │   └── handler_test.go
│   │
│   ├── email/                              # Email sender
│   │   ├── sender.go                       # Interfaz (definida aquí, consumida por auth/)
│   │   └── resend.go                       # Implementación Resend
│   │
│   ├── db/                                 # Conexión y migraciones
│   │   ├── db.go                           # pgx pool setup
│   │   ├── seed.go                         # Seeders (superuser, oauth2 client, api token)
│   │   └── generated/                      # Código generado por sqlc (NO editar manualmente)
│   │       ├── db.go
│   │       ├── models.go
│   │       └── querier.go
│   │
│   └── http/                               # Capa HTTP (handlers, middleware, routing)
│       ├── server.go                       # chi.Router setup, middleware chain
│       ├── middleware/
│       │   ├── cors.go
│       │   ├── auth.go                     # JWT validation, cookie extraction
│       │   ├── roles.go                    # Role-based authorization
│       │   ├── apikey.go                   # API key validation
│       │   └── ratelimit.go
│       ├── handler/
│       │   ├── auth.go                     # Auth endpoints
│       │   ├── oauth2.go                   # OAuth2 endpoints
│       │   ├── discovery.go                # OIDC discovery, JWKS
│       │   ├── admin.go                    # Admin users
│       │   ├── producto.go
│       │   ├── combo.go
│       │   ├── precio.go
│       │   ├── sede.go
│       │   ├── sync.go
│       │   └── health.go
│       └── response/
│           └── jsend.go                    # JSend response helper
│
├── sqlc.yaml                               # Configuración de sqlc
├── migrations/                             # Migraciones SQL (golang-migrate)
│   ├── 000001_initial_schema.up.sql
│   ├── 000001_initial_schema.down.sql
│   └── ...
│
├── configs/
│   └── config.go                           # Configuración con viper/env
│
├── Dockerfile
├── docker-compose.yml
├── go.mod
├── go.sum
├── .env.example
└── README.md
```

### Por qué esta estructura

- **`internal/user/`** agrupa todo lo de usuarios: structs, lógica de negocio y queries SQL. No hay separación artificial entre "domain" e "infrastructure".
- **`internal/auth/`** es el paquete más complejo: maneja registro, login, OAuth2 server, JWT, Google OAuth. Las queries SQL están en el mismo paquete.
- **`internal/pricing/`** agrupa productos, combos y precios porque comparten lógica de negocio (pricing tiers, combos).
- **`internal/db/`** contiene solo la conexión pgx y los seeders. El código generado por sqlc vive en `internal/db/generated/`.
- **`internal/http/`** es la capa delgada: handlers parsean JSON, llaman a servicios, retornan JSend.
- **`internal/email/`** define la interfaz `Sender` que consume `auth/`. La implementación Resend vive aquí.

---

## 4. Stack Tecnológico Go

### 4.1 Framework HTTP
**Elección:** `chi`
- Compatible con `net/http` estándar
- Middleware elegante con `func(http.Handler) http.Handler`
- Sin abstractions innecesarias

### 4.2 Base de Datos
**Elección:** `pgx/v5` + `sqlc`
- `pgx`: Driver nativo de PostgreSQL, mejor rendimiento que `lib/pq`
- `sqlc`: Genera código type-safe desde queries SQL. El struct `Queries` actúa como repositorio directo — NO se envuelve en otra capa.

```go
// sqlc genera Queries. Se inyecta directamente en los servicios.
// NO se crea un UserRepository wrapper.
type Service struct {
    queries *db.Queries
    pool    *pgxpool.Pool // para transacciones
}
```

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

### 4.5 OAuth2 Server
**Elección:** Implementación propia (orchestration manual)

`golang.org/x/oauth2` es un **cliente** OAuth2 (sirve para Google Login). No sirve para construir un servidor de autorización. Opciones:

1. **Implementación propia (recomendado):** Ya que el API actual ya implementa los flujos RFC 6749/7636/7662/7009, se replican los flujos directamente manejando las tablas `oauth2_authorization_codes`, `oauth2_refresh_tokens`, `oauth2_clients`. Se controla PKCE, rotación de refresh tokens, y detección de robo.
2. **Ory Fosite:** Framework especializado en OAuth2 server. Más complejo pero battle-tested.

**Decisión:** Implementación propia, ya que el código C# actual ya tiene toda la lógica implementada y se puede portar directamente.

### 4.6 Google OAuth (Cliente)
**Elección:** `golang.org/x/oauth2` + `google.golang.org/api/oauth2/v2`
- Solo como **cliente** para el flujo de Google Login
- Token exchange, user info retrieval

### 4.7 Configuración
**Elección:** `spf13/viper`
- Variables de entorno
- Archivos .env
- Defaults

### 4.8 Logging
**Elección:** `log/slog` (Go 1.21+)
- Logger estructurado estándar
- Sin dependencias externas

### 4.9 Testing
**Elección:** `testify` + `testcontainers-go`
- `testify`: Assertions y mocks
- `testcontainers-go`: Containers Docker para tests de integración con PostgreSQL

### 4.10 Email
**Elección:** `resend-go` (cliente oficial de Resend)
- API compatible con el servicio actual

### 4.11 Validación
**Elección:** `go-playground/validator`
- Validación de structs con tags
- Mensajes de error personalizables

---

## 5. Filosofía de Interfaces en Go

### Regla: "Acepta interfaces, retorna structs"

**NO hacer (antipatrón en Go):**
```go
// application/productos/service.go — interfaces definidas por adelantado
type IProductoService interface {
    Create(ctx context.Context, req CreateProductoRequest) (*Producto, error)
    GetByID(ctx context.Context, id uuid.UUID) (*Producto, error)
    List(ctx context.Context) ([]Producto, error)
    Update(ctx context.Context, id uuid.UUID, req UpdateProductoRequest) (*Producto, error)
    Delete(ctx context.Context, id uuid.UUID) error
}
```

**Hacer (correcto en Go):**
```go
// internal/http/handler/producto.go — interfaz definida en el consumidor
type productoCreator interface {
    Create(ctx context.Context, req pricing.CreateProductoRequest) (*pricing.Producto, error)
}

type productoReader interface {
    GetByID(ctx context.Context, id uuid.UUID) (*pricing.Producto, error)
    List(ctx context.Context) ([]pricing.Producto, error)
}

type ProductoHandler struct {
    creator productoCreator
    reader  productoReader
    updater productoUpdater
    deleter productoDeleter
}
```

### Beneficios
- Cada handler define solo los métodos que necesita
- Fácil de mockear en tests (mock solo lo necesario)
- No hay dependencia circular entre paquetes
- Refactoring seguro: agregar métodos no rompe consumidores

---

## 6. SQLC como Repositorio Directo

### Configuración de sqlc

```yaml
# sqlc.yaml
version: "2"
sql:
  - engine: "postgresql"
    queries: "internal/*/queries.sql"
    schema: "migrations/"
    gen:
      go:
        package: "db"
        out: "internal/db/generated"
        sql_package: "pgx/v5"
        emit_json_tags: true
        emit_empty_slices: true
```

### Uso directo (sin wrapper)

```go
// internal/pricing/producto_service.go
type ProductoService struct {
    q    *db.Queries    // sqlc-generated — NO envolver
    pool *pgxpool.Pool  // para transacciones
}

func (s *ProductoService) Create(ctx context.Context, req CreateProductoRequest) (*Producto, error) {
    // Llamada directa al código generado por sqlc
    row, err := s.q.CreateProducto(ctx, db.CreateProductoParams{
        Nombre:            req.Nombre,
        Sku:               req.Sku,
        TipoMedidaBase:    string(req.TipoMedidaBase),
        TipoComercialMayor: string(req.TipoComercialMayor),
        UnidadesPorCaja:   req.UnidadesPorCaja,
        UnidadesPorBulto:  req.UnidadesPorBulto,
        Activo:            true,
    })
    if err != nil {
        return nil, fmt.Errorf("create producto: %w", err)
    }

    return toDomainProducto(row), nil
}

// Para transacciones:
func (s *ProductoService) UpdateWithPrices(ctx context.Context, id uuid.UUID, req UpdateRequest) error {
    tx, err := s.pool.Begin(ctx)
    if err != nil {
        return fmt.Errorf("begin tx: %w", err)
    }
    defer tx.Rollback(ctx)

    qtx := s.q.WithTx(tx) // sqlc genera WithTx para transacciones

    // ... usar qtx para queries dentro de la transacción

    return tx.Commit(ctx)
}
```

---

## 7. Control de Accesos y Middleware

### Principio de menor privilegio

El middleware de roles en `internal/http/middleware/roles.go` implementa:

```go
func RequireRoles(roles ...string) func(http.Handler) http.Handler {
    return func(next http.Handler) http.Handler {
        return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
            userRoles := getUserRoles(r.Context()) // del JWT o DB

            for _, required := range roles {
                for _, user := range userRoles {
                    if user == required {
                        next.ServeHTTP(w, r)
                        return
                    }
                }
            }

            writeJSendError(w, http.StatusForbidden, "insufficient_permissions")
        })
    }
}
```

### Jerarquía de roles

| Rol | Alcance | Endpoints permitidos |
|-----|---------|---------------------|
| `cliente` | Operaciones comerciales propias | Productos (read), Combos (read), Precios (read), Sedes (read) |
| `admin_sede` | Gestión de sede asignada | CRUD en su sede, combos en su sede |
| `admin_global` | Gestión global | Todos los endpoints, admin users, security policy |

### Reglas de middleware

- `admin_global` se usa solo para configuración inicial y gestión de equipos
- Operación diaria usa `admin_sede` o `cliente`
- Sync endpoints usan API Key, no roles de usuario
- Rate limiting se aplica antes de la autenticación

---

## 8. Plan de Migración por Fases

### Fase 0: Preparación (1-2 días)
- [ ] Crear estructura de directorios (feature-based, plana)
- [ ] Inicializar `go.mod` con dependencias
- [ ] Configurar sqlc.yaml
- [ ] Configurar Dockerfile y docker-compose
- [ ] Configurar variables de entorno
- [ ] Crear migraciones SQL desde el esquema actual (20 tablas)
- [ ] Generar código sqlc
- [ ] Configurar CI básico (lint, test)

### Fase 1: DB y Modelos Base (2-3 días)
- [ ] Configurar conexión PostgreSQL con pgx pool
- [ ] Crear queries SQL para tablas de usuarios (sqlc)
- [ ] Crear queries SQL para tablas de pricing (sqlc)
- [ ] Crear queries SQL para tablas de OAuth2 (sqlc)
- [ ] Crear queries SQL para sedes, sync, api_tokens (sqlc)
- [ ] Implementar seeders (superuser, oauth2 client, api token)
- [ ] Migrar enums y value objects como structs Go
- [ ] Tests de integración con DB real (testcontainers)

### Fase 2: Auth Core (3-4 días)
- [ ] Implementar password hashing (PBKDF2-SHA256, compatible con C#)
- [ ] Implementar token hashing (SHA256 hex)
- [ ] Implementar JWT generation (RS256, misma estructura de claims)
- [ ] Implementar JWT validation
- [ ] Implementar RSA key service (ephemeral + PEM file)
- [ ] Implementar JWKS endpoint handler
- [ ] Implementar registration flow (start, verify-email, profile)
- [ ] Implementar login (legacy cookie adapter)
- [ ] Implementar me, refresh, logout
- [ ] Implementar forgot-password, reset-password
- [ ] Implementar security policy (get, update)
- [ ] Tests de compatibilidad con formato de tokens C#

### Fase 3: OAuth2 Server (2-3 días)
- [ ] Implementar authorize endpoint (PKCE S256 mandatory)
- [ ] Implementar token endpoint (authorization_code + refresh_token)
- [ ] Implementar introspect endpoint (RFC 7662)
- [ ] Implementar revoke endpoint (RFC 7009)
- [ ] Implementar refresh token rotation con detección de robo
- [ ] Implementar OIDC discovery document
- [ ] Implementar Google OAuth (cliente con `golang.org/x/oauth2`)
- [ ] Tests de compatibilidad con frontend existente

### Fase 4: Pricing y Sedes (2-3 días)
- [ ] Implementar ProductoService (CRUD)
- [ ] Implementar ComboService (CRUD + publicar/pausar)
- [ ] Implementar PrecioProductoSedeService (CRUD)
- [ ] Implementar PricingService (resolución de tier)
- [ ] Implementar ComboPricingService (cálculo de precio total)
- [ ] Implementar UnidadComercialResolver
- [ ] Implementar SedesService (CRUD)
- [ ] Implementar SyncService (bulk sync productos, combos)
- [ ] Tests de lógica de negocio

### Fase 5: HTTP Layer (2-3 días)
- [ ] Configurar chi router con middleware chain
- [ ] Implementar middleware CORS (AllowCredentials para auth)
- [ ] Implementar middleware de autenticación JWT (Bearer + cookie)
- [ ] Implementar middleware de roles
- [ ] Implementar middleware de API Key
- [ ] Implementar middleware de rate limiting
- [ ] Implementar helper JSend
- [ ] Implementar handlers de auth (12 endpoints)
- [ ] Implementar handlers de OAuth2 (5 endpoints)
- [ ] Implementar handlers de discovery (2 endpoints)
- [ ] Implementar handlers de pricing (productos, combos, precios)
- [ ] Implementar handlers de sedes (2 endpoints)
- [ ] Implementar handlers de sync (2 endpoints)
- [ ] Implementar handlers de admin (4 endpoints)
- [ ] Implementar health handler (1 endpoint)
- [ ] Tests de integración de endpoints

### Fase 6: Testing y Validación (2-3 días)
- [ ] Tests de integración end-to-end
- [ ] Tests de compatibilidad con frontend existente
- [ ] Validación de todos los endpoints con curl/httpie
- [ ] Validación de autenticación (OAuth2 + legacy cookies)
- [ ] Validación de roles y permisos
- [ ] Validación de refresh token rotation
- [ ] Performance benchmarks

### Fase 7: Deployment (1-2 días)
- [ ] Dockerfile multi-stage (build + scratch/alpine)
- [ ] docker-compose con postgres + api
- [ ] Variables de entorno documentadas
- [ ] Script de migración de datos (si es necesario)
- [ ] Documentación de diferencias con versión C#

---

## 9. Mapeo de Tecnologías

| C# (.NET 10) | Go | Notas |
|---------------|-----|-------|
| ASP.NET Core | `chi` + `net/http` | Router minimalista, middleware chain |
| Entity Framework Core | `pgx` + `sqlc` | Queries type-safe, sin ORM |
| PostgreSQL (Npgsql) | `pgx/v5` | Driver nativo |
| JWT (System.IdentityModel) | `golang-jwt/jwt/v5` | RS256 support |
| RSA (System.Security.Cryptography) | `crypto/rsa` | Estándar Go |
| DI (IServiceCollection) | Wiring en main.go | Go no tiene DI framework |
| Options Pattern | Structs + constructors | Más simple |
| IAsyncActionFilter | Middleware | `func(http.Handler) http.Handler` |
| TypeFilterAttribute | Funciones middleware | Composición funcional |
| xUnit | `testify` + `testing` | Testing estándar |
| Resend (NuGet) | `resend-go` | Cliente oficial |
| Scalar (OpenAPI) | `swaggo/swag` | Swagger generation |
| Health Checks | `health` handler custom | Simple |
| CORS | `rs/cors` | Middleware |
| Rate Limiting | `ulule/limiter` | Token bucket |
| Configuration | `spf13/viper` | Flexible |
| Logging | `log/slog` | Structured logging estándar |
| golang.org/x/oauth2 | Solo como **cliente** Google | NO es servidor OAuth2 |

---

## 10. Diferencias Arquitectónicas Clave

### 10.1 Sin Clean Architecture rígida
**C#:** Domain → Application → Infrastructure → API (control de ensamblados)
**Go:** Paquetes planos por feature. Cada paquete agrupa structs + lógica + queries.

### 10.2 DI Manual
**C#:** `IServiceCollection`, `AddScoped`, `AddSingleton`
**Go:** Wiring en `main.go`

```go
func main() {
    cfg := configs.Load()
    pool := db.Connect(ctx, cfg.DatabaseURL)
    queries := db.New(pool)

    emailSvc := email.NewResendSender(cfg.ResendAPIKey, cfg.ResendFrom)
    authSvc := auth.NewService(queries, pool, emailSvc, cfg)
    pricingSvc := pricing.NewService(queries, pool)

    router := http.NewServer(authSvc, pricingSvc, ...)
    http.ListenAndServe(":8080", router)
}
```

### 10.3 Interfaces Implícitas
**C#:** `interface IProductoService` definida explícitamente
**Go:** Interfaz definida en el consumidor, satisfecha automáticamente

### 10.4 SQLC vs EF Core
**C#:** LINQ, change tracking, lazy loading, migrations en runtime
**Go:** Queries SQL escritas a mano, type-safe code generation, migraciones separadas

### 10.5 Error Handling
**C#:** `throw new InvalidOperationException("CODE")`
**Go:** `return fmt.Errorf("operation: %w", err)`

### 10.6 Concurrencia
**C#:** `async/await` con `Task<T>`
**Go:** `context.Context` + goroutines cuando sea necesario

---

## 11. Estimación de Tiempo

| Fase | Días | Dependencias |
|------|------|--------------|
| Fase 0: Preparación | 1-2 | Ninguna |
| Fase 1: DB y Modelos | 2-3 | Fase 0 |
| Fase 2: Auth Core | 3-4 | Fase 1 |
| Fase 3: OAuth2 Server | 2-3 | Fase 2 |
| Fase 4: Pricing y Sedes | 2-3 | Fase 1 |
| Fase 5: HTTP Layer | 2-3 | Fase 2, 3, 4 |
| Fase 6: Testing | 2-3 | Fase 5 |
| Fase 7: Deployment | 1-2 | Fase 6 |
| **Total** | **15-23 días** | |

---

## 12. Priorización de Endpoints

### Crítico (Fase 2 - primero)
1. Health check
2. Auth: login, me, refresh, logout
3. Auth: registro (start, verify, profile)
4. Productos: CRUD básico
5. Sedes: listar

### Importante (Fase 3 + 4 - segundo)
1. OAuth2: authorize, token, introspect, revoke
2. Discovery: OIDC, JWKS
3. Combos: CRUD + publicar/pausar
4. Precios: CRUD
5. Auth: password reset, security policy

### Secundario (Fase 5 - tercero)
1. Admin users: gestión completa
2. Sync: productos, combos
3. Google OAuth
4. Rate limiting avanzado

---

## 13. Comandos de Inicio Rápido

```bash
# Inicializar proyecto Go
mkdir lcdpc-go && cd lcdpc-go
go mod init github.com/lcdpc/lcdpc-go

# Instalar dependencias principales
go get github.com/go-chi/chi/v5
go get github.com/jackc/pgx/v5
go get github.com/jackc/pgx/v5/pgxpool
go get github.com/golang-jwt/jwt/v5
go get github.com/spf13/viper
go get github.com/google/uuid
go get golang.org/x/crypto/pbkdf2
go get golang.org/x/oauth2
go get github.com/resend/resend-go/v2
go get github.com/go-playground/validator/v10

# Crear estructura
mkdir -p cmd/server
mkdir -p internal/{user,auth,pricing,sede,sync,admin,email,db/generated,http}
mkdir -p internal/http/{middleware,handler,response}
mkdir -p migrations configs

# Ejecutar
go run cmd/server/main.go

# Tests
go test ./...

# Build
go build -o lcdpc-server cmd/server/main.go

# sqlc
sqlc generate
```

---

## 14. Checklist de Validación Post-Migración

- [ ] Todos los endpoints responden correctamente
- [ ] Autenticación OAuth2 funciona con frontend existente
- [ ] Legacy cookies funcionan para compatibilidad
- [ ] Roles y permisos se aplican correctamente (menor privilegio)
- [ ] JWT tiene misma estructura de claims
- [ ] Password hashing es compatible (PBKDF2)
- [ ] Refresh token rotation funciona con detección de robo
- [ ] Seeders crean datos iniciales correctamente
- [ ] Health checks responden
- [ ] CORS permite credenciales
- [ ] Rate limiting funciona
- [ ] Tests de integración pasan
- [ ] Docker build genera imagen < 20MB
- [ ] Performance es igual o mejor que C#

---

## 15. Referencias

- [Chi Router](https://github.com/go-chi/chi)
- [pgx - PostgreSQL Driver](https://github.com/jackc/pgx)
- [sqlc - SQL Code Generator](https://sqlc.dev/)
- [golang-jwt](https://github.com/golang-jwt/jwt)
- [golang-migrate](https://github.com/golang-migrate/migrate)
- [testify](https://github.com/stretchr/testify)
- [Resend Go](https://github.com/resend/resend-go)
- [golang.org/x/oauth2](https://pkg.go.dev/golang.org/x/oauth2) — Solo como cliente
- [Go Proverbs](https://go-proverbs.github.io/) — "Acepta interfaces, retorna structs"
- [sqlc + pgx tutorial](https://docs.sqlc.dev/en/stable/tutorials/getting-started-pgx.html)
