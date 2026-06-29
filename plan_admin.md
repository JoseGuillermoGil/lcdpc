# Plan: Sección Administrativa

## 1. Objetivo

Implementar una sección administrativa completa con:
- Dashboard con métricas
- CRUD de Productos
- CRUD de Combos (Bundles)
- CRUD de Órdenes

### Reglas de acceso

| Rol | Acceso al admin | Dashboard | Productos | Combos | Órdenes | RBAC |
|-----|----------------|-----------|-----------|--------|---------|------|
| `client` | **NO** — no ve link, no accede | — | — | — | — | — |
| `branch_admin` | SÍ | SÍ | CRUD | CRUD | CRUD | — |
| `global_admin` | SÍ | SÍ | CRUD | CRUD | CRUD | CRUD |

---

## 2. Look and Feel — Admin

La sección admin debe ser consistente con el look and feel del storefront, pero con un tono más funcional y menos comercial.

### 2.1 Paleta de colores admin

Usar las variables CSS existentes con variaciones para el admin:

| Variable | Uso en admin |
|----------|--------------|
| `--page-bg: #e8e8e8` | Fondo del contenido admin |
| `--surface: #fffdf3` | Cards, diálogos, tablas |
| `--surface-strong: #f5cb00` | Sidebar activo link, badges destacados |
| `--accent: #f7931a` | Hover states, alertas |
| `--text-strong: #121212` | Sidebar fondo, textos principales |
| `--text-soft: #2f2f2f` | Textos secundarios |
| `--border: #e2a94f` | Bordes de tabla, separadores |

**Sidebar:** Fondo `--text-strong` (#121212), texto blanco, link activo con borde izquierdo `--surface-strong`.

### 2.2 Tipografía en admin

| Elemento | Font | Weight | Tamaño |
|----------|------|--------|--------|
| Títulos de página (h1) | `Anton` | 400 | 1.8rem |
| Subtítulos / secciones | `Poppins` | 600 | 1.1rem |
| Texto de tabla | `Poppins` | 400 | 0.875rem |
| Badges / tags | `Poppins` | 500 | 0.75rem |
| Sidebar links | `Poppins` | 500 | 0.9rem |
| Sidebar brand | `Anton` | 400 | 1.3rem |

### 2.3 Componentes PrimeNG en admin

| Componente | Uso |
|------------|-----|
| `p-table` | Listados con lazy loading |
| `p-dialog` | Formularios modales |
| `p-tag` | Estados (Draft, Published, Activo, etc.) |
| `p-button` | Acciones, navegación |
| `p-select` | Filtros, selects de formulario |
| `p-inputtext` | Inputs de texto |
| `p-inputnumber` | Campos numéricos |
| `p-floatlabel` | Labels flotantes en formularios |
| `p-confirmdialog` | Confirmación antes de eliminar |
| `p-toast` | Notificaciones de éxito/error |
| `p-card` | Stats del dashboard |
| `p-toolbar` | Barra de acciones sobre tablas |

---

## 3. Arquitectura Frontend

### 3.1 Estructura de directorios

```
web/src/app/
├── pages/
│   ├── admin/
│   │   ├── admin-layout.component.ts        # Shell con sidebar
│   │   ├── admin-layout.component.html
│   │   ├── admin-layout.component.scss
│   │   ├── dashboard/
│   │   │   ├── dashboard-page.component.ts
│   │   │   ├── dashboard-page.component.html
│   │   │   └── dashboard-page.component.scss
│   │   ├── products/
│   │   │   ├── products-page.component.ts
│   │   │   ├── products-page.component.html
│   │   │   ├── products-page.component.scss
│   │   │   ├── product-form-dialog.component.ts
│   │   │   └── product-form-dialog.component.html
│   │   ├── bundles/
│   │   │   ├── bundles-page.component.ts
│   │   │   ├── bundles-page.component.html
│   │   │   ├── bundles-page.component.scss
│   │   │   ├── bundle-form-dialog.component.ts
│   │   │   └── bundle-form-dialog.component.html
│   │   └── orders/
│   │       ├── orders-page.component.ts
│   │       ├── orders-page.component.html
│   │       ├── orders-page.component.scss
│   │       ├── order-detail-dialog.component.ts
│   │       └── order-detail-dialog.component.html
│   ├── landing-page/
│   ├── search-page/
│   ├── auth-page/
│   └── register-page/
├── shared/
│   └── ... (existentes)
├── core/
│   ├── auth/
│   │   ├── admin.guard.ts                   # NUEVO
│   │   └── permission.guard.ts              # NUEVO
│   ├── models/
│   └── services/
└── app.routes.ts
```

### 3.2 Rutas

```typescript
// app.routes.ts
export const routes: Routes = [
  { path: '', component: LandingPageComponent },
  { path: 'search', component: SearchPageComponent },
  { path: 'login', component: AuthPageComponent },
  { path: 'register', component: RegisterPageComponent },
  {
    path: 'admin',
    component: AdminLayoutComponent,
    canActivate: [adminGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: DashboardPageComponent },
      { path: 'products', component: ProductsPageComponent, canActivate: [permissionGuard('product:view')] },
      { path: 'bundles', component: BundlesPageComponent, canActivate: [permissionGuard('bundle:view')] },
      { path: 'orders', component: OrdersPageComponent, canActivate: [permissionGuard('order:view')] },
    ]
  }
];
```

**Nota sobre rutas:** Las rutas usan nombres en inglés según el estándar del proyecto (`/admin/products`, no `/admin/productos`).

### 3.3 Guards

#### `adminGuard`

Archivo: `web/src/app/core/auth/admin.guard.ts`

```typescript
export const adminGuard: CanActivateFn = () => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (!authStore.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }

  // Client NO puede acceder al admin — solo tiene permisos de vista
  const adminPermissions = [
    'product:create', 'product:update', 'product:delete',
    'bundle:create', 'bundle:update', 'bundle:delete',
    'order:view', 'order:create', 'order:update', 'order:delete',
    'rbac:resource:view', 'rbac:role:view', 'rbac:profile:view',
  ];

  if (authStore.hasAnyPermission(...adminPermissions)) {
    return true;
  }

  return router.createUrlTree(['/']);
};
```

#### `permissionGuard(permission: string)`

Archivo: `web/src/app/core/auth/permission.guard.ts`

```typescript
export function permissionGuard(permission: string): CanActivateFn {
  return () => {
    const authStore = inject(AuthStore);
    if (authStore.hasPermission(permission)) return true;
    return inject(Router).createUrlTree(['/admin']);
  };
}
```

---

## 4. Admin Layout (Shell con Sidebar)

### 4.1 Componente: AdminLayoutComponent

**Template:**
```html
<div class="admin-shell">
  <aside class="admin-sidebar">
    <div class="sidebar-header">
      <a routerLink="/admin/dashboard" class="sidebar-brand">LCDPC Admin</a>
    </div>

    <nav class="sidebar-nav">
      <a routerLink="/admin/dashboard" routerLinkActive="active" [routerLinkActiveOptions]="{exact: true}">
        <span class="pi pi-chart-bar"></span>
        <span>Dashboard</span>
      </a>

      @if (canViewProducts()) {
        <a routerLink="/admin/products" routerLinkActive="active">
          <span class="pi pi-box"></span>
          <span>Productos</span>
        </a>
      }

      @if (canViewBundles()) {
        <a routerLink="/admin/bundles" routerLinkActive="active">
          <span class="pi pi-gift"></span>
          <span>Combos</span>
        </a>
      }

      @if (canViewOrders()) {
        <a routerLink="/admin/orders" routerLinkActive="active">
          <span class="pi pi-shopping-cart"></span>
          <span>Órdenes</span>
        </a>
      }
    </nav>

    <div class="sidebar-footer">
      <a routerLink="/" class="back-to-store">
        <span class="pi pi-arrow-left"></span>
        <span>Volver a la tienda</span>
      </a>
      <div class="sidebar-user">
        <span class="pi pi-user"></span>
        <span>{{ userName() }}</span>
      </div>
    </div>
  </aside>

  <main class="admin-content">
    <router-outlet></router-outlet>
  </main>
</div>
```

**Lógica:**
```typescript
@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.scss'
})
export class AdminLayoutComponent {
  private readonly authStore = inject(AuthStore);

  protected readonly userName = computed(() => this.authStore.currentUser()?.displayName ?? 'Admin');

  protected readonly canViewProducts = computed(() =>
    this.authStore.hasPermission('product:view')
  );
  protected readonly canViewBundles = computed(() =>
    this.authStore.hasPermission('bundle:view')
  );
  protected readonly canViewOrders = computed(() =>
    this.authStore.hasPermission('order:view')
  );
}
```

### 4.2 SCSS del Admin Layout

```scss
// admin-layout.component.scss

.admin-shell {
  display: flex;
  min-height: 100vh;
}

// ── Sidebar ──────────────────────────────────────

.admin-sidebar {
  width: 260px;
  min-height: 100vh;
  background: var(--text-strong);
  color: #fff;
  display: flex;
  flex-direction: column;
  position: fixed;
  top: 0;
  left: 0;
  z-index: 100;
}

.sidebar-header {
  padding: 1.5rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.sidebar-brand {
  font-family: var(--font-display);
  font-size: 1.3rem;
  color: var(--surface-strong);
  text-decoration: none;
  letter-spacing: 0.5px;
}

.sidebar-nav {
  flex: 1;
  padding: 1rem 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;

  a {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 0.75rem 1.5rem;
    color: rgba(255, 255, 255, 0.7);
    text-decoration: none;
    font-family: var(--font-body);
    font-weight: 500;
    font-size: 0.9rem;
    transition: all 0.15s ease;
    border-left: 3px solid transparent;

    .pi {
      font-size: 1rem;
      width: 1.25rem;
      text-align: center;
    }

    &:hover {
      color: #fff;
      background: rgba(255, 255, 255, 0.05);
    }

    &.active {
      color: var(--surface-strong);
      background: rgba(245, 203, 0, 0.08);
      border-left-color: var(--surface-strong);
    }
  }
}

.sidebar-footer {
  padding: 1rem 1.5rem;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  display: flex;
  flex-direction: column;
  gap: 0.75rem;

  .back-to-store {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    color: rgba(255, 255, 255, 0.5);
    text-decoration: none;
    font-size: 0.8rem;
    transition: color 0.15s;

    &:hover {
      color: var(--surface-strong);
    }
  }

  .sidebar-user {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    color: rgba(255, 255, 255, 0.7);
    font-size: 0.85rem;

    .pi {
      color: var(--surface-strong);
    }
  }
}

// ── Content ──────────────────────────────────────

.admin-content {
  flex: 1;
  margin-left: 260px;
  padding: 2rem;
  background: var(--page-bg);
  min-height: 100vh;
}

// ── Responsive ───────────────────────────────────

@media (max-width: 768px) {
  .admin-sidebar {
    width: 60px;

    .sidebar-brand,
    .sidebar-nav a span:last-child,
    .sidebar-footer .back-to-store span:last-child,
    .sidebar-footer .sidebar-user span:last-child {
      display: none;
    }

    .sidebar-nav a {
      justify-content: center;
      padding: 0.75rem;
    }
  }

  .admin-content {
    margin-left: 60px;
    padding: 1rem;
  }
}
```

---

## 5. Header — Botón "Administrar"

### 5.1 Cambio en header.component.html

Agregar entre el botón de búsqueda y el branch picker (línea 21 del header actual):

```html
@if (isAdmin()) {
  <p-button
    label="Administrar"
    icon="pi pi-cog"
    styleClass="admin-nav-button"
    [text]="true"
    severity="secondary"
    routerLink="/admin"
  ></p-button>
}
```

### 5.2 Cambio en header.component.ts

Agregar computed para visibilidad:

```typescript
protected readonly isAdmin = computed(() =>
  this.authStore.hasAnyPermission(
    'product:create', 'product:update', 'product:delete',
    'bundle:create', 'bundle:update', 'bundle:delete',
    'order:view', 'order:create', 'order:update', 'order:delete',
    'rbac:resource:view'
  )
);
```

**Lógica de visibilidad:**
- `client` → tiene solo `product:view`, `bundle:view`, `price:view`, `branch:view` → **NO ve el botón**
- `branch_admin` → tiene `product:create`, `bundle:create`, `order:view`, etc. → **VE el botón**
- `global_admin` → tiene todos los permisos → **VE el botón**

---

## 6. Dashboard

### 6.1 Métricas

| Card | Dato | Fuente | Icono |
|------|------|--------|-------|
| Total Productos | `totalCount` | `productApi.list({limit:1})` | `pi-box` |
| Total Combos | `totalCount` | `bundleApi.list({limit:1})` | `pi-gift` |
| Total Órdenes | `totalCount` | `orderApi.list({limit:1})` | `pi-shopping-cart` |
| Pendientes | `totalCount` con filtro | `orderApi.list({status:'PENDING_REVIEW',limit:1})` | `pi-clock` |

### 6.2 Template

```html
<section class="dashboard-page">
  <div class="page-header">
    <h1>Dashboard</h1>
  </div>

  <div class="stats-grid">
    <div class="stat-card">
      <div class="stat-icon"><span class="pi pi-box"></span></div>
      <div class="stat-body">
        <div class="stat-value">{{ stats().totalProducts }}</div>
        <div class="stat-label">Productos</div>
      </div>
    </div>

    <div class="stat-card">
      <div class="stat-icon"><span class="pi pi-gift"></span></div>
      <div class="stat-body">
        <div class="stat-value">{{ stats().totalBundles }}</div>
        <div class="stat-label">Combos</div>
      </div>
    </div>

    <div class="stat-card">
      <div class="stat-icon"><span class="pi pi-shopping-cart"></span></div>
      <div class="stat-body">
        <div class="stat-value">{{ stats().totalOrders }}</div>
        <div class="stat-label">Órdenes Totales</div>
      </div>
    </div>

    <div class="stat-card pending">
      <div class="stat-icon"><span class="pi pi-clock"></span></div>
      <div class="stat-body">
        <div class="stat-value">{{ stats().pendingOrders }}</div>
        <div class="stat-label">Pendientes</div>
      </div>
    </div>
  </div>
</section>
```

### 6.3 SCSS del Dashboard

```scss
.dashboard-page {
  .page-header {
    margin-bottom: 2rem;

    h1 {
      font-family: var(--font-display);
      font-size: 1.8rem;
      color: var(--text-strong);
      margin: 0;
    }
  }
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1.5rem;
}

.stat-card {
  background: var(--surface);
  border-radius: 12px;
  padding: 1.5rem;
  display: flex;
  align-items: center;
  gap: 1rem;
  box-shadow: var(--shadow);
  transition: transform 0.15s ease;

  &:hover {
    transform: translateY(-2px);
  }

  .stat-icon {
    width: 48px;
    height: 48px;
    border-radius: 10px;
    background: rgba(245, 203, 0, 0.12);
    display: flex;
    align-items: center;
    justify-content: center;

    .pi {
      font-size: 1.25rem;
      color: var(--accent);
    }
  }

  .stat-value {
    font-family: var(--font-display);
    font-size: 2rem;
    color: var(--text-strong);
    line-height: 1;
  }

  .stat-label {
    font-family: var(--font-body);
    font-size: 0.85rem;
    color: var(--text-soft);
    margin-top: 0.25rem;
  }

  &.pending {
    border-left: 4px solid var(--accent);

    .stat-icon {
      background: rgba(247, 147, 26, 0.12);

      .pi {
        color: var(--accent);
      }
    }
  }
}
```

---

## 7. CRUD de Productos

### 7.1 Endpoints

| Acción | Endpoint | Auth | Permiso |
|--------|----------|------|---------|
| Listar | `GET /api/v1/products/?limit=&offset=&category_id=&name=&sku=&is_active=` | No | — |
| Obtener | `GET /api/v1/products/{id}` | No | — |
| Crear | `POST /api/v1/products/` | SÍ | `product:create` |
| Actualizar | `PUT /api/v1/products/{id}` | SÍ | `product:update` |
| Eliminar | `DELETE /api/v1/products/{id}` | SÍ | `product:delete` |
| Subir imagen | `PUT /api/v1/products/{id}/image` | SÍ | `product:update` |

### 7.2 ProductsPageComponent

**Tabla PrimeNG** con:
- Columnas: Imagen, Nombre, SKU, Categoría, Tipo Medida, Activo, Acciones
- Paginación server-side (lazy loading)
- Filtros: nombre (InputText), categoría (Select), is_active (Select con opciones Activo/Inactivo)
- Toolbar con botón "Nuevo Producto" y filtros
- Acciones por fila: Editar, Eliminar (con confirmación)

**Template:**
```html
<section class="products-page">
  <div class="page-header">
    <h1>Productos</h1>
    @if (canCreate()) {
      <p-button label="Nuevo Producto" icon="pi pi-plus" (onClick)="openCreateDialog()"></p-button>
    }
  </div>

  <p-toolbar styleClass="admin-toolbar">
    <div class="p-toolbar-group-start">
      <span class="p-input-icon-left">
        <i class="pi pi-search"></i>
        <input pInputText type="text" placeholder="Buscar por nombre..." [(ngModel)]="filterName" (keyup.enter)="applyFilters()" />
      </span>
    </div>
    <div class="p-toolbar-group-end">
      <p-select [options]="categoryOptions()" [(ngModel)]="filterCategoryId" placeholder="Todas las categorías" [showClear]="true" (onChange)="applyFilters()"></p-select>
      <p-select [options]="activeOptions" [(ngModel)]="filterIsActive" placeholder="Todos" [showClear]="true" (onChange)="applyFilters()"></p-select>
    </div>
  </p-toolbar>

  <p-table [value]="products()" [lazy]="true" [paginator]="true"
           [rows]="pageSize" [totalRecords]="totalCount()" [loading]="loading()"
           (onLazyLoad)="loadProducts($event)" styleClass="admin-table">
    <ng-template pTemplate="header">
      <tr>
        <th style="width: 60px">Imagen</th>
        <th>Nombre</th>
        <th>SKU</th>
        <th>Categoría</th>
        <th>Medida</th>
        <th style="width: 80px">Activo</th>
        <th style="width: 120px">Acciones</th>
      </tr>
    </ng-template>
    <ng-template pTemplate="body" let-product>
      <tr>
        <td>
          <img [src]="resolveImage(product.img)" class="product-thumb"
               loading="lazy" (error)="onImageError($event)" />
        </td>
        <td>{{ product.name }}</td>
        <td><code>{{ product.sku }}</code></td>
        <td>{{ getCategoryName(product.categoryId) }}</td>
        <td>{{ product.baseMeasureType }}</td>
        <td>
          <p-tag [value]="product.isActive ? 'Activo' : 'Inactivo'"
                 [severity]="product.isActive ? 'success' : 'danger'" />
        </td>
        <td>
          @if (canUpdate()) {
            <p-button icon="pi pi-pencil" [text]="true" [rounded]="true"
                      (onClick)="openEditDialog(product)" pTooltip="Editar"></p-button>
          }
          @if (canDelete()) {
            <p-button icon="pi pi-trash" [text]="true" [rounded]="true" severity="danger"
                      (onClick)="confirmDelete(product)" pTooltip="Eliminar"></p-button>
          }
        </td>
      </tr>
    </ng-template>
    <ng-template pTemplate="emptymessage">
      <tr>
        <td colspan="7" class="empty-state">
          <span class="pi pi-box" style="font-size: 2rem; color: var(--text-soft)"></span>
          <p>No hay productos registrados</p>
        </td>
      </tr>
    </ng-template>
  </p-table>
</section>

<p-confirmDialog />
<p-toast />
```

### 7.3 ProductFormDialogComponent

**Diálogo** con formulario para crear/editar productos.

**Campos:**
| Campo | Componente | Requerido | Notas |
|-------|-----------|-----------|-------|
| `name` | `p-inputtext` + `p-floatlabel` | SÍ | |
| `sku` | `p-inputtext` + `p-floatlabel` | SÍ | |
| `base_measure_type` | `p-select` | SÍ | Opciones: "Unidad", "Peso", "Volumen" |
| `wholesale_commercial_type` | `p-inputtext` + `p-floatlabel` | SÍ | |
| `units_per_box` | `p-inputnumber` + `p-floatlabel` | Condicional | Visible si `base_measure_type` = "Unidad" |
| `units_per_bundle` | `p-inputnumber` + `p-floatlabel` | Condicional | Visible si `base_measure_type` = "Unidad" |
| `category_id` | `p-select` | No | Carga categorías desde API |
| `is_active` | `p-checkbox` | — | Default: true |
| `img` | File input + preview | No | Preview de imagen actual o nueva |

**Template:**
```html
<p-dialog [header]="isEditMode ? 'Editar Producto' : 'Nuevo Producto'"
          [visible]="visible" (visibleChange)="close.emit($event)"
          [modal]="true" [dismissableMask]="true" [style]="{width: '550px'}">
  <div class="product-form">
    <div class="field">
      <p-floatlabel>
        <input pInputText id="name" [(ngModel)]="form.name" [class.ng-invalid]="submitted && !form.name" />
        <label for="name">Nombre *</label>
      </p-floatlabel>
    </div>

    <div class="field">
      <p-floatlabel>
        <input pInputText id="sku" [(ngModel)]="form.sku" [class.ng-invalid]="submitted && !form.sku" />
        <label for="sku">SKU *</label>
      </p-floatlabel>
    </div>

    <div class="field-row">
      <div class="field">
        <p-floatlabel>
          <p-select id="measureType" [options]="measureTypeOptions" [(ngModel)]="form.base_measure_type" [style]="{'width':'100%'}" />
          <label for="measureType">Tipo de Medida *</label>
        </p-floatlabel>
      </div>
      <div class="field">
        <p-floatlabel>
          <input pInputText id="commercialType" [(ngModel)]="form.wholesale_commercial_type" />
          <label for="commercialType">Tipo Comercial *</label>
        </p-floatlabel>
      </div>
    </div>

    @if (form.base_measure_type === 'Unidad') {
      <div class="field-row">
        <div class="field">
          <p-floatlabel>
            <p-inputNumber id="unitsPerBox" [(ngModel)]="form.units_per_box" [min]="1" [style]="{'width':'100%'}" />
            <label for="unitsPerBox">Unidades por Caja</label>
          </p-floatlabel>
        </div>
        <div class="field">
          <p-floatlabel>
            <p-inputNumber id="unitsPerBundle" [(ngModel)]="form.units_per_bundle" [min]="1" [style]="{'width':'100%'}" />
            <label for="unitsPerBundle">Unidades por Bulto</label>
          </p-floatlabel>
        </div>
      </div>
    }

    <div class="field">
      <p-floatlabel>
        <p-select id="category" [options]="categoryOptions()" [(ngModel)]="form.category_id"
                  optionLabel="name" optionValue="categoryId" placeholder="Sin categoría"
                  [showClear]="true" [style]="{'width':'100%'}" />
        <label for="category">Categoría</label>
      </p-floatlabel>
    </div>

    <div class="field">
      <p-checkbox [(ngModel)]="form.is_active" label="Producto activo" [binary]="true"></p-checkbox>
    </div>

    <div class="field">
      <label class="field-label">Imagen</label>
      @if (imagePreview) {
        <div class="image-preview">
          <img [src]="imagePreview" alt="Preview" />
          <p-button icon="pi pi-times" [text]="true" severity="danger" (onClick)="removeImage()"></p-button>
        </div>
      }
      <input type="file" accept="image/*" (change)="onFileSelect($event)" />
    </div>
  </div>

  <ng-template pTemplate="footer">
    <p-button label="Cancelar" severity="secondary" [text]="true" (onClick)="close.emit(false)"></p-button>
    <p-button [label]="isEditMode ? 'Guardar Cambios' : 'Crear Producto'"
              icon="pi pi-check" [loading]="saving" (onClick)="save()"></p-button>
  </ng-template>
</p-dialog>
```

### 7.4 Modificar ProductApiService para multipart

```typescript
create(req: CreateProductRequest, file?: File): Observable<Product> {
  if (file) {
    const formData = new FormData();
    formData.append('data', JSON.stringify(req));
    formData.append('file', file);
    return this.http
      .post<JsendEnvelope<ProductGoData>>(`${this.baseUrl}/api/v1/products/`, formData, {
        withCredentials: true,
      })
      .pipe(map((res) => this.map(res.data)));
  }
  return this.http
    .post<JsendEnvelope<ProductGoData>>(`${this.baseUrl}/api/v1/products/`, req, {
      withCredentials: true,
    })
    .pipe(map((res) => this.map(res.data)));
}

update(id: string, req: CreateProductRequest, file?: File): Observable<Product> {
  if (file) {
    const formData = new FormData();
    formData.append('data', JSON.stringify(req));
    formData.append('file', file);
    return this.http
      .put<JsendEnvelope<ProductGoData>>(`${this.baseUrl}/api/v1/products/${id}`, formData, {
        withCredentials: true,
      })
      .pipe(map((res) => this.map(res.data)));
  }
  return this.http
    .put<JsendEnvelope<ProductGoData>>(`${this.baseUrl}/api/v1/products/${id}`, req, {
      withCredentials: true,
    })
    .pipe(map((res) => this.map(res.data)));
}
```

---

## 8. CRUD de Combos (Bundles)

### 8.1 Endpoints

| Acción | Endpoint | Auth | Permiso |
|--------|----------|------|---------|
| Listar | `GET /api/v1/bundles/?limit=&offset=&category_id=&name=&code=&status=` | No | — |
| Obtener | `GET /api/v1/bundles/{id}` | No | — |
| Crear | `POST /api/v1/bundles/` | SÍ | `bundle:create` |
| Actualizar | `PUT /api/v1/bundles/{id}` | SÍ | `bundle:update` |
| Eliminar | `DELETE /api/v1/bundles/{id}` | SÍ | `bundle:delete` |
| Publicar | `POST /api/v1/bundles/{id}/publish` | SÍ | `bundle:create` |
| Pausar | `POST /api/v1/bundles/{id}/pause` | SÍ | `bundle:create` |
| Subir imagen | `PUT /api/v1/bundles/{id}/image` | SÍ | `bundle:update` |

### 8.2 BundlesPageComponent

Similar a ProductsPage pero con columnas y acciones específicas para bundles.

**Columnas:** Imagen, Nombre, Código, Estado, Precio, Categoría, Acciones

**Acciones por fila:**
- Editar (si tiene `bundle:update`)
- Publicar (si status es Draft o Paused, y tiene `bundle:create`)
- Pausar (si status es Published, y tiene `bundle:create`)
- Eliminar (si tiene `bundle:delete`)

**Filtros:** nombre, código, status (Draft/Published/Paused), category_id

### 8.3 BundleFormDialogComponent

**Campos:**
| Campo | Componente | Requerido |
|-------|-----------|-----------|
| `name` | `p-inputtext` | SÍ |
| `code` | `p-inputtext` | SÍ |
| `category_id` | `p-select` | No |
| `items` | Lista dinámica | SÍ (mínimo 1) |
| `img` | File input | No |

**Items del combo** — cada item tiene:
- `product_id`: Select de productos (carga desde API)
- `quantity`: InputNumber (mínimo 1)
- Botón para quitar item

Al final: botón "Agregar Item" para agregar más líneas.

### 8.4 Modificar BundleApiService para multipart

Mismo patrón que ProductApiService — `create()` y `update()` aceptan `file?: File` y envían multipart si hay archivo.

---

## 9. CRUD de Órdenes

### 9.1 Endpoints

| Acción | Endpoint | Auth | Permiso |
|--------|----------|------|---------|
| Listar | `GET /api/v1/orders/?limit=&offset=&branch_id=&client_user_id=&status=` | SÍ | `order:view` |
| Obtener | `GET /api/v1/orders/{id}` | SÍ | `order:view` |
| Historial | `GET /api/v1/orders/{id}/history` | SÍ | `order:view` |
| Crear | `POST /api/v1/orders/` | SÍ | `order:create` |
| Actualizar | `PUT /api/v1/orders/{id}` | SÍ | `order:update` |
| Eliminar | `DELETE /api/v1/orders/{id}` | SÍ | `order:delete` |
| Cambiar estado | `POST /api/v1/orders/{id}/status` | SÍ | `order:status:change` |

### 9.2 OrdersPageComponent

**Columnas:** ID (corto), Sucursal, Estado, Total, Items, Fecha, Acciones

**Filtros:** status (Select), branch_id (Select de sucursales)

**Acciones por fila:**
- Ver detalle (abre OrderDetailDialog)
- Cambiar estado (abre diálogo de transición)
- Eliminar (con confirmación)

### 9.3 Estados y transiciones

| Estado actual | Siguiente(s) permitido(s) | Severidad PrimeNG |
|---------------|--------------------------|-------------------|
| `PENDING_REVIEW` | `APPROVED`, `REJECTED`, `CANCELLED` | `warn` |
| `APPROVED` | `IN_PREPARATION`, `CANCELLED` | `info` |
| `IN_PREPARATION` | `READY` | `info` |
| `READY` | `DELIVERED` | `success` |
| `DELIVERED` | — (final) | `success` |
| `REJECTED` | — (final) | `danger` |
| `CANCELLED` | — (final) | `danger` |

### 9.4 OrderDetailDialogComponent

**Secciones del diálogo:**
1. **Header:** ID de orden, estado actual (tag), fecha de creación
2. **Info general:** Sucursal, cliente, total, cantidad de items
3. **Items:** Tabla con product/bundle name, quantity, unit_price, subtotal
4. **Historial:** Timeline de cambios de estado con fecha, usuario, notas

---

## 10. Migración backend: Permisos de orden

Los permisos de orden (`order:view`, `order:create`, etc.) se seedearon en la migración 000003 pero NO se asignaron a ningún rol. Se necesita una nueva migración.

### 000007_assign_order_permissions.up.sql

```sql
-- global_admin gets all order permissions
INSERT INTO role_resources (role_id, resource_id)
SELECT '33333333-3333-3333-3333-333333333333', id FROM resources
WHERE code IN ('order:view', 'order:create', 'order:update', 'order:delete', 'order:status:change')
ON CONFLICT (role_id, resource_id) DO NOTHING;

-- branch_admin gets all order permissions
INSERT INTO role_resources (role_id, resource_id)
SELECT '22222222-2222-2222-2222-222222222222', id FROM resources
WHERE code IN ('order:view', 'order:create', 'order:update', 'order:delete', 'order:status:change')
ON CONFLICT (role_id, resource_id) DO NOTHING;

-- client gets order:view only
INSERT INTO role_resources (role_id, resource_id)
SELECT '11111111-1111-1111-1111-111111111111', id FROM resources
WHERE code IN ('order:view')
ON CONFLICT (role_id, resource_id) DO NOTHING;
```

### 000007_assign_order_permissions.down.sql

```sql
DELETE FROM role_resources WHERE resource_id IN (
    SELECT id FROM resources WHERE code IN ('order:view', 'order:create', 'order:update', 'order:delete', 'order:status:change')
);
```

---

## 11. SCSS compartido para admin

### admin-shared.scss (importar en cada componente admin)

```scss
// ── Page header ──────────────────────────────────

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1.5rem;

  h1 {
    font-family: var(--font-display);
    font-size: 1.8rem;
    color: var(--text-strong);
    margin: 0;
  }
}

// ── Toolbar ──────────────────────────────────────

.admin-toolbar {
  margin-bottom: 1rem;
  background: var(--surface);
  border-radius: 8px;
  box-shadow: var(--shadow);

  .p-toolbar-group-end {
    display: flex;
    gap: 0.5rem;
  }
}

// ── Table ────────────────────────────────────────

.admin-table {
  background: var(--surface);
  border-radius: 8px;
  overflow: hidden;
  box-shadow: var(--shadow);

  th {
    font-family: var(--font-body);
    font-weight: 600;
    font-size: 0.8rem;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    color: var(--text-soft);
    background: rgba(0, 0, 0, 0.02);
  }

  td {
    font-family: var(--font-body);
    font-size: 0.875rem;
    vertical-align: middle;
  }

  code {
    font-size: 0.8rem;
    background: rgba(0, 0, 0, 0.04);
    padding: 0.15rem 0.4rem;
    border-radius: 4px;
  }
}

.product-thumb,
.bundle-thumb {
  width: 40px;
  height: 40px;
  object-fit: cover;
  border-radius: 6px;
}

.empty-state {
  text-align: center;
  padding: 2rem !important;

  p {
    color: var(--text-soft);
    margin-top: 0.5rem;
  }
}

// ── Form ─────────────────────────────────────────

.product-form,
.bundle-form {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.field-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.field-label {
  font-family: var(--font-body);
  font-weight: 500;
  font-size: 0.85rem;
  color: var(--text-soft);
}

.image-preview {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.5rem;

  img {
    width: 80px;
    height: 80px;
    object-fit: cover;
    border-radius: 8px;
    border: 2px solid var(--border);
  }
}

// ── Item row (bundle form) ───────────────────────

.item-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.5rem;

  p-select {
    flex: 1;
  }

  p-inputnumber {
    width: 100px;
  }
}

// ── Order status timeline ────────────────────────

.status-timeline {
  list-style: none;
  padding: 0;
  margin: 0;

  li {
    display: flex;
    align-items: flex-start;
    gap: 0.75rem;
    padding: 0.75rem 0;
    border-left: 2px solid var(--border);
    margin-left: 0.5rem;
    padding-left: 1.5rem;
    position: relative;

    &::before {
      content: '';
      position: absolute;
      left: -5px;
      top: 1rem;
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--surface-strong);
    }

    .timeline-date {
      font-size: 0.75rem;
      color: var(--text-soft);
      min-width: 80px;
    }

    .timeline-content {
      font-size: 0.85rem;
    }
  }
}
```

---

## 12. Archivos a crear/modificar

### 12.1 Archivos nuevos (23 archivos)

| # | Archivo | Descripción |
|---|---------|-------------|
| 1 | `core/auth/admin.guard.ts` | Guard que bloquea clients |
| 2 | `core/auth/permission.guard.ts` | Guard parametrizado |
| 3 | `pages/admin/admin-layout.component.ts` | Shell con sidebar |
| 4 | `pages/admin/admin-layout.component.html` | Template sidebar |
| 5 | `pages/admin/admin-layout.component.scss` | Estilos sidebar |
| 6 | `pages/admin/dashboard/dashboard-page.component.ts` | Dashboard |
| 7 | `pages/admin/dashboard/dashboard-page.component.html` | Template dashboard |
| 8 | `pages/admin/dashboard/dashboard-page.component.scss` | Estilos dashboard |
| 9 | `pages/admin/products/products-page.component.ts` | Lista productos |
| 10 | `pages/admin/products/products-page.component.html` | Template lista |
| 11 | `pages/admin/products/products-page.component.scss` | Estilos lista |
| 12 | `pages/admin/products/product-form-dialog.component.ts` | Formulario |
| 13 | `pages/admin/products/product-form-dialog.component.html` | Template form |
| 14 | `pages/admin/bundles/bundles-page.component.ts` | Lista combos |
| 15 | `pages/admin/bundles/bundles-page.component.html` | Template lista |
| 16 | `pages/admin/bundles/bundles-page.component.scss` | Estilos lista |
| 17 | `pages/admin/bundles/bundle-form-dialog.component.ts` | Formulario |
| 18 | `pages/admin/bundles/bundle-form-dialog.component.html` | Template form |
| 19 | `pages/admin/orders/orders-page.component.ts` | Lista órdenes |
| 20 | `pages/admin/orders/orders-page.component.html` | Template lista |
| 21 | `pages/admin/orders/orders-page.component.scss` | Estilos lista |
| 22 | `pages/admin/orders/order-detail-dialog.component.ts` | Detalle |
| 23 | `pages/admin/orders/order-detail-dialog.component.html` | Template detalle |

### 12.2 Archivos modificados (5 archivos)

| # | Archivo | Cambio |
|---|---------|--------|
| 1 | `app.routes.ts` | Agregar rutas `/admin` con children y guards |
| 2 | `shared/header/header.component.ts` | Agregar `isAdmin` computed |
| 3 | `shared/header/header.component.html` | Agregar botón "Administrar" |
| 4 | `core/services/product-api.service.ts` | `create()` y `update()` soportan multipart |
| 5 | `core/services/bundle-api.service.ts` | `create()` y `update()` soportan multipart |

### 12.3 Migración backend (1 archivo)

| # | Archivo | Descripción |
|---|---------|-------------|
| 1 | `api/migrations/000007_assign_order_permissions.up.sql` | Asigna permisos de orden a roles |

---

## 13. Orden de implementación

### Fase 1: Infraestructura admin
1. Crear `admin.guard.ts` y `permission.guard.ts`
2. Crear `AdminLayoutComponent` (ts + html + scss)
3. Actualizar `app.routes.ts`
4. Agregar botón "Administrar" en header
5. Crear migración 000007

### Fase 2: Dashboard
6. Crear `DashboardPageComponent` (ts + html + scss)

### Fase 3: CRUD Productos
7. Modificar `product-api.service.ts` para multipart
8. Crear `ProductsPageComponent` (ts + html + scss)
9. Crear `ProductFormDialogComponent` (ts + html)

### Fase 4: CRUD Combos
10. Modificar `bundle-api.service.ts` para multipart
11. Crear `BundlesPageComponent` (ts + html + scss)
12. Crear `BundleFormDialogComponent` (ts + html)

### Fase 5: CRUD Órdenes
13. Crear `OrdersPageComponent` (ts + html + scss)
14. Crear `OrderDetailDialogComponent` (ts + html)

---

## 14. Resumen de permisos por rol

### `client` — Solo tienda
- Ve: productos, combos, precios, sucursales (solo lectura)
- NO ve: botón "Administrar", NO accede a `/admin`
- Puede: ver sus propias órdenes (`order:view`)

### `branch_admin` — Admin parcial
- Ve: botón "Administrar", accede a `/admin`
- Sidebar: Dashboard, Productos, Combos, Órdenes
- CRUD: productos, combos, precios, órdenes
- NO ve: RBAC

### `global_admin` — Admin total
- Ve: todo lo de `branch_admin` + RBAC
- Sidebar: Dashboard, Productos, Combos, Órdenes, RBAC
- CRUD: todo
