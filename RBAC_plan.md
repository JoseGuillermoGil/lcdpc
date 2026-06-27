# Plan: RBAC Basado en Memoria — User → Profile → Role → Resource

## 1. Problema Actual

1. **Roles hardcoded en el router** — `RequireRoles("admin_global")` escrito directamente en `server.go`. No consulta la DB.
2. **`role_resource_permissions` es decorativa** — permisos CRUD existen en la DB pero solo se retornan en `/me`. Ningún middleware los valida.
3. **Roles embebidos en el PASETO** — si cambias los roles de un usuario, el cambio no se refleja hasta que el token expira.
4. **No hay forma de crear admins** — el registro solo asigna `cliente`. No existe endpoint para `admin_sede`/`admin_global`.
5. **`sede_ids` nunca se valida** — un `admin_sede` puede editar datos de cualquier sede.
6. **`api_resources` incompleta** — solo 3 de ~40 endpoints están registrados.

## 2. Nuevo Paradigma

```
User ──1:1──> Profile ──N:N──> Role ──N:N──> Resource
```

### Flujo de autorización:

```
Request
  │
  ▼
PASETOAuth middleware
  │ Extrae: user_id, profile_id del token
  ▼
RequirePermission(store, "product:create") middleware
  │ 1. Busca profile_id en el PASETO
  │ 2. RBACStore.HasPermission(profileID, "product:create")
  │ 3. ¿Algún rol del profile tiene asignado el resource "product:create"?
  │ 4. Si no → 403
  ▼
Handler
```

### Cambios clave:

| Aspecto | Antes | Después |
|---------|-------|---------|
| Roles en token | `roles: ["admin_global"]` en PASETO | Solo `profile_id` en PASETO |
| Autorización | `RequireRoles("admin_global")` hardcoded | `RequirePermission(store, "product:create")` |
| Cache | Ninguna | In-memory `profileID → []resource_code` |
| CRUD RBAC | No existe | Endpoints para roles, resources, assignments |
| Permisos | `can_view`, `can_write`, etc. en tabla | El resource code ya incluye la acción |

## 3. Modelo de Datos

### 3.1 Schema nuevo

```sql
-- resources: tabla simple con code
CREATE TABLE resources (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(100) NOT NULL UNIQUE
);

-- roles: sin cambios
CREATE TABLE roles (
    id UUID PRIMARY KEY,
    code VARCHAR(30) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(300) NOT NULL
);

-- role_resources: join table simple (sin CRUD flags)
CREATE TABLE role_resources (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    role_id UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    resource_id UUID NOT NULL REFERENCES resources(id) ON DELETE CASCADE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(role_id, resource_id)
);

-- profile_role_assignments: profile → role
CREATE TABLE profile_role_assignments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    profile_id UUID NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    role_id UUID NOT NULL REFERENCES roles(id) ON DELETE RESTRICT,
    active BOOLEAN NOT NULL DEFAULT true,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_profile_role_assignments_profile_active ON profile_role_assignments (profile_id, active);
```

### 3.2 Seed de resources

Convención: `entidad:acción`

```
product:create
product:view
product:update
product:delete
combo:create
combo:view
combo:update
combo:delete
combo:publish
combo:pause
price:create
price:view
price:update
price:delete
sede:create
sede:view
security-policy:view
security-policy:update
rbac:resource:create
rbac:resource:view
rbac:resource:update
rbac:resource:delete
rbac:role:create
rbac:role:view
rbac:role:update
rbac:role:delete
rbac:profile:create
rbac:profile:view
rbac:profile:update
rbac:profile:delete
rbac:user:update
```

### 3.3 Seed de permisos por rol

**`admin_global`** — Todos los resources.

**`admin_sede`** — CRUD de productos, combos, precios, sedes. Sin RBAC ni security-policy.

**`cliente`** — Solo lectura: `product:view`, `combo:view`, `price:view`, `sede:view`.

### 3.4 PASETO claims

```go
type TokenClaims struct {
    Iss       string `json:"iss"`
    Aud       string `json:"aud"`
    Sub       string `json:"sub"`         // user_id
    ProfileID string `json:"profile_id"`  // NUEVO
    ClientID  string `json:"client_id"`
    Scope     string `json:"scope"`
    Email     string `json:"email"`
    Iat       int64  `json:"iat"`
    Exp       int64  `json:"exp"`
    Jti       string `json:"jti"`
}
```

Sin `roles` embebidos. Solo `profile_id`.

## 4. RBACStore — Cache en Memoria

### 4.1 Estructura

```go
type RBACStore struct {
    mu sync.RWMutex
    // profileID → set de resource codes
    profileResources map[uuid.UUID]map[string]struct{}
}

func (s *RBACStore) HasPermission(profileID uuid.UUID, resourceCode string) bool {
    s.mu.RLock()
    defer s.mu.RUnlock()
    resources, ok := s.profileResources[profileID]
    if !ok {
        return false
    }
    _, found := resources[resourceCode]
    return found
}
```

### 4.2 Query de carga

```sql
SELECT pra.profile_id, res.code
FROM profile_role_assignments pra
JOIN role_resources rr ON rr.role_id = pra.role_id
JOIN resources res ON res.id = rr.resource_id
WHERE pra.active = true
```

### 4.3 Operaciones

```go
func (s *RBACStore) LoadFromDB(ctx context.Context, pool *pgxpool.Pool) error
func (s *RBACStore) HasPermission(profileID uuid.UUID, resourceCode string) bool
func (s *RBACStore) GetPermissions(profileID uuid.UUID) []string
func (s *RBACStore) ReloadAll(ctx context.Context, pool *pgxpool.Pool) error
func (s *RBACStore) ReloadForProfile(ctx context.Context, pool *pgxpool.Pool, profileID uuid.UUID) error
func (s *RBACStore) InvalidateProfile(profileID uuid.UUID)
```

## 5. Middleware

### 5.1 `RequirePermission(store, resourceCode)`

```go
func RequirePermission(store *rbac.RBACStore, resourceCode string) func(http.Handler) http.Handler {
    return func(next http.Handler) http.Handler {
        return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
            profileID := GetProfileID(r.Context())
            if profileID == uuid.Nil {
                http.Error(w, `{"status":"error","message":"unauthorized"}`, 401)
                return
            }
            if !store.HasPermission(profileID, resourceCode) {
                http.Error(w, `{"status":"error","message":"insufficient_permissions"}`, 403)
                return
            }
            next.ServeHTTP(w, r)
        })
    }
}
```

### 5.2 Cambios en `PASETOAuth`

Agregar `ProfileIDKey` al context. Quitar `UserRolesKey`.

### 5.3 Helper

```go
func GetProfileID(ctx context.Context) uuid.UUID
```

## 6. Cambios en PASETO

### 6.1 `TokenClaims` — quitar `Roles`, agregar `ProfileID`

### 6.2 `GenerateAccessToken` — nuevo parámetro `profileID`, quitar `roles`

```go
func GenerateAccessToken(
    key []byte,
    cfg TokenConfig,
    userID uuid.UUID,
    profileID uuid.UUID,
    clientID string,
    scope string,
    email string,
) (string, error)
```

### 6.3 Buscar `profile_id` al generar tokens

En `Login`, `Refresh`, `ExchangeCode`, `RefreshToken`:

```go
var profileID uuid.UUID
err = s.pool.QueryRow(ctx, `SELECT id FROM profiles WHERE user_id = $1`, userID).Scan(&profileID)
```

## 7. CRUD de RBAC

### 7.1 Paquete `internal/rbac/`

```
internal/rbac/
├── store.go       ← RBACStore (cache en memoria)
├── store_test.go  ← Tests
├── service.go     ← CRUD roles, resources, assignments, permissions
├── handler.go     ← HTTP handlers
└── models.go      ← DTOs
```

### 7.2 Endpoints

#### Resources (5 — CRUD estándar)
| Method | Path | Descripción | Resource |
|--------|------|-------------|----------|
| GET | `/api/v1/rbac/resources` | Lista resources | `rbac:resource:view` |
| GET | `/api/v1/rbac/resources/{id}` | Obtiene resource | `rbac:resource:view` |
| POST | `/api/v1/rbac/resources` | Crea resource | `rbac:resource:create` |
| PUT | `/api/v1/rbac/resources/{id}` | Actualiza (solo code) | `rbac:resource:update` |
| DELETE | `/api/v1/rbac/resources/{id}` | Elimina resource | `rbac:resource:delete` |

#### Roles (7 — CRUD + asignar/eliminar resources)
| Method | Path | Descripción | Resource |
|--------|------|-------------|----------|
| GET | `/api/v1/rbac/roles` | Lista roles con sus resources | `rbac:role:view` |
| GET | `/api/v1/rbac/roles/{id}` | Obtiene rol con sus resources | `rbac:role:view` |
| POST | `/api/v1/rbac/roles` | Crea rol (sin resources aún) | `rbac:role:create` |
| PUT | `/api/v1/rbac/roles/{id}` | Actualiza code, name, description | `rbac:role:update` |
| DELETE | `/api/v1/rbac/roles/{id}` | Elimina rol | `rbac:role:delete` |
| POST | `/api/v1/rbac/roles/{id}/resources` | Asigna 1 resource al rol | `rbac:role:update` |
| DELETE | `/api/v1/rbac/roles/{id}/resources/{resourceId}` | Elimina 1 resource del rol | `rbac:role:update` |

Body de `POST .../resources`:
```json
{ "resource_id": "uuid" }
```

#### Profiles (7 — CRUD + asignar/eliminar roles)
| Method | Path | Descripción | Resource |
|--------|------|-------------|----------|
| GET | `/api/v1/rbac/profiles` | Lista profiles con sus roles | `rbac:profile:view` |
| GET | `/api/v1/rbac/profiles/{id}` | Obtiene profile con sus roles | `rbac:profile:view` |
| POST | `/api/v1/rbac/profiles` | Crea profile (sin roles aún) | `rbac:profile:create` |
| PUT | `/api/v1/rbac/profiles/{id}` | Actualiza datos del profile | `rbac:profile:update` |
| DELETE | `/api/v1/rbac/profiles/{id}` | Elimina profile | `rbac:profile:delete` |
| POST | `/api/v1/rbac/profiles/{id}/roles` | Asigna 1 role al profile | `rbac:profile:update` |
| DELETE | `/api/v1/rbac/profiles/{id}/roles/{roleId}` | Elimina 1 role del profile | `rbac:profile:update` |

Body de `POST .../roles`:
```json
{ "role_id": "uuid" }
```

#### Users (1 — asignar profile)
| Method | Path | Descripción | Resource |
|--------|------|-------------|----------|
| PUT | `/api/v1/users/{id}/profile` | Asigna/cambia profile | `rbac:user:update` |

Body:
```json
{ "profile_id": "uuid" }
```

**Total: 20 endpoints**

### 7.3 Recarga del cache

Cada handler que modifica RBAC llama a `store.ReloadAll()` después del cambio.

### 7.4 DTOs

```go
// Resources
type CreateResourceRequest struct {
    Code string `json:"code"`
}

type UpdateResourceRequest struct {
    Code string `json:"code"`
}

type ResourceResponse struct {
    ID   uuid.UUID `json:"id"`
    Code string    `json:"code"`
}

// Roles
type CreateRoleRequest struct {
    Code        string `json:"code"`
    Name        string `json:"name"`
    Description string `json:"description"`
}

type UpdateRoleRequest struct {
    Code        string `json:"code"`
    Name        string `json:"name"`
    Description string `json:"description"`
}

type RoleResponse struct {
    ID          uuid.UUID        `json:"id"`
    Code        string           `json:"code"`
    Name        string           `json:"name"`
    Description string           `json:"description"`
    Resources   []ResourceEntry  `json:"resources"`
}

// Asignar resource a role
type AssignResourceRequest struct {
    ResourceID uuid.UUID `json:"resource_id"`
}

// Profiles
type CreateProfileRequest struct {
    FirstName        string  `json:"first_name"`
    LastName         string  `json:"last_name"`
    IdentityDocument string  `json:"identity_document"`
    Rif              *string `json:"rif"`
    WhatsAppPhone    string  `json:"whatsapp_phone"`
    FullAddress      string  `json:"full_address"`
}

type UpdateProfileRequest struct {
    FirstName        string  `json:"first_name"`
    LastName         string  `json:"last_name"`
    IdentityDocument string  `json:"identity_document"`
    Rif              *string `json:"rif"`
    WhatsAppPhone    string  `json:"whatsapp_phone"`
    FullAddress      string  `json:"full_address"`
}

type ProfileResponse struct {
    ID               uuid.UUID   `json:"id"`
    UserID           *uuid.UUID  `json:"user_id,omitempty"`
    FirstName        string      `json:"first_name"`
    LastName         string      `json:"last_name"`
    IdentityDocument string      `json:"identity_document"`
    Rif              *string     `json:"rif"`
    WhatsAppPhone    string      `json:"whatsapp_phone"`
    FullAddress      string      `json:"full_address"`
    Roles            []RoleEntry `json:"roles"`
    CreatedAt        time.Time   `json:"created_at_utc"`
    UpdatedAt        time.Time   `json:"updated_at_utc"`
}

type RoleEntry struct {
    ID   uuid.UUID `json:"id"`
    Code string    `json:"code"`
    Name string    `json:"name"`
}

// Asignar role a profile
type AssignRoleRequest struct {
    RoleID uuid.UUID `json:"role_id"`
}

// Asignar profile a user
type AssignProfileRequest struct {
    ProfileID uuid.UUID `json:"profile_id"`
}

// Shared
type ResourceEntry struct {
    ID   uuid.UUID `json:"id"`
    Code string    `json:"code"`
}
```

## 8. Cambios en `server.go`

```go
// ANTES
r.Use(middleware.RequireRoles("admin_global", "admin_sede"))

// DESPUÉS
r.Use(middleware.RequirePermission(rbacStore, "product:create"))
```

### Mapping rutas → resources:

| Ruta | Resource Code |
|------|---------------|
| `POST /productos` | `product:create` |
| `PUT /productos/{id}` | `product:update` |
| `DELETE /productos/{id}` | `product:delete` |
| `POST /combos` | `combo:create` |
| `PUT /combos/{id}` | `combo:update` |
| `DELETE /combos/{id}` | `combo:delete` |
| `POST /combos/{id}/publicar` | `combo:publish` |
| `POST /combos/{id}/pausar` | `combo:pause` |
| `POST /precios` | `price:create` |
| `PUT /precios/{id}` | `price:update` |
| `DELETE /precios/{id}` | `price:delete` |
| `POST /sedes` | `sede:create` |
| `PUT /security-policy` | `security-policy:update` |
| RBAC CRUD | `rbac:*` (según endpoint) |
| `POST /rbac/roles/{id}/resources` | `rbac:role:update` |
| `DELETE /rbac/roles/{id}/resources/{resourceId}` | `rbac:role:update` |
| `POST /rbac/profiles/{id}/roles` | `rbac:profile:update` |
| `DELETE /rbac/profiles/{id}/roles/{roleId}` | `rbac:profile:update` |
| `PUT /users/{id}/profile` | `rbac:user:update` |

## 9. Cambios en Auth

### 9.1 `Login` — buscar profileID, quitar roles del token

### 9.2 `Me` — consultar permisos del store

```go
func (s *Service) Me(ctx context.Context, accessToken string, rbacStore *rbac.RBACStore) (*MeResponse, error) {
    // ... validar token ...
    resourceCodes := rbacStore.GetPermissions(profileID)
    // Mapear a response
}
```

### 9.3 `Refresh` — buscar profileID

### 9.4 Eliminar `getUserRoles` y `getUserPermissions` de service.go y oauth2.go

### 9.5 Seeder — asignar rol via `profile_role_assignments`

El seeder crea user + profile. La asignación de rol se hace insertando en `profile_role_assignments(profile_id, role_id)` en vez de `user_role_assignments(user_id, role_id)`.

## 10. Seeder

El seeder del superuser crea user + profile. La asignación de rol se inserta en `profile_role_assignments(profile_id, role_id)` en vez de `user_role_assignments`. Los permisos se configuran en la migración SQL.

## 11. Archivos

### Nuevos
| Archivo | Contenido |
|---------|-----------|
| `internal/rbac/store.go` | RBACStore |
| `internal/rbac/store_test.go` | Tests |
| `internal/rbac/service.go` | CRUD resources, roles, profiles, user profile assignment |
| `internal/rbac/handler.go` | HTTP handlers |
| `internal/rbac/models.go` | DTOs |
| `migrations/000002_rbac.up.sql` | Schema nuevo + seed |
| `migrations/000002_rbac.down.sql` | Rollback |

### Modificar
| Archivo | Cambios |
|---------|---------|
| `internal/auth/paseto.go` | `ProfileID` en claims, quitar `Roles`, nuevo param en `GenerateAccessToken` |
| `internal/auth/service.go` | Login/Refresh/Me buscan profileID, usan store. Eliminar `getUserRoles`, `getUserPermissions` |
| `internal/auth/oauth2.go` | ExchangeCode/RefreshToken buscan profileID. Eliminar `getUserRoles` |
| `internal/http/middleware/auth.go` | `ProfileIDKey`, `GetProfileID`, `RequirePermission` |
| `internal/http/server.go` | Reemplazar `RequireRoles` por `RequirePermission`. Registrar rutas RBAC (resources, roles, profiles, user profile) |
| `cmd/server/main.go` | Crear RBACStore, inyectar |
| `internal/db/seed.go` | Ajustar seed: insertar en `profile_role_assignments` en vez de `user_role_assignments` |

## 12. Orden de Implementación

1. **Migración SQL** — schema `resources`, `role_resources`, `profile_role_assignments`, seed completo, migrar datos de `user_role_assignments` ✅
2. **RBACStore** — `store.go` ✅
3. **PASETO** — `ProfileID` en claims, quitar `Roles` ✅
4. **Auth service** — Login/Refresh/Me con profileID + store ✅
5. **OAuth2 service** — ExchangeCode/RefreshToken con profileID ✅
6. **Middleware** — `RequirePermission` ✅
7. **RBAC CRUD** — service + handler (resources CRUD, roles CRUD + assign/remove resource, profiles CRUD + assign/remove role, user profile assignment) ✅
8. **Server wiring** — reemplazar RequireRoles, registrar rutas RBAC ✅
9. **Main wiring** — inyectar store ✅
10. **Seeder update** ✅
11. **Tests** — build, vet ✅

## 13. Notas de Diseño

### 13.1 ¿Por qué resource code incluye la acción?

`"product:create"` en vez de `resource="product"` + `can_write=true`. Ventajas:
- Más granular: `combo:publish` y `combo:pause` son acciones distintas, no genéricas CRUD
- Más simple: la asignación `role → resource` **es** el permiso. Sin flags adicionales.
- Más legible: el middleware dice exactamente qué se protege

### 13.2 ¿Por qué in-memory y no Redis?

- Más simple para empezar
- Cero dependencias externas
- Volumen bajo (< 10k usuarios)
- Futuro: migrar a Redis sin cambiar la interfaz del store

### 13.3 ¿Por qué `profile_id` y no `user_id`?

- El modelo es `User → Profile → Roles`
- Un usuario tiene exactamente un profile (1:1)
- El profile es la entidad que se relaciona con roles
- Preparado para multi-tenant futuro

### 13.4 Backward compatibility

- `user_role_assignments` se reemplaza por `profile_role_assignments`. La migración migra los datos existentes haciendo JOIN a `profiles` para obtener el `profile_id`.
- Tokens viejos (con `roles` embebidos) se siguen validando — el campo `roles` se ignora
- Tokens nuevos (con `profile_id`) usan el store
- Si un token no tiene `profile_id`, el middleware retorna 401 → usuario debe re-login

### 13.5 Recarga automática

```go
// En cada handler que modifica RBAC:
func (h *RBACHandler) CreateRole(w http.ResponseWriter, r *http.Request) {
    // ... crear rol en DB ...
    h.store.ReloadAll(r.Context(), h.pool)
    // ... responder ...
}
```

### 13.6 Endpoints públicos (sin RequirePermission)

- `GET /`, `/health`, `/api/health`
- `/.well-known/openid-configuration`
- `POST /api/v1/auth/register/*`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/forgot-password`
- `POST /api/v1/auth/reset-password`
- `GET /api/v1/auth/security-policy`
- `GET /api/v1/productos`, `GET /api/v1/productos/{id}`
- `GET /api/v1/combos`, `GET /api/v1/combos/{id}`
- `GET /api/v1/precios/*`
- `GET /api/v1/sedes`
- `POST /api/v1/sync/*` (API Key)
- `GET /oauth2/*`

## 14. Riesgos

| Riesgo | Mitigación |
|--------|------------|
| Cache stale | Recarga síncrona en el mismo request |
| Token sin `profile_id` | Middleware retorna 401, usuario re-login |
| Performance | Lookup O(permissions) por usuario, RWMutex para concurrencia |
| Resource code no existe en store | `HasPermission` retorna false → 403 |
| Eliminar rol con assignments | `role_resources` CASCADE, `profile_role_assignments` RESTRICT |
| Eliminar profile con assignments | `profile_role_assignments` CASCADE via `profiles(id)` |
