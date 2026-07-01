# Plan: Completar CRUD de Órdenes (Admin) ✅ IMPLEMENTADO

## Estado Actual
- **List endpoint**: `GET /api/v1/orders/` ✅ (requiere `order:view`)
- **Get by ID**: `GET /api/v1/orders/{id}` ✅
- **Create**: `POST /api/v1/orders/` ✅ (requiere `order:create`)
- **Update**: `PUT /api/v1/orders/{id}` ✅ (requiere `order:update`)
- **Delete**: `DELETE /api/v1/orders/{id}` ✅ (requiere `order:delete`, cambia status a CANCELLED)
- **Change status**: `POST /api/v1/orders/{id}/status` ✅ (requiere `order:status:change`)
- **Frontend admin orders page**: list, detail dialog, status change, delete ✅
- **Frontend create form dialog**: ✅ Implementado
- **User search/list endpoint**: ✅ Implementado

---

## Archivos creados/modificados

### Backend (nuevos)
- `api/internal/user/service.go` — `List` + `Search` con raw pgx queries
- `api/internal/http/handler/user.go` — `List` + `Search` handlers

### Backend (modificados)
- `api/internal/http/server.go` — NewServer acepta `userSvc`, rutas `/api/v1/users/` y `/search`
- `api/main/main.go` — wire `user.NewService(pool)` → `NewServer`
- `api/internal/order/service.go` — validación de `product_id`/`bundle_id` no-nil según item_type

### Frontend (nuevos)
- `web/src/app/core/models/user.model.ts` — `AppUser` interface
- `web/src/app/core/services/user-api.service.ts` — `search()` + `list()`
- `web/src/app/pages/admin/orders/order-form-dialog.component.ts` — form dialog
- `web/src/app/pages/admin/orders/order-form-dialog.component.html` — template
- `web/src/app/pages/admin/orders/order-form-dialog.component.scss` — estilos

### Frontend (modificados)
- `web/src/app/core/models/order.model.ts` — `CreateOrderItem.bundle_id` y `product_id` opcionales
- `web/src/app/shared/cart-dialog/cart-dialog.component.ts` — omitir `bundle_id` en request
- `web/src/app/pages/admin/orders/orders-page.component.ts` — botón crear + dialog
- `web/src/app/pages/admin/orders/orders-page.component.html` — template actualizado
- `web/src/app/pages/admin/orders/orders-page.component.scss` — flex header

---

## Fix adicional: `bundle_id: ''` → omitir

El campo `bundle_id` en `CreateOrderItem` se envía como `''` (string vacío) desde el cart dialog.
`google/uuid.UUID` implementa `TextUnmarshaler` y `ParseBytes([]byte(""))` falla con `invalid UUID length: 0`.

**Fix**: `bundle_id` y `product_id` ahora son opcionales en el modelo TypeScript.
El cart dialog y el order form dialog omiten `bundle_id` para items de tipo `product`.
El backend valida que `product_id` no sea `uuid.Nil` cuando `item_type == "product"`.

---

## 1. Backend: Endpoint de búsqueda de usuarios

### Por qué
El form de creación de orden requiere seleccionar un `client_user_id`. Actualmente no hay forma de listar/buscar usuarios desde la UI.

### Qué hacer

**a) Servicio `internal/user/service.go`** (crear)
```go
package user

import (
    "context"
    "github.com/google/uuid"
    "github.com/jackc/pgx/v5/pgxpool"
)

type UserModel struct {
    ID        uuid.UUID `json:"id"`
    Email     string    `json:"email"`
    FirstName *string   `json:"first_name"`
    LastName  *string   `json:"last_name"`
    Status    string    `json:"status"`
    CreatedAt time.Time `json:"created_at_utc"`
}

type Service struct {
    pool *pgxpool.Pool
}

func NewService(pool *pgxpool.Pool) *Service

func (s *Service) Search(ctx, query string, limit, offset int) ([]UserModel, int, error)
func (s *Service) List(ctx, limit, offset int) ([]UserModel, int, error)
```

Usar raw pgx queries (no sqlc en este proyecto). Las queries están definidas en `internal/user/queries.sql` como referencia.

**b) Handler `internal/http/handler/user.go`** (crear)
```go
type UserHandler struct {
    svc *user.Service
}

func NewUserHandler(svc *user.Service) *UserHandler
func (h *UserHandler) List(w, r) // GET /api/v1/users/ — listado paginado
func (h *UserHandler) Search(w, r) // GET /api/v1/users/search?q=... — búsqueda
```

**c) Rutas en `internal/http/server.go`**
```go
r.Route("/api/v1/users", func(r chi.Router) {
    r.Use(middleware.PASETOAuth(...))
    r.Use(middleware.RequireAuth())
    r.Use(middleware.RequirePermission(rbacStore, "order:create"))

    r.Get("/", userH.List)
    r.Get("/search", userH.Search)
})
```

Colocar **antes** de la ruta existente de `/api/v1/users/{id}/profile` para evitar conflictos de ruta chi. O mejor, fusionar ambas rutas en un solo bloque con grupos:

```go
r.Route("/api/v1/users", func(r chi.Router) {
    r.Use(middleware.PASETOAuth(...))
    r.Use(middleware.RequireAuth())

    r.Group(func(r chi.Router) {
        r.Use(middleware.RequirePermission(rbacStore, "order:create"))
        r.Get("/", userH.List)
        r.Get("/search", userH.Search)
    })
    r.Group(func(r chi.Router) {
        r.Use(middleware.RequirePermission(rbacStore, "rbac:user:update"))
        r.Put("/{id}/profile", rbacH.AssignProfileToUser)
    })
})
```

**d) Wire en `main/main.go`**: instanciar `user.NewService(pool)`, crear `handler.NewUserHandler(userSvc)`, y agregar `userSvc *user.Service` al parámetro de `httpserver.NewServer()`.

**e) NewServer signature en `internal/http/server.go`**: agregar `userSvc *user.Service` como parámetro. Instanciar `userH := handler.NewUserHandler(userSvc)` junto a los demás handlers.

---

## 2. Frontend: User model y API service

### `web/src/app/core/models/user.model.ts`
```typescript
export interface AppUser {
  id: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  status: string;
  createdAtUtc: string;
}
```

### `web/src/app/core/services/user-api.service.ts`
```typescript
@Injectable({ providedIn: 'root' })
export class UserApiService {
  search(query: string): Observable<AppUser[]>
  list(filter?: { limit?: number; offset?: number }): Observable<PaginatedResponse<AppUser>>
}
```

---

## 3. Frontend: Order Form Dialog

### `web/src/app/pages/admin/orders/order-form-dialog.component.ts`

**Inputs**: `visible`, `branches` (para selector)
**Outputs**: `visibleChange`, `saved`
**Estado interno**:
- `saving` signal
- `submitted` boolean
- `form`: `CreateOrderRequest` (branch_id, client_user_id, notes, items[])
- `selectedUser` signal — para mostrar nombre del usuario seleccionado
- `userSearchResults` signal — resultados de búsqueda
- `userSearchQuery` signal — query string
- `availableProducts` signal — productos cargados para el branch seleccionado
- `userSearchTimeout` — debounce para búsqueda

**Campos del form**:
1. **Branch** (`p-select`): seleccionar sucursal. Auto-filtra por branch si no tiene `view:branch:all`.
   - `(onChange)` → recargar productos de esa sucursal
2. **Cliente** (`p-autocomplete` o `p-select` con filtro): buscar usuario por email/nombre
   - Debounced search llamando a `userApi.search(q)`
   - Mostrar `email - FirstName LastName`
   - Guardar `client_user_id` cuando selecciona
3. **Items** (sub-form): lista de productos agregados
   - Botón "Agregar Producto" abre fila inline o mini dialog
   - Cada item: selector de producto (`p-select` de productos), `p-inputNumber` para cantidad, `p-inputNumber` para precio unitario (auto-poblado del precio del producto), botón eliminar
   - Mostrar subtotal por item y total general
   - Validar quantity > 0
4. **Notas** (`textarea`): opcional

**Validaciones**:
- branch_id requerido
- client_user_id requerido
- al menos 1 item con quantity > 0

**Save**:
1. Build `CreateOrderRequest`:
   ```typescript
   {
     branch_id: this.form.branch_id,
     client_user_id: this.form.client_user_id,
     notes: this.form.notes,
     items: this.form.items.map(i => ({
       item_type: 'product',
       product_id: i.product_id,
       // NO enviar bundle_id — uuid.UUID no acepta string vacío
       quantity: i.quantity,
       unit_price: i.unitPrice,
     }))
   }
   ```
2. Calcular `priceTotal` y `totalItems` en backend (ya implementado)
3. Llamar `orderApi.create(req)`
4. Emitir `saved.emit()` en éxito
5. Mostrar toast de éxito

**Reinicio del form**:
- En `ngOnChanges`, cuando `visible` pasa a true, resetear form con `emptyForm()`
- `emptyForm()`: `{ branch_id: '', client_user_id: '', notes: '', items: [] }`
- Si no tiene `view:branch:all`, auto-setear `branch_id = userBranchId()`

---

## 4. Frontend: Modificar Orders Page

### `web/src/app/pages/admin/orders/orders-page.component.ts`

Agregar:
- `canCreate = computed(() => this.authStore.hasPermission('order:create'))`
- `createDialogVisible = signal(false)`
- `openCreateDialog()` method
- Importar `OrderFormDialogComponent`

### `web/src/app/pages/admin/orders/orders-page.component.html`

Agregar en `.page-header`:
```html
@if (canCreate()) {
  <p-button label="Nueva Orden" icon="pi pi-plus" (onClick)="openCreateDialog()"></p-button>
}
```

Antes del `</section>`:
```html
<app-order-form-dialog
  [visible]="createDialogVisible()"
  [branches]="branches()"
  (visibleChange)="createDialogVisible.set($event)"
  (saved)="onOrderCreated()"
></app-order-form-dialog>
```

También modificar `loadOrders` para incluir `client_user_id` filter si existe.

---

## 5. Tareas de verificación

- Backend:
  - `go build ./...` sin errores
  - `go test ./...` pasa
  - Probar con curl: `GET /api/v1/users/search?q=test`
  - Probar `POST /api/v1/orders/` con datos válidos desde admin

- Frontend:
  - `pnpm build` (o `ng build`) sin errores
  - Probar crear orden desde admin: seleccionar branch, buscar usuario, agregar items, guardar
  - Verificar que la orden aparece en el listado
  - Verificar stock disponible se descuenta correctamente

---

## Referencias

- **Order create backend**: `api/internal/order/service.go` — `Create()` método con stock reservation
- **Order create handler**: `api/internal/order/handler.go` — `Create()` handler
- **Bundle form dialog** (referencia items sub-form): `web/src/app/pages/admin/bundles/bundle-form-dialog.component.ts`
- **User queries existentes**: `api/internal/user/queries.sql` — `ListUsers`, `SearchUsers`
- **Permisos existentes**: `order:create`, `order:view`, `order:update`, `order:delete`, `order:status:change`
