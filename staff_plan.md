# Plan: Modulo de Personal (Staff Management)

## 1. Objetivo

Crear un modulo para que administradores y gerentes gestionen miembros del personal:
- Crear, editar, eliminar miembros del personal
- Asignar sucursal y rol (staff o manager)
- Excluir perfiles con roles `admin` o `client`

### Reglas de negocio

| Regla | Detalle |
|-------|---------|
| Un usuario pertenece a UNA sucursal | FK directo `branch_id` en tabla `users` |
| Roles de personal | `staff` (basico) y `manager` (gerente de sucursal) |
| Exclusion | Perfiles con roles `global_admin`, `branch_admin` o `client` NO aparecen en la lista |
| Permisos | Solo `global_admin` y `branch_admin` pueden gestionar personal |

---

## 2. Cambios de esquema

### 2.1 Simplificar tabla `profiles`

**Estado actual:**
```
profiles(id, user_id, first_name, last_name, identity_document, tax_id, whatsapp_phone, full_address, created_at_utc, updated_at_utc)
```

**Estado nuevo:**
```
profiles(id, user_id FK, name, code UNIQUE, created_at_utc, updated_at_utc)
```

- `name` reemplaza `first_name` + `last_name` (campo unico)
- `code` es un identificador unico para el perfil (ej: cedula, username)
- Se eliminan `identity_document`, `tax_id`, `whatsapp_phone`, `full_address` de profiles

### 2.2 Mover campos personales a `users`

Los campos que se eliminan de `profiles` se mueven a `users`:

**Estado actual `users`:**
```
users(id, email, password_hash, onboarding_status, email_verified_at_utc, status, created_at_utc)
```

**Estado nuevo `users`:**
```
users(id, email, password_hash, onboarding_status, email_verified_at_utc, status, created_at_utc, branch_id FK, identity_document, tax_id, whatsapp_phone, full_address)
```

Campos agregados:
- `branch_id` UUID FK → branches(id) ON DELETE SET NULL
- `identity_document` VARCHAR(40) — cedula de identidad
- `tax_id` VARCHAR(40) — RIF (nullable)
- `whatsapp_phone` VARCHAR(30) — telefono WhatsApp
- `full_address` VARCHAR(500) — direccion completa

---

## 3. Backend — Migracion 000008

### 3.1 Agregar campos a `users`

```sql
ALTER TABLE users ADD COLUMN branch_id UUID REFERENCES branches(id) ON DELETE SET NULL;
ALTER TABLE users ADD COLUMN identity_document VARCHAR(40);
ALTER TABLE users ADD COLUMN tax_id VARCHAR(40);
ALTER TABLE users ADD COLUMN whatsapp_phone VARCHAR(30);
ALTER TABLE users ADD COLUMN full_address VARCHAR(500);
CREATE INDEX idx_users_branch_id ON users(branch_id);
```

### 3.2 Migrar datos de `profiles` a `users`

```sql
UPDATE users u SET
    identity_document = p.identity_document,
    tax_id = p.tax_id,
    whatsapp_phone = p.whatsapp_phone,
    full_address = p.full_address
FROM profiles p
WHERE p.user_id = u.id;
```

### 3.3 Agregar `name` y `code` a `profiles`

```sql
ALTER TABLE profiles ADD COLUMN name VARCHAR(200);
ALTER TABLE profiles ADD COLUMN code VARCHAR(100);

-- Migrar datos existentes
UPDATE profiles SET
    name = first_name || ' ' || last_name,
    code = identity_document;

-- Hacer campos NOT NULL despues de migrar datos
ALTER TABLE profiles ALTER COLUMN name SET NOT NULL;
ALTER TABLE profiles ALTER COLUMN code SET NOT NULL;
CREATE UNIQUE INDEX idx_profiles_code ON profiles (code);
```

### 3.4 Eliminar columnas redundantes de `profiles`

```sql
ALTER TABLE profiles DROP COLUMN first_name;
ALTER TABLE profiles DROP COLUMN last_name;
ALTER TABLE profiles DROP COLUMN identity_document;
ALTER TABLE profiles DROP COLUMN tax_id;
ALTER TABLE profiles DROP COLUMN whatsapp_phone;
ALTER TABLE profiles DROP COLUMN full_address;
```

### 3.5 Crear nuevos roles `staff` y `manager`

```sql
INSERT INTO roles (id, code, name, description) VALUES
    ('44444444-4444-4444-4444-444444444444', 'staff', 'staff', 'Staff member with basic operations access'),
    ('55555555-5555-5555-5555-555555555555', 'manager', 'manager', 'Branch manager with elevated access')
ON CONFLICT (code) DO NOTHING;
```

### 3.6 Asignar permisos a los nuevos roles

**`staff`** — permisos basicos:
- `product:view`, `bundle:view`, `price:view`, `branch:view`
- `order:view`, `order:create`, `order:status:change`

**`manager`** — todo lo de staff mas:
- `product:create`, `product:update`
- `bundle:create`, `bundle:update`, `bundle:publish`, `bundle:pause`
- `price:create`, `price:update`
- `order:update`, `order:delete`

### 3.7 Seed de resources nuevos

```sql
INSERT INTO resources (id, code) VALUES
    (gen_random_uuid(), 'staff:create'),
    (gen_random_uuid(), 'staff:view'),
    (gen_random_uuid(), 'staff:update'),
    (gen_random_uuid(), 'staff:delete')
ON CONFLICT (code) DO NOTHING;

-- Asignar a global_admin
INSERT INTO role_resources (role_id, resource_id)
SELECT '33333333-3333-3333-3333-333333333333', id FROM resources
WHERE code IN ('staff:create', 'staff:view', 'staff:update', 'staff:delete')
ON CONFLICT (role_id, resource_id) DO NOTHING;

-- Asignar a branch_admin
INSERT INTO role_resources (role_id, resource_id)
SELECT '22222222-2222-2222-2222-222222222222', id FROM resources
WHERE code IN ('staff:create', 'staff:view', 'staff:update', 'staff:delete')
ON CONFLICT (role_id, resource_id) DO NOTHING;
```

### 3.8 Actualizar unique index de identity_document

El unique index `idx_profiles_identity_document` ya no existe en profiles. Verificar que no haya conflicto.

### 3.9 Down migration

```sql
-- Restaurar columnas en profiles
ALTER TABLE profiles ADD COLUMN first_name VARCHAR(120);
ALTER TABLE profiles ADD COLUMN last_name VARCHAR(120);
ALTER TABLE profiles ADD COLUMN identity_document VARCHAR(40);
ALTER TABLE profiles ADD COLUMN tax_id VARCHAR(40);
ALTER TABLE profiles ADD COLUMN whatsapp_phone VARCHAR(30);
ALTER TABLE profiles ADD COLUMN full_address VARCHAR(500);

-- Migrar datos de vuelta
UPDATE profiles p SET
    first_name = split_part(p.name, ' ', 1),
    last_name = CASE WHEN position(' ' in p.name) > 0 THEN substring(p.name from position(' ' in p.name) + 1) ELSE '' END,
    identity_document = p.code,
    tax_id = u.tax_id,
    whatsapp_phone = u.whatsapp_phone,
    full_address = u.full_address
FROM users u WHERE u.id = p.user_id;

-- Eliminar columnas nuevas de profiles
ALTER TABLE profiles DROP COLUMN name;
ALTER TABLE profiles DROP COLUMN code;

-- Eliminar columnas de users
ALTER TABLE users DROP COLUMN branch_id;
ALTER TABLE users DROP COLUMN identity_document;
ALTER TABLE users DROP COLUMN tax_id;
ALTER TABLE users DROP COLUMN whatsapp_phone;
ALTER TABLE users DROP COLUMN full_address;

-- Eliminar roles y resources
DELETE FROM role_resources WHERE role_id IN (SELECT id FROM roles WHERE code IN ('staff', 'manager'));
DELETE FROM roles WHERE code IN ('staff', 'manager');
DELETE FROM resources WHERE code IN ('staff:create', 'staff:view', 'staff:update', 'staff:delete');
```

---

## 4. Backend — Actualizar structs existentes

### 4.1 `internal/pricing/service.go`

No cambia — products y bundles no referencian profiles directamente.

### 4.2 `internal/rbac/models.go`

Actualizar structs de profile:

```go
type ProfileResponse struct {
    ID        uuid.UUID   `json:"id"`
    UserID    *uuid.UUID  `json:"user_id,omitempty"`
    Name      string      `json:"name"`
    Code      string      `json:"code"`
    Roles     []RoleEntry `json:"roles"`
    CreatedAt time.Time   `json:"created_at_utc"`
    UpdatedAt time.Time   `json:"updated_at_utc"`
}

type CreateProfileRequest struct {
    Name string `json:"name" validate:"required"`
    Code string `json:"code" validate:"required"`
}

type UpdateProfileRequest struct {
    Name string `json:"name"`
    Code string `json:"code"`
}
```

### 4.3 `internal/rbac/service.go`

Actualizar queries de profiles para usar las nuevas columnas.

### 4.4 `internal/auth/service.go`

Actualizar `CompleteProfile` para:
- Insertar en `users` con los nuevos campos (`identity_document`, `tax_id`, `whatsapp_phone`, `full_address`, `branch_id`)
- Insertar en `profiles` con `name` (concatenar first_name + last_name) y `code` (usar identity_document)

### 4.5 `internal/db/seed.go`

Actualizar seed del superusuario para usar la nueva estructura de profiles.

---

## 5. Backend — Servicio de Personal

### 5.1 Nuevo paquete `internal/staff/`

**Archivo:** `internal/staff/service.go`

#### Structs

```go
type StaffMember struct {
    UserID           uuid.UUID  `json:"user_id"`
    Email            string     `json:"email"`
    Status           string     `json:"status"`
    BranchID         *uuid.UUID `json:"branch_id"`
    BranchName       *string    `json:"branch_name"`
    IdentityDocument string     `json:"identity_document"`
    WhatsAppPhone    string     `json:"whatsapp_phone"`
    ProfileID        uuid.UUID  `json:"profile_id"`
    ProfileName      string     `json:"profile_name"`
    ProfileCode      string     `json:"profile_code"`
    RoleCode         string     `json:"role_code"`
    RoleName         string     `json:"role_name"`
    CreatedAtUtc     time.Time  `json:"created_at_utc"`
}

type CreateStaffRequest struct {
    Email            string    `json:"email" validate:"required"`
    Password         string    `json:"password" validate:"required"`
    Name             string    `json:"name" validate:"required"`      // nombre del perfil
    Code             string    `json:"code" validate:"required"`      // codigo unico del perfil
    IdentityDocument string    `json:"identity_document" validate:"required"`
    WhatsAppPhone    string    `json:"whatsapp_phone" validate:"required"`
    FullAddress      string    `json:"full_address" validate:"required"`
    BranchID         uuid.UUID `json:"branch_id" validate:"required"`
    RoleCode         string    `json:"role_code" validate:"required"` // "staff" o "manager"
}

type UpdateStaffRequest struct {
    Name             string     `json:"name"`
    Code             string     `json:"code"`
    IdentityDocument string     `json:"identity_document"`
    WhatsAppPhone    string     `json:"whatsapp_phone"`
    FullAddress      string     `json:"full_address"`
    BranchID         *uuid.UUID `json:"branch_id"`
    RoleCode         *string    `json:"role_code"`
    Status           *string    `json:"status"`
}

type StaffFilter struct {
    Limit    int
    Offset   int
    BranchID *uuid.UUID
    RoleCode *string
    Search   *string
}
```

#### Metodos

```go
func (s *Service) List(ctx context.Context, filter StaffFilter) ([]StaffMember, int, error)
func (s *Service) GetByID(ctx context.Context, userID uuid.UUID) (*StaffMember, error)
func (s *Service) Create(ctx context.Context, req CreateStaffRequest) (*StaffMember, error)
func (s *Service) Update(ctx context.Context, userID uuid.UUID, req UpdateStaffRequest) (*StaffMember, error)
func (s *Service) Delete(ctx context.Context, userID uuid.UUID) error
```

#### Logica del `List`

```sql
SELECT u.id, u.email, u.status, u.branch_id, b.store_name,
       u.identity_document, u.whatsapp_phone,
       p.id, p.name, p.code,
       r.code, r.name, u.created_at_utc
FROM users u
JOIN profiles p ON p.user_id = u.id
JOIN profile_role_assignments pra ON pra.profile_id = p.id AND pra.active = true
JOIN roles r ON r.id = pra.role_id
LEFT JOIN branches b ON b.id = u.branch_id
WHERE r.code NOT IN ('global_admin', 'branch_admin', 'client')
  AND ($1::uuid IS NULL OR u.branch_id = $1)
  AND ($2::text IS NULL OR r.code = $2)
  AND ($3::text IS NULL OR p.name ILIKE '%' || $3 || '%'
       OR u.email ILIKE '%' || $3 || '%')
ORDER BY u.created_at_utc DESC
LIMIT $4 OFFSET $5
```

#### Logica del `Create`

1. Hash password con PBKDF2
2. INSERT en `users` con `branch_id`, `identity_document`, `whatsapp_phone`, `full_address`
3. INSERT en `profiles` con `name`, `code`, `user_id`
4. Buscar rol por `code`
5. INSERT en `profile_role_assignments`
6. Recargar RBAC store

#### Logica del `Update`

- Actualizar campos en `users` (identity_document, whatsapp_phone, full_address, branch_id, status)
- Actualizar campos en `profiles` (name, code)
- Si cambia `role_code`, eliminar asignacion anterior y crear nueva
- Recargar RBAC store

#### Logica del `Delete`

- DELETE de profile_role_assignments
- DELETE de profiles
- DELETE de users
- Recargar RBAC store

---

## 6. Backend — Handler

**Archivo:** `internal/http/handler/staff.go`

| Metodo | Endpoint | Permiso |
|--------|----------|---------|
| GET | `/api/v1/staff/` | public o auth + `staff:view` |
| GET | `/api/v1/staff/{id}` | auth + `staff:view` |
| POST | `/api/v1/staff/` | auth + `staff:create` |
| PUT | `/api/v1/staff/{id}` | auth + `staff:update` |
| DELETE | `/api/v1/staff/{id}` | auth + `staff:delete` |

### Wiring

`server.go`: import `staff`, crear `StaffHandler`, registrar rutas
`main.go`: import `staff`, crear `StaffService(pool, rbacStore)`, pasar a `NewServer`

---

## 7. Frontend — Modelo

**Archivo:** `web/src/app/core/models/staff.model.ts`

```typescript
export interface StaffMember {
  userId: string;
  email: string;
  status: string;
  branchId: string | null;
  branchName: string | null;
  identityDocument: string;
  whatsappPhone: string;
  profileId: string;
  profileName: string;
  profileCode: string;
  roleCode: string;
  roleName: string;
  createdAtUtc: string;
}

export interface CreateStaffRequest {
  email: string;
  password: string;
  name: string;
  code: string;
  identity_document: string;
  whatsapp_phone: string;
  full_address: string;
  branch_id: string;
  role_code: string;
}

export interface UpdateStaffRequest {
  name?: string;
  code?: string;
  identity_document?: string;
  whatsapp_phone?: string;
  full_address?: string;
  branch_id?: string;
  role_code?: string;
  status?: string;
}

export interface StaffListFilter {
  limit?: number;
  offset?: number;
  branch_id?: string;
  role_code?: string;
  search?: string;
}
```

---

## 8. Frontend — Servicio

**Archivo:** `web/src/app/core/services/staff-api.service.ts`

```typescript
@Injectable({ providedIn: 'root' })
export class StaffApiService {
  list(filter?: StaffListFilter): Observable<PaginatedResponse<StaffMember>>
  getById(id: string): Observable<StaffMember>
  create(req: CreateStaffRequest): Observable<StaffMember>
  update(id: string, req: UpdateStaffRequest): Observable<StaffMember>
  delete(id: string): Observable<void>
}
```

---

## 9. Frontend — Componentes

### 9.1 StaffPageComponent

**Archivos:**
- `pages/admin/staff/staff-page.component.ts`
- `pages/admin/staff/staff-page.component.html`
- `pages/admin/staff/staff-page.component.scss`

**Tabla PrimeNG** con:
- Columnas: Nombre, Email, Sucursal, Rol, Estado, Acciones
- Filtros: sucursal (Select), rol (Select: staff/manager), search (InputText)
- Paginacion server-side
- Acciones: Editar, Eliminar (con confirmacion)
- Boton "Nuevo Miembro"

### 9.2 StaffFormDialogComponent

**Archivos:**
- `pages/admin/staff/staff-form-dialog.component.ts`
- `pages/admin/staff/staff-form-dialog.component.html`
- `pages/admin/staff/staff-form-dialog.component.scss`

**Campos:**
| Campo | Tipo | Requerido | Notas |
|-------|------|-----------|-------|
| `email` | InputText | SI | Solo en creacion |
| `password` | Password | SI | Solo en creacion, min 8 caracteres |
| `name` | InputText | SI | Nombre completo del perfil |
| `code` | InputText | SI | Codigo unico (cedula, username) |
| `identity_document` | InputText | SI | Cedula de identidad |
| `whatsapp_phone` | InputText | SI | Numero de WhatsApp |
| `full_address` | InputText | SI | Direccion completa |
| `branch_id` | Select | SI | Carga sucursales desde BranchApiService |
| `role_code` | Select | SI | Opciones: staff, manager |

---

## 10. Frontend — Integracion

### 10.1 Rutas

Agregar en `app.routes.ts`:
```typescript
{ path: 'staff', component: StaffPageComponent, canActivate: [permissionGuard('staff:view')] },
```

### 10.2 Sidebar

Agregar link en `admin-layout.component.html`:
```html
@if (canViewStaff()) {
  <a routerLink="/admin/staff" routerLinkActive="active">
    <span class="pi pi-users"></span>
    <span>Personal</span>
  </a>
}
```

En `admin-layout.component.ts`:
```typescript
protected readonly canViewStaff = computed(() =>
  this.authStore.hasPermission('staff:view')
);
```

### 10.3 Header

Agregar `staff:view` al computed `isAdmin` en `header.component.ts`.

---

## 11. Archivos a crear/modificar

### Backend — Archivos nuevos

| # | Archivo | Descripcion |
|---|---------|-------------|
| 1 | `migrations/000008_add_staff.up.sql` | Simplificar profiles, mover campos a users, roles staff/manager, resources |
| 2 | `migrations/000008_add_staff.down.sql` | Rollback |
| 3 | `internal/staff/service.go` | CRUD de personal |
| 4 | `internal/http/handler/staff.go` | Handler HTTP |

### Backend — Archivos modificados

| # | Archivo | Cambio |
|---|---------|--------|
| 1 | `internal/rbac/models.go` | Simplificar ProfileResponse, CreateProfileRequest, UpdateProfileRequest |
| 2 | `internal/rbac/service.go` | Actualizar queries de profiles |
| 3 | `internal/auth/service.go` | Actualizar CompleteProfile para nueva estructura |
| 4 | `internal/db/seed.go` | Actualizar seed superuser |
| 5 | `internal/http/server.go` | Import staff, crear handler, registrar rutas |
| 6 | `cmd/main/main.go` | Import staff, crear servicio, pasar a NewServer |

### Frontend — Archivos nuevos

| # | Archivo | Descripcion |
|---|---------|-------------|
| 1 | `core/models/staff.model.ts` | Interfaces |
| 2 | `core/services/staff-api.service.ts` | Servicio HTTP |
| 3 | `pages/admin/staff/staff-page.component.ts` | Lista |
| 4 | `pages/admin/staff/staff-page.component.html` | Template lista |
| 5 | `pages/admin/staff/staff-page.component.scss` | Estilos lista |
| 6 | `pages/admin/staff/staff-form-dialog.component.ts` | Formulario |
| 7 | `pages/admin/staff/staff-form-dialog.component.html` | Template form |
| 8 | `pages/admin/staff/staff-form-dialog.component.scss` | Estilos form |

### Frontend — Archivos modificados

| # | Archivo | Cambio |
|---|---------|--------|
| 1 | `app.routes.ts` | Agregar ruta `/admin/staff` |
| 2 | `pages/admin/admin-layout.component.ts` | Agregar `canViewStaff` |
| 3 | `pages/admin/admin-layout.component.html` | Link "Personal" |
| 4 | `shared/header/header.component.ts` | `staff:view` en `isAdmin` |

---

## 12. Orden de implementacion

### Fase 1: Backend — Esquema
1. Crear migracion 000008 (simplificar profiles, mover campos a users, crear roles)
2. Actualizar `internal/rbac/models.go`
3. Actualizar `internal/rbac/service.go` (queries de profiles)
4. Actualizar `internal/auth/service.go` (CompleteProfile)
5. Actualizar `internal/db/seed.go`

### Fase 2: Backend — Staff module
6. Crear `internal/staff/service.go`
7. Crear `internal/http/handler/staff.go`
8. Actualizar `server.go` y `main.go`
9. Build y verificar

### Fase 3: Frontend
10. Crear modelo `staff.model.ts`
11. Crear servicio `staff-api.service.ts`
12. Crear `StaffPageComponent` (ts + html + scss)
13. Crear `StaffFormDialogComponent` (ts + html + scss)
14. Actualizar rutas, sidebar, header

### Fase 4: Verificacion
15. Typecheck frontend
16. Build backend
