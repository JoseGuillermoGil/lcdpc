# Plan: Modulo de Configuracion

## 1. Objetivo

Crear un modulo de configuracion en el admin con dos sub-modulos:
- **RBAC** — gestionar perfiles, roles y recursos
- **Categorias** — gestionar categorias activas del portal

### Acceso

| Rol | Acceso a Configuracion | RBAC | Categorias |
|-----|------------------------|------|------------|
| `client` | NO | — | — |
| `staff` | NO | — | — |
| `manager` | NO | — | — |
| `branch_admin` | NO | — | — |
| `global_admin` | SI | CRUD completo | CRUD completo |

Solo `global_admin` accede al modulo de configuracion.

---

## 2. Arquitectura

### 2.1 Estructura de directorios

```
web/src/app/pages/admin/
├── config/
│   ├── config-layout.component.ts        # Shell con sub-nav
│   ├── config-layout.component.html
│   ├── config-layout.component.scss
│   ├── rbac/
│   │   ├── rbac-page.component.ts        # Tabs: Perfiles, Roles, Recursos
│   │   ├── rbac-page.component.html
│   │   ├── rbac-page.component.scss
│   │   ├── profiles-tab.component.ts
│   │   ├── profiles-tab.component.html
│   │   ├── roles-tab.component.ts
│   │   ├── roles-tab.component.html
│   │   ├── resources-tab.component.ts
│   │   ├── resources-tab.component.html
│   │   ├── profile-form-dialog.component.ts
│   │   ├── profile-form-dialog.component.html
│   │   ├── role-form-dialog.component.ts
│   │   ├── role-form-dialog.component.html
│   │   ├── role-resources-dialog.component.ts
│   │   ├── role-resources-dialog.component.html
│   │   └── profile-roles-dialog.component.ts
│   │   └── profile-roles-dialog.component.html
│   └── categories/
│       ├── categories-page.component.ts
│       ├── categories-page.component.html
│       ├── categories-page.component.scss
│       ├── category-form-dialog.component.ts
│       └── category-form-dialog.component.html
```

### 2.2 Rutas

```typescript
{
  path: 'config',
  component: ConfigLayoutComponent,
  canActivate: [permissionGuard('rbac:profile:view')],
  children: [
    { path: '', redirectTo: 'rbac', pathMatch: 'full' },
    { path: 'rbac', component: RbacPageComponent },
    { path: 'categories', component: CategoriesPageComponent, canActivate: [permissionGuard('category:create')] },
  ]
}
```

---

## 3. Look and Feel

### 3.1 Config Layout

El layout de configuracion usa un **sub-nav horizontal** dentro del contenido admin (no un sidebar adicional). Esto evita anidar sidebars y mantiene la jerarquia visual clara.

```
┌─────────────────────────────────────────────┐
│ [Sidebar Admin] │  Configuracion            │
│                 │  ┌─────────┬──────────┐   │
│  Dashboard      │  │   RBAC  │ Categorias│   │
│  Productos      │  └─────────┴──────────┘   │
│  Combos         │                            │
│  Ordenes        │  <contenido del sub-modulo>│
│  Personal       │                            │
│  Configuracion  │                            │
└─────────────────────────────────────────────┘
```

### 3.2 Sub-nav

- Fondo: `var(--surface)`
- Borde inferior: `1px solid var(--border)`
- Tabs: `p-button` con `[text]="true"` para inactivo, sin `[text]` para activo
- Tab activo: fondo `var(--surface-strong)`, texto `var(--text-strong)`
- Tab inactivo: texto `var(--text-soft)`

### 3.3 RBAC Page — Tabs internos

La pagina RBAC usa PrimeNG `p-tabs` para alternar entre:
- **Perfiles** — tabla con perfiles, roles asignados, acciones
- **Roles** — tabla con roles, recursos asignados, acciones
- **Recursos** — tabla con codigos de recursos, acciones

Cada tab tiene su propia tabla con:
- Fondo: `var(--surface)`, border-radius `8px`, shadow `var(--shadow)`
- Headers: `var(--font-body)`, `0.8rem`, `600wt`, uppercase, `var(--text-soft)`
- Filas: hover `rgba(0,0,0,0.02)`
- Acciones: iconos `[text]="true" [rounded]="true"`

### 3.4 Categories Page

Mismo patron que products/bundles:
- Toolbar con filtros
- Tabla con columns: Nombre, Slug, Orden, Activo, Acciones
- Dialog para crear/editar

### 3.5 Dialogs

- Ancho: `min(500px, 95vw)`
- Campos con `p-floatlabel`
- Botones en footer: Cancelar (secondary, text) + Guardar (primary)

---

## 4. Sub-modulo: RBAC

### 4.1 Backend — Endpoints existentes

Todos los endpoints ya existen y funcionan:

| Recurso | Listar | Obtener | Crear | Actualizar | Eliminar | Asignar | Remover |
|---------|--------|---------|-------|------------|----------|---------|---------|
| Resources | GET | GET/{id} | POST | PUT/{id} | DELETE/{id} | — | — |
| Roles | GET | GET/{id} | POST | PUT/{id} | DELETE/{id} | POST/{id}/resources | DELETE/{id}/resources/{rid} |
| Profiles | GET | GET/{id} | POST | PUT/{id} | DELETE/{id} | POST/{id}/roles | DELETE/{id}/roles/{rid} |

No se necesita backend nuevo.

### 4.2 Frontend — Servicios existentes

`RbacApiService` ya tiene todos los metodos implementados. Solo hay que consumirlos.

### 4.3 Componentes

#### RbacPageComponent

Contenedor con `p-tabs` que renderiza los tres sub-tabs.

```typescript
@Component({
  selector: 'app-rbac-page',
  standalone: true,
  imports: [CommonModule, TabsModule, ProfilesTabComponent, RolesTabComponent, ResourcesTabComponent],
  template: `
    <div class="rbac-page">
      <div class="page-header">
        <h1>RBAC</h1>
      </div>
      <p-tabs [(value)]="activeTab">
        <p-tablist>
          <p-tab value="profiles">Perfiles</p-tab>
          <p-tab value="roles">Roles</p-tab>
          <p-tab value="resources">Recursos</p-tab>
        </p-tablist>
        <p-tabpanels>
          <p-tabpanel value="profiles">
            <app-profiles-tab />
          </p-tabpanel>
          <p-tabpanel value="roles">
            <app-roles-tab />
          </p-tabpanel>
          <p-tabpanel value="resources">
            <app-resources-tab />
          </p-tabpanel>
        </p-tabpanels>
      </p-tabs>
    </div>
  `
})
export class RbacPageComponent {
  protected activeTab = signal('profiles');
}
```

#### ProfilesTabComponent

**Tabla** con:
- Columnas: Nombre, Codigo, Roles, Acciones
- Boton "Nuevo Perfil"
- Acciones: Editar, Gestionar Roles, Eliminar
- Los roles se muestran como `p-tag` inline

**ProfileFormDialogComponent:**
- Campos: `name` (InputText), `code` (InputText)
- Crear y editar

**ProfileRolesDialogComponent:**
- Muestra roles actuales del perfil
- Select para agregar nuevo rol
- Boton para remover cada rol

#### RolesTabComponent

**Tabla** con:
- Columnas: Codigo, Nombre, Descripcion, Recursos (count), Acciones
- Boton "Nuevo Rol"
- Acciones: Editar, Gestionar Recursos, Eliminar

**RoleFormDialogComponent:**
- Campos: `code` (InputText), `name` (InputText), `description` (InputText)

**RoleResourcesDialogComponent:**
- Lista de recursos actuales del rol con boton remover
- Select multi para agregar nuevos recursos

#### ResourcesTabComponent

**Tabla** con:
- Columnas: Codigo, Acciones
- Boton "Nuevo Recurso"
- Acciones: Editar, Eliminar

**ResourceFormDialogComponent:**
- Campo: `code` (InputText)

---

## 5. Sub-modulo: Categorias

### 5.1 Backend — Endpoints existentes

| Metodo | Endpoint | Auth | Permiso |
|--------|----------|------|---------|
| GET | `/api/v1/categories/` | No | — |
| GET | `/api/v1/categories/{id}` | No | — |
| POST | `/api/v1/categories/` | SI | `category:create` |
| PUT | `/api/v1/categories/{id}` | SI | `category:update` |
| DELETE | `/api/v1/categories/{id}` | SI | `category:delete` |

### 5.2 Frontend — Modelo actualizado

Agregar `UpdateCategoryRequest` al modelo:

```typescript
export interface UpdateCategoryRequest {
  name?: string;
  slug?: string;
  sort_order?: number;
  is_active?: boolean;
}
```

### 5.3 Frontend — Servicio actualizado

`CategoryApiService.update()` debe aceptar `UpdateCategoryRequest` en vez de `CreateCategoryRequest`.

### 5.4 Componentes

#### CategoriesPageComponent

**Tabla** con:
- Columnas: Nombre, Slug, Orden, Activo, Acciones
- Toolbar con boton "Nueva Categoria"
- Acciones: Editar, Activar/Desactivar, Eliminar
- Sin paginado (tabla pequeña)

**CategoryFormDialogComponent:**
- Campos: `name` (InputText), `slug` (InputText, auto-generado del nombre), `sort_order` (InputNumber)
- En edicion: incluye toggle `is_active`

---

## 6. Sidebar

### 6.1 Agregar "Configuracion" al sidebar

En `admin-layout.component.html`, agregar al final del nav (antes del footer):

```html
@if (canViewConfig()) {
  <a routerLink="/admin/config" routerLinkActive="active">
    <span class="pi pi-cog"></span>
    <span>Configuracion</span>
  </a>
}
```

En `admin-layout.component.ts`:

```typescript
protected readonly canViewConfig = computed(() =>
  this.authStore.hasAnyPermission('rbac:profile:view', 'category:create')
);
```

### 6.2 Header

No necesita cambios — `rbac:profile:view` ya esta incluido en el computed `isAdmin`.

---

## 7. Archivos a crear

### Frontend — Config Layout (3 archivos)

| # | Archivo |
|---|---------|
| 1 | `pages/admin/config/config-layout.component.ts` |
| 2 | `pages/admin/config/config-layout.component.html` |
| 3 | `pages/admin/config/config-layout.component.scss` |

### Frontend — RBAC (12 archivos)

| # | Archivo |
|---|---------|
| 4 | `pages/admin/config/rbac/rbac-page.component.ts` |
| 5 | `pages/admin/config/rbac/rbac-page.component.html` |
| 6 | `pages/admin/config/rbac/profiles-tab.component.ts` |
| 7 | `pages/admin/config/rbac/profiles-tab.component.html` |
| 8 | `pages/admin/config/rbac/roles-tab.component.ts` |
| 9 | `pages/admin/config/rbac/roles-tab.component.html` |
| 10 | `pages/admin/config/rbac/resources-tab.component.ts` |
| 11 | `pages/admin/config/rbac/resources-tab.component.html` |
| 12 | `pages/admin/config/rbac/profile-form-dialog.component.ts` |
| 13 | `pages/admin/config/rbac/profile-form-dialog.component.html` |
| 14 | `pages/admin/config/rbac/role-form-dialog.component.ts` |
| 15 | `pages/admin/config/rbac/role-form-dialog.component.html` |

### Frontend — Categorias (4 archivos)

| # | Archivo |
|---|---------|
| 16 | `pages/admin/config/categories/categories-page.component.ts` |
| 17 | `pages/admin/config/categories/categories-page.component.html` |
| 18 | `pages/admin/config/categories/categories-page.component.scss` |
| 19 | `pages/admin/config/categories/category-form-dialog.component.ts` |
| 20 | `pages/admin/config/categories/category-form-dialog.component.html` |

### Frontend — Modificaciones (4 archivos)

| # | Archivo | Cambio |
|---|---------|--------|
| 21 | `app.routes.ts` | Agregar ruta `/admin/config` con children |
| 22 | `pages/admin/admin-layout.component.ts` | Agregar `canViewConfig` computed |
| 23 | `pages/admin/admin-layout.component.html` | Agregar link "Configuracion" |
| 24 | `core/models/category.model.ts` | Agregar `UpdateCategoryRequest` |
| 25 | `core/services/category-api.service.ts` | `update()` acepta `UpdateCategoryRequest` |

---

## 8. Orden de implementacion

### Fase 1: Infraestructura
1. Crear `ConfigLayoutComponent` con sub-nav
2. Actualizar rutas y sidebar
3. Actualizar modelo y servicio de categorias

### Fase 2: Categorias
4. Crear `CategoriesPageComponent` con tabla
5. Crear `CategoryFormDialogComponent`

### Fase 3: RBAC — Recursos
6. Crear `ResourcesTabComponent` con tabla
7. Crear resource form (inline o dialog)

### Fase 4: RBAC — Roles
8. Crear `RolesTabComponent` con tabla
9. Crear `RoleFormDialogComponent`
10. Crear dialog para asignar recursos a rol

### Fase 5: RBAC — Perfiles
11. Crear `ProfilesTabComponent` con tabla
12. Crear `ProfileFormDialogComponent`
13. Crear dialog para asignar roles a perfil

### Fase 6: Verificacion
14. Typecheck frontend
