# AGENTS

High-signal guidance for OpenCode sessions in this repo.

## What is actually here

- Monorepo with three real work areas:
  - `api/` — Go backend, PASETO v2.local auth, pgx + PostgreSQL.
  - `web/` — Angular 20 standalone app with signals, PrimeNG 20, SCSS, pnpm.
  - `docs/` — architecture + SDD artifacts that drive feature work.

## Fast commands

- Backend with Docker (recommended when auth/db matters):
  - `cd api && cp .env.example .env && docker compose up --build`
- Backend local:
  - `cd api && go run main/main.go`
- Backend tests:
  - `cd api && go test ./...`
- Backend build:
  - `cd api && go build -o server main/main.go`
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
- All `code` fields (brands, categories, price_categories, measurement_units, etc.) must be UPPERCASE. The only exception is RBAC permission codes (`resource:action`), which are lowercase.
- If a user lacks permission for an action, module, or view, the corresponding button, menu item, or page access must not be rendered in the frontend. Use `computed()` + `authStore.hasPermission()` to gate visibility.

## Backend facts agents often guess wrong

- Auth uses PASETO v2.local, NOT JWT. Do not add JWT libraries or `/.well-known/jwks.json`.
- A valid PASETO token is enough for auth — no DB lookup needed for access token validity.
- Refresh token rotation with family-based theft detection is implemented in `internal/auth/oauth2.go`.
- Password hashing is PBKDF2-SHA256, 100k iterations, compatible with the previous C# hashes.
- The OAuth2 server is custom-built (authorize, token, introspect, revoke). It is NOT using `golang.org/x/oauth2` as a server.
- `golang.org/x/oauth2` is used only as a Google OAuth **client**.
- Package structure: `internal/auth/` (auth + OAuth2), `internal/pricing/` (products, bundles, prices), `internal/branch/`, `internal/category/`, `internal/sync/`, `internal/email/`, `internal/db/`, `internal/rbac/` (RBAC store + CRUD), `internal/order/` (orders, items, status transitions), `internal/staff/`, `internal/admin/`, `internal/user/`.

## Frontend facts agents often guess wrong

- The real frontend root is `web/`, not `api/LCDPC/web/`.
- `web/` is not scaffold-only: it already has standalone components, route wiring, PrimeNG theme setup, and active auth/register flows.
- Package-manager rule: use `pnpm` only for JS/TS work unless the user explicitly approves otherwise.
- Existing route/UI language is English (`/login`, `/register`); extend the current app vocabulary instead of introducing Spanish mid-feature.

## Repo gotchas

- No `.github/` workflows are present; do not invent CI expectations.
- `docs/` contains SDD artifacts; read them before feature work.
- `go_plan_finalized.md` contains the migration analysis (C# vs Go trade-offs).

---

## Backend architectural standards

### Package structure
- Feature-based packages in `internal/` (no Clean Architecture layering).
- Each feature package owns its service, models, and queries.
- `internal/http/handler/` contains thin HTTP handlers — parse input, call service, return JSend.
- `internal/http/response/` contains JSend helpers + `Paginated()` for list endpoints.
- `internal/http/middleware/` contains PASETO auth, RBAC, CORS.

### Data access
- No repository wrappers around sqlc — use `*db.Queries` directly where sqlc is used.
- Most feature packages use raw `*pgxpool.Pool` with inline SQL queries.
- SQL queries are co-located in each feature package (`internal/*/queries.sql`).
- Migrations are in `migrations/` with format `NNNNNN_snake_case.{up,down}.sql`.

### Service layer pattern
- Services take `*pgxpool.Pool` in constructor.
- Structs have JSON tags matching DB column names.
- Request structs have `validate` tags.
- Nullable fields use pointer types (`*string`, `*int`, `*uuid.UUID`).
- Methods return `(*Model, error)` for single items, `([]Model, int, error)` for lists (with total count).
- Filter structs use variadic pattern: `func (s *Service) List(ctx, filter ...Filter) ([]Model, int, error)`.

### Handler pattern
- Thin handlers: parse HTTP input → call service → JSend response.
- Multipart form for file uploads (image field + `file` field).
- `response.Success(w, data)` for 200, `response.Created(w, data)` for 201.
- `response.Error(w, statusCode, message)` for errors.
- `response.Fail(w, statusCode, data)` for validation errors.
- `response.Paginated(w, items, total, limit, offset)` for paginated lists.

### Pagination and filtering
- List endpoints return paginated responses with `items`, `total_count`, `limit`, `offset`.
- Default limit is 10, max limit is 100.
- Filter structs are defined in the feature package (e.g., `pricing.ProductFilter`).
- Filter parsing happens in the handler, not the service.
- Query params: `limit`, `offset`, `category_id`, `name`, `sku`, `code`, `status`, `is_active`.

### Routing
- Uses `chi` router with nested groups.
- Public GET routes at top level.
- Authenticated routes: `PASETOAuth → RequireAuth → RequirePermission(store, "resource:action")`.
- Route structure: `/api/v1/{resource}/` for CRUD.
- Toggle endpoints (e.g., activate/deactivate) use `PATCH /{id}/toggle-action` with a dedicated service method that flips the boolean via `NOT is_active` in SQL. Never send the full entity to toggle a single field.

### RBAC
- Resources are code-based permissions (e.g., `product:create`, `bundle:update`).
- `RequirePermission(rbacStore, "resource:action")` middleware for protected endpoints.
- New resources must be seeded in migrations.

#### Permission matrix

| Resource | create | view | update | delete | Extra |
|---|---|---|---|---|---|
| `product` | ✅ | ✅ | ✅ | ✅ | |
| `bundle` | ✅ | ✅ | ✅ | ✅ | `publish`, `pause` |
| `price` | ✅ | ✅ | ✅ | ✅ | |
| `brand` | ✅ | ✅ | ✅ | ✅ | |
| `category` | ✅ | ✅ | ✅ | ✅ | |
| `price_category` | ✅ | ✅ | ✅ | ✅ | |
| `measurement_unit` | ✅ | ✅ | ✅ | ✅ | |
| `branch` | ✅ | ✅ | — | — | |
| `staff` | ✅ | ✅ | ✅ | ✅ | |
| `order` | ✅ | ✅ | ✅ | ✅ | `status:change` |
| `rbac:resource` | ✅ | ✅ | ✅ | ✅ | |
| `rbac:role` | ✅ | ✅ | ✅ | ✅ | |
| `rbac:profile` | ✅ | ✅ | ✅ | ✅ | |
| `rbac:user` | — | — | ✅ | — | |
| `security-policy` | — | ✅ | ✅ | — | |

#### Route → permission mapping

- **Products**: GET public, POST `product:create`, PUT `product:update`, DELETE `product:delete`
- **Bundles**: GET public, POST `bundle:create`, publish `bundle:publish`, pause `bundle:pause`, PUT `bundle:update`, DELETE `bundle:delete`
- **Prices**: GET public, POST `price:create`, PUT `price:update`, DELETE `price:delete`
- **Brands**: GET list public, GET by id `brand:view`, POST `brand:create`, PUT `brand:update`, DELETE `brand:delete`
- **Categories**: GET public, POST `category:create`, PUT `category:update`, DELETE `category:delete`
- **Price Categories**: GET public, POST `price_category:create`, PUT `price_category:update`, DELETE `price_category:delete`
- **Measurement Units**: GET public, POST `measurement_unit:create`, PUT `measurement_unit:update`, DELETE `measurement_unit:delete`
- **Measurement Unit Classifications**: GET public, POST/PUT/DELETE `measurement_unit:create/update/delete`
- **Conversion Factors**: GET public, POST/PUT/DELETE `measurement_unit:create/update/delete`
- **Branches**: GET public, POST `branch:create`. Branches have schedules in `branch_schedules` table (day_of_week 0-6 where 0=Lunes, start_time, end_time as TIME). Multiple ranges per day allowed. `business_hours` column was removed in migration 000025.
- **Staff**: GET public, POST `staff:create`, PUT `staff:update`, DELETE `staff:delete`
- **Orders**: GET/POST/PUT/DELETE `order:view/create/delete`, status change `order:status:change`
- **RBAC**: all endpoints require matching `rbac:resource/role/profile:view/create/update/delete`
- **Auth**: login/register/public, `/me`+`/refresh`+`/logout` auth-only, `/security-policy` PUT `security-policy:update`
- **Sync**: API Key protected

### Response format
- All responses use JSend: `{"status":"success","data":{}}` or `{"status":"error","message":"..."}`.
- Paginated responses: `{"status":"success","data":{"items":[],"total_count":0,"limit":10,"offset":0}}`.

---

## Frontend architectural standards

### Project structure
- Real frontend root is `web/`, not `api/LCDPC/web/`.
- Standalone components (no NgModules).
- Signals for state management (Angular 20+).
- Package manager: `pnpm` only.

### Directory layout
```
web/src/app/
  core/
    auth/          — auth store, interceptor, init, guards, hasPermission directive
    models/        — TypeScript interfaces matching API structs (13 files)
    services/      — API services (one per domain, 12 files)
    stores/        — domain stores with signals (e.g. CategoryStore)
  pages/           — route-level components
    landing-page/
    search-page/
    auth-page/     — login + AuthApiService (defines API_BASE_URL token)
    register-page/ — multi-step OTP registration flow
    admin/
      admin-layout.component  — collapsible sidebar with permission-gated menu groups
      dashboard/
      products/    — list + form dialog (with prices & conversions sub-forms)
      bundles/     — list + form dialog (with items sub-form)
      orders/      — list + detail dialog + status change dialog
      staff/       — list + form dialog
      classifications/ — measurement unit classifications CRUD
      config/      — section-based config page (?section= query param)
        sections/
          categories-section
          price-categories-section
          measurement-units-section
          rbac-section (profiles, roles, resources with nested assignment dialogs)
  shared/          — reusable UI components (PrimeNG-based)
    header/
    footer/
    hero/
    catalog/
    catalog-search/
    advanced-search/
    branches/
  components/      — legacy plain-HTML versions of hero, catalog, branches (no PrimeNG)
```

### Models (`core/models/`)
- One file per domain: `product.model.ts`, `bundle.model.ts`, `category.model.ts`, etc.
- Interfaces use camelCase (mapped from snake_case API responses).
- Nullable fields typed as `T | null`.
- Pagination model in `pagination.model.ts`: `PaginatedResponse<T>`, `ProductListFilter`, `BundleListFilter`.

### Services (`core/services/`)
- One service per domain: `product-api.service.ts`, `bundle-api.service.ts`, etc.
- Inject `HttpClient` and `API_BASE_URL` (injection token).
- `API_BASE_URL` comes from `environment.apiBaseUrl` via `app.config.ts`.
- All services are `providedIn: 'root'`.
- Private `map()` method converts snake_case GoData to camelCase model.
- List methods accept optional filter, return `Observable<PaginatedResponse<T>>`.
- CRUD methods: `list`, `getById`, `create`, `update`, `delete`.
- Image methods: `updateImage`, `resolveImageUrl`.
- Auth-protected calls use `{ withCredentials: true }`.
- `JsendEnvelope<T>` interface for typing API responses.

### Environment configuration
- `src/environments/environment.ts` for development.
- `src/environments/environment.prod.ts` for production.
- `angular.json` has `fileReplacements` for production builds.
- `apiBaseUrl` is empty string for production (same origin), `http://localhost:8080` for dev.

### Component patterns
- `@Component` with `standalone: true`.
- `inject()` for dependency injection (not constructor injection).
- `signal()` for mutable state, `computed()` for derived state.
- `OnInit` for data loading.
- `forkJoin` for parallel API calls with `error` handler in subscribe.
- Signals passed to templates with `()` invocation: `[prop]="mySignal()"`.
- Image `loading="lazy"` on all non-hero images.
- `(error)="onImageError($event)"` handler sets `/not-found.png` as fallback.

### Routing
- Routes in `app.routes.ts`:
  - `/` — landing page
  - `/search` — catalog search
  - `/login` — auth page
  - `/register` — registration page
  - `/admin` — admin layout (guarded), children:
    - `/admin/dashboard`
    - `/admin/products` (requires `product:view`)
    - `/admin/bundles` (requires `bundle:view`)
    - `/admin/orders` (requires `order:view`)
    - `/admin/staff` (requires `staff:view`)
- English route names, English UI vocabulary.
- Auth routes skip store shell (header/footer).

### Auth
- PASETO v2.local tokens via HTTP-only cookies.
- `authInterceptor` handles 401 → refresh → retry. Skips auth endpoints (`/api/v1/auth/login`, `/refresh`, `/register/*`, `/forgot-password`, `/reset-password`).
- `AuthStore` manages user state with signals: `currentUser`, `permissions`, `isAuthenticated`, `isLoaded`, `expiresAt`.
- `AuthStore` has `hasPermission(code)` and `hasAnyPermission(...codes)` for RBAC checks, `isExpiringSoon()` for refresh timing.
- `withCredentials: true` on all authenticated requests.
- Guards: `adminGuard` (checks specific admin permission list), `permissionGuard(code)` (single permission), `authGuard` (tries session restore via `me()`).
- Directive: `hasPermission` for conditional rendering in templates.
- `API_BASE_URL` injection token is defined in `pages/auth-page/auth-api-go.service.ts`, not in `core/services/`.
- `AuthApiService` handles login, logout, refresh, me, and multi-step registration (start → verify-email → complete).
- `initializeAuth()` factory in `core/auth/auth-init.ts` runs as `APP_INITIALIZER` to restore session on app boot.

### Stores (`core/stores/`)
- Domain stores manage read-only data caches with signals.
- `CategoryStore` — loads categories once, provides `categoryMap`, `categoryOptions`, `filterOptions`, `getCategoryName(id)`.
- Pattern: `signal()` for data, `computed()` for derived state, `load()` with dedup (`loaded` flag).
- Used by both `pages/` and `shared/` components.

### Config page pattern (`pages/admin/config/`)
- Section-based admin page navigated via `?section=` query param.
- Two groups: `rbac` (profiles, roles, resources) and `inventario` (categories, price-categories, measurement-units).
- Permission-gated visibility per group.
- Each section is a standalone component in `config/sections/`.
- Sections that manage entities use a table + form dialog pattern.
- RBAC section manages three entities (resources, roles, profiles) with nested assignment dialogs (assign resource→role, assign role→profile).

### Form dialog pattern
- Reusable dialog components with `@Input() visible`, `@Input() item/entity`, `@Output() saved`, `@Output() closed`.
- Implements `OnChanges` to reset/populate form on visibility change.
- `isEditMode` getter checks if `item` input is set.
- `saving` signal for loading state during save.
- Calls parent `saved.emit()` on success, parent reloads list and shows toast.
- Inline templates for simple dialogs (categories, price-categories, measurement-units, classifications, staff).
- Separate HTML templates for complex dialogs (products, bundles, orders).

#### Form dialog styling standards
- Dialog padding-top: always add `paddingTop: '20px'` to dialog `[style]` so the first floatlabel is visible.
- Floatlabel inputs: every `p-floatlabel` input must have `placeholder=" "` (space) so PrimeNG detects pre-filled values via `ngModel`.
- Vertical gap: `.form-fields` uses `gap: 1.75rem` between fields for comfortable label spacing.

### Admin CRUD page pattern
- Each admin page: list component + form dialog component.
- List component injects: `AuthStore` (permissions), domain API service, `CategoryStore` (if needed), `ConfirmationService`, `MessageService`.
- Permission signals: `canCreate`, `canUpdate`, `canDelete` via `computed()` + `authStore.hasPermission()`.
- Table data loaded via signal, with `loadItems(event)` for pagination.
- Filters: component properties + `applyFilters()` method.
- Delete: `confirmDelete()` using PrimeNG `ConfirmationService`.
- Toast messages via `MessageService` (Spanish: "Exito", "Error").
- All `ConfirmationService` and `MessageService` provided locally in component `providers: []`.

#### Pagination pattern (PrimeNG lazy table)
All paginated list pages must follow the products page pattern — **never** use an external `<p-paginator>` outside `<p-table>`:
- `p-table` must have `[lazy]="true" [paginator]="true" [rows]="pageSize" (onLazyLoad)="loadItems($event)"` — the paginator lives inside the table.
- `pageSize` is a constant (`const pageSize = 10`), not a signal.
- `loadItems(event)` receives `{ first, rows }` from the table's `onLazyLoad` event, computes `offset = event.first`, `limit = event.rows`.
- `totalCount` is a signal populated from `res.totalCount`.
- When filters change, `applyFilters()` calls `loadItems({ first: 0, rows: this.pageSize })` to reset to page 1.
- No `<p-paginator>` is rendered separately; the table handles pagination UI natively.

### Order status flow
- `PENDING_REVIEW` → `APPROVED` | `REJECTED` | `CANCELLED`
- `APPROVED` → `IN_PREPARATION` | `CANCELLED`
- `IN_PREPARATION` → `READY`
- `READY` → `DELIVERED`
- Status transitions managed in `orders-page.component.ts` with a dialog.

### Bundle status flow
- `Draft` → `Published` (via `bundleApi.publish()`)
- `Published` → `Paused` (via `bundleApi.pause()`)
- Status displayed with PrimeNG `p-tag`: Draft=info, Published=success, Paused=warn.

---

## Look and feel standards

### Color palette
- `--page-bg: #e8e8e8` — page background
- `--surface: #fffdf3` — card/surface background
- `--surface-strong: #f5cb00` — primary yellow accent
- `--accent: #f7931a` — orange accent
- `--accent-2: #f5cb00` — secondary yellow
- `--text-strong: #121212` — primary text
- `--text-soft: #2f2f2f` — secondary text
- `--border: #e2a94f` — border color

### Typography
- Display: `Anton` — bold headlines, hero titles
- Body: `Poppins` — all body text, UI elements
- Script: `Pacifico` — decorative accents only
- Font weights: 400 (regular), 500 (medium), 600 (semibold), 700 (bold), 800 (extrabold)

### PrimeNG theme
- Preset: `Aura` (configured in `app.config.ts`)
- Prefix: `p`
- Dark mode: disabled (`darkModeSelector: 'none'`)
- Components used: `p-card`, `p-tag`, `p-button`, `p-select`, `p-inputgroup`, `p-inputtext`, `p-stepper`, `p-floatlabel`, `p-checkbox`, `p-password`, `p-inputotp`, `p-carousel`, `p-datepicker`

### Layout patterns
- Page background: radial gradient with accent colors + linear gradient
- Cards: white surface with `var(--shadow)` shadow
- Category pills: `p-button` with `severity="primary"` (active) or `severity="secondary"` + `[outlined]` (inactive)
- Product grid: CSS grid with responsive columns
- Hero: full-width carousel with overlay text
- Mobile nav: fixed bottom bar with icon buttons

### UI rules
- All buttons must have `cursor: pointer` on hover. PrimeNG buttons may need explicit `cursor: pointer` in `::ng-deep` styles.
- Be precise with layout — align related elements (e.g. action buttons) using sub-grids, not by mixing unrelated elements in the same grid row.
- All dialogs must have `[draggable]="false"` — they should not be draggable.

### Form dialog template (canonical structure)

Use `category-form-dialog.component.ts` as the reference for all simple form dialogs. Key structure:

```typescript
@Component({
  selector: 'app-<entity>-form-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ButtonModule, DialogModule,
    InputTextModule, FloatLabelModule,
  ],
  template: `
    <p-dialog [header]="isEditMode ? 'Editar <Entity>' : 'Nuevo <Entity>'"
              [visible]="visible" (visibleChange)="visibleChange.emit($event)"
              [modal]="true" [dismissableMask]="true" [draggable]="false" [style]="{width: 'min(500px, 95vw)'}"
              (onHide)="close()">
      <div class="form-fields" [style]="{paddingTop: '20px'}">
        <div class="field">
          <p-floatlabel>
            <input pInputText id="name" [(ngModel)]="form.name"
                   [class.ng-invalid]="submitted && !form.name" style="width: 100%" placeholder=" " />
            <label for="name">Nombre *</label>
          </p-floatlabel>
        </div>
      </div>
      <ng-template pTemplate="footer">
        <p-button label="Cancelar" severity="secondary" (onClick)="close()"></p-button>
        <p-button [label]="isEditMode ? 'Guardar Cambios' : 'Crear <Entity>'"
                  icon="pi pi-check" [loading]="saving()" (onClick)="save()"></p-button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`.form-fields { display: flex; flex-direction: column; gap: 1.75rem; } .field { display: flex; flex-direction: column; gap: 0.25rem; }`],
})
```

Rules derived from this template:
- `@Input() visible` + `@Output() visibleChange` for dialog visibility (two-way binding).
- `@Input() entity: Entity | null = null` — null means create mode.
- `@Output() saved` + `@Output() closed` for parent communication.
- `saving` signal, `submitted` boolean, `form` typed as `CreateRequest & { extra? }`.
- `isEditMode` getter checks `entity !== null`.
- `ngOnChanges` resets form when dialog opens (checks `changes['entity'] || changes['visible']`).
- `save()` validates → sets saving → calls create or update → emits `saved`.
- `close()` emits `closed` (parent handles visibility).
- `emptyForm()` private method returns default form values.
- Always use `p-floatlabel` with `placeholder=" "` (space) on inputs.
- Dialog `[style]` must include `paddingTop: '20px'` so the first floatlabel is visible.
- Use `InputTextModule` only (no `InputNumberModule` unless numeric fields are required).

### Image handling
- All product/bundle images: `loading="lazy"` attribute
- Fallback: `/not-found.png` via `(error)="onImageError($event)"`
- API images resolved via `resolveImageUrl(img)` which prepends `apiBaseUrl`
- Static files served from `/static/*` with 7-day cache headers
