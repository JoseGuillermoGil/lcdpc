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

### RBAC
- Resources are code-based permissions (e.g., `product:create`, `bundle:update`).
- `RequirePermission(rbacStore, "resource:action")` middleware for protected endpoints.
- New resources must be seeded in migrations.

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
    auth/          — auth store, interceptor, init, guards
    models/        — TypeScript interfaces matching API structs
    services/      — API services (one per domain)
  pages/           — route-level components
    landing-page/
    search-page/
    auth-page/
    register-page/
    admin/
      dashboard/
      products/
      bundles/
      orders/
      staff/
  shared/          — reusable UI components
    header/
    footer/
    hero/
    catalog/
    catalog-search/
    advanced-search/
    branches/
  components/      — alternative UI components (legacy)
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
- `authInterceptor` handles 401 → refresh → retry.
- `AuthStore` manages user state with signals.
- `withCredentials: true` on all authenticated requests.
- Guards: `adminGuard`, `permissionGuard(code)`.
- Directive: `hasPermission` for conditional rendering.

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
- Components used: `p-card`, `p-tag`, `p-button`, `p-select`, `p-inputgroup`, `p-inputtext`, `p-stepper`, `p-floatlabel`, `p-checkbox`, `p-password`, `p-inputotp`, `p-carousel`

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

### Image handling
- All product/bundle images: `loading="lazy"` attribute
- Fallback: `/not-found.png` via `(error)="onImageError($event)"`
- API images resolved via `resolveImageUrl(img)` which prepends `apiBaseUrl`
- Static files served from `/static/*` with 7-day cache headers
