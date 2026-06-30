# Plan: Modulo de Configuracion (Tag-Based)

## 1. Objetivo

Crear un modulo de configuracion en el admin donde todo este agrupado por **tags**.
El usuario ve tags en la parte superior y filtra la configuracion relevante.

Tags definidos:
- **RBAC** — perfiles, roles, recursos
- **Inventario** — categorias, categorias de precio, unidades de medida, clasificaciones

### Acceso

| Rol | Acceso a Config | RBAC | Inventario |
|-----|-----------------|------|------------|
| `client` | NO | — | — |
| `staff` | NO | — | — |
| `manager` | NO | — | — |
| `branch_admin` | NO | — | — |
| `global_admin` | SI | CRUD completo | CRUD completo |

Solo `global_admin` accede al modulo de configuracion.

---

## 2. Arquitectura

### 2.1 Concepto de Tags

La pagina de configuracion NO usa sub-rutas. Es una sola pagina con **tags como filtros**.

```
┌──────────────────────────────────────────────────┐
│ Configuracion                                     │
│                                                   │
│  [RBAC]  [Inventario]                             │
│                                                   │
│  ┌─────────────────────────────────────────────┐ │
│  │ Categorias de Precio                        │ │
│  │ ┌────────────────────┬──────────┬─────────┐ │ │
│  │ │ Nombre             │ Orden    │ Accion  │ │ │
│  │ ├────────────────────┼──────────┼─────────┤ │ │
│  │ │ Minorista          │ 1        │ ✏ 🗑   │ │ │
│  │ │ Mayorista          │ 2        │ ✏ 🗑   │ │ │
│  │ └────────────────────┴──────────┴─────────┘ │ │
│  └─────────────────────────────────────────────┘ │
│                                                   │
│  ┌─────────────────────────────────────────────┐ │
│  │ Unidades de Medida                          │ │
│  │ ...                                         │ │
│  └─────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────┘
```

### 2.2 Estructura de directorios

```
web/src/app/pages/admin/
├── config/
│   ├── config-page.component.ts           # Pagina principal con tags
│   ├── config-page.component.html
│   ├── config-page.component.scss
│   └── sections/                          # Cada seccion es un componente standalone
│       ├── rbac-section.component.ts      # Tabla perfiles + roles + recursos (tabs internos)
│       ├── rbac-section.component.html
│       ├── categories-section.component.ts
│       ├── categories-section.component.html
│       ├── price-categories-section.component.ts
│       ├── price-categories-section.component.html
│       ├── measurement-units-section.component.ts
│       ├── measurement-units-section.component.html
│       └── (dialogs reutilizados de componentes existentes o nuevos)
```

### 2.3 Rutas

```typescript
{
  path: 'config',
  component: ConfigPageComponent,
  canActivate: [permissionGuard('rbac:profile:view')],
}
```

Sin children. Una sola pagina.

---

## 3. Tag System

### 3.1 Definicion de tags

Cada seccion de configuracion tiene un tag asignado. Los tags se definen como datos, no como rutas.

```typescript
type ConfigTag = 'rbac' | 'inventario';

interface ConfigSection {
  tag: ConfigTag;
  label: string;
  component: Type<any>;  // componente standalone
}

const CONFIG_SECTIONS: ConfigSection[] = [
  { tag: 'rbac', label: 'RBAC', component: RbacSectionComponent },
  { tag: 'inventario', label: 'Categorias', component: CategoriesSectionComponent },
  { tag: 'inventario', label: 'Categorias de Precio', component: PriceCategoriesSectionComponent },
  { tag: 'inventario', label: 'Unidades de Medida', component: MeasurementUnitsSectionComponent },
];
```

### 3.2 Filtro por tag

- `activeTag` signal: `ConfigTag | null`
- Si `null` → muestra todas las secciones
- Si tiene valor → filtra por ese tag
- Tags se renderizan como `p-button` con `[outlined]` para inactivo, sin outlined para activo

### 3.3 Renderizado

```html
<div class="config-tags">
  @for (tag of tags; track tag.key) {
    <p-button [label]="tag.label" [outlined]="activeTag() !== tag.key"
              (onClick)="activeTag.set(tag.key)" />
  }
  @if (activeTag()) {
    <p-button icon="pi pi-times" [text]="true" (onClick)="activeTag.set(null)" />
  }
</div>

<div class="config-sections">
  @for (section of filteredSections(); track section.label) {
    <div class="config-section-card">
      <h3>{{ section.label }}</h3>
      <ng-container *ngComponentOutlet="section.component" />
    </div>
  }
</div>
```

---

## 4. Look and Feel

### 4.1 Pagina de Configuracion

- Titulo: "Configuracion" con icono `pi-cog`
- Tags debajo del titulo, alineados a la izquierda
- Gap entre secciones: `1.5rem`

### 4.2 Tags

- `p-button` con `severity="primary"`, `size="small"`
- Activo: sin `[outlined]` (solid)
- Inactivo: `[outlined]="true"`
- Boton "X" para limpiar filtro (solo si hay tag activo)

### 4.3 Secciones

Cada seccion es una tarjeta con:
- Titulo en header
- Contenido (tabla con CRUD) en body
- Fondo: `var(--surface)`, border-radius `8px`, shadow `var(--shadow)`
- Mismo patron visual que products/bundles

### 4.4 Tablas dentro de secciones

- Headers: `var(--font-body)`, `0.8rem`, `600wt`, uppercase, `var(--text-soft)`
- Filas: hover `rgba(0,0,0,0.02)`
- Acciones: iconos `[text]="true" [rounded]="true"`
- Empty state: texto centrado sin icono

### 4.5 Dialogs

- Ancho: `min(500px, 95vw)`
- Campos con `p-floatlabel` donde aplique
- Selects con `appendTo="body"`
- Botones en footer: Cancelar (secondary) + Guardar (primary)
- `cursor: pointer` en todos los botones

---

## 5. Seccion: RBAC (tag: `rbac`)

### 5.1 Backend — Endpoints existentes

Todos los endpoints ya existen. No se necesita backend nuevo.

| Recurso | Listar | Crear | Actualizar | Eliminar | Asignar | Remover |
|---------|--------|-------|------------|----------|---------|---------|
| Resources | GET | POST | PUT/{id} | DELETE/{id} | — | — |
| Roles | GET | POST | PUT/{id} | DELETE/{id} | POST/{id}/resources | DELETE/{id}/resources/{rid} |
| Profiles | GET | POST | PUT/{id} | DELETE/{id} | POST/{id}/roles | DELETE/{id}/roles/{rid} |

### 5.2 Frontend — Servicios existentes

`RbacApiService` ya tiene todos los metodos.

### 5.3 Componente: RbacSectionComponent

Usa `p-tabs` internos para Perfiles / Roles / Recursos.

**Tab Perfiles:**
- Tabla: Nombre, Codigo, Roles (p-tag inline), Acciones
- Acciones: Editar, Gestionar Roles, Eliminar
- Dialog: `name`, `code`
- Dialog roles: select para agregar, lista con boton remover

**Tab Roles:**
- Tabla: Codigo, Nombre, Descripcion, Recursos (count), Acciones
- Acciones: Editar, Gestionar Recursos, Eliminar
- Dialog: `code`, `name`, `description`
- Dialog recursos: multi-select para agregar, lista con boton remover

**Tab Recursos:**
- Tabla: Codigo, Acciones
- Acciones: Editar, Eliminar
- Dialog: `code`

---

## 6. Seccion: Categorias (tag: `inventario`)

### 6.1 Backend — Endpoints existentes

| Metodo | Endpoint | Permiso |
|--------|----------|---------|
| GET | `/api/v1/categories/` | — |
| POST | `/api/v1/categories/` | `category:create` |
| PUT | `/api/v1/categories/{id}` | `category:update` |
| DELETE | `/api/v1/categories/{id}` | `category:delete` |

### 6.2 Componente: CategoriesSectionComponent

Tabla inline (sin paginado) con:
- Columnas: Nombre, Slug, Orden, Activo, Acciones
- Acciones: Editar, Activar/Desactivar, Eliminar
- Dialog: `name`, `slug` (auto-generado), `sort_order`, `is_active` (toggle en edicion)

---

## 7. Seccion: Categorias de Precio (tag: `inventario`)

### 7.1 Backend — Endpoints existentes

| Metodo | Endpoint | Permiso |
|--------|----------|---------|
| GET | `/api/v1/price-categories/` | — |
| POST | `/api/v1/price-categories/` | `price_category:create` |
| PUT | `/api/v1/price-categories/{id}` | `price_category:update` |
| DELETE | `/api/v1/price-categories/{id}` | `price_category:delete` |

### 7.2 Componente: PriceCategoriesSectionComponent

Tabla inline con:
- Columnas: Nombre, Slug, Descripcion, Activo, Acciones
- Acciones: Editar, Activar/Desactivar, Eliminar
- Dialog: `name`, `slug`, `description`, `is_active`

---

## 8. Seccion: Unidades de Medida (tag: `inventario`)

### 8.1 Backend — Endpoints existentes

| Metodo | Endpoint | Permiso |
|--------|----------|---------|
| GET | `/api/v1/measurement-units/` | — |
| POST | `/api/v1/measurement-units/` | `measurement_unit:create` |
| PUT | `/api/v1/measurement-units/{id}` | `measurement_unit:update` |
| DELETE | `/api/v1/measurement-units/{id}` | `measurement_unit:delete` |

### 8.2 Componente: MeasurementUnitsSectionComponent

Tabla inline con:
- Columnas: Nombre, Simbolo, Clasificacion, Acciones
- Acciones: Editar, Eliminar
- Dialog: `name`, `symbol`, `classification_id` (select)

---

## 9. Sidebar

### 9.1 Agregar "Configuracion" al sidebar

En `admin-layout.component.html`, agregar al final del nav (antes del footer):

```html
@if (canViewConfig()) {
  <a routerLink="/admin/config" routerLinkActive="active">
    <span class="pi pi-cog"></span>
    <span class="nav-label">Configuracion</span>
  </a>
}
```

En `admin-layout.component.ts`:

```typescript
protected readonly canViewConfig = computed(() =>
  this.authStore.hasAnyPermission('rbac:profile:view', 'category:create')
);
```

### 9.2 Remover "Clasificaciones" del sidebar

La seccion de clasificaciones se mueve a Configuracion > Inventario.
Remover el link actual de "Clasificaciones" del sidebar.
Remover la ruta `/admin/classifications` de `app.routes.ts`.

---

## 10. Archivos

### Crear (8 archivos)

| # | Archivo | Descripcion |
|---|---------|-------------|
| 1 | `pages/admin/config/config-page.component.ts` | Pagina principal con tags + secciones |
| 2 | `pages/admin/config/config-page.component.html` | Template con tags + ngComponentOutlet |
| 3 | `pages/admin/config/config-page.component.scss` | Estilos de tags y secciones |
| 4 | `pages/admin/config/sections/rbac-section.component.ts` | Seccion RBAC con tabs |
| 5 | `pages/admin/config/sections/rbac-section.component.html` | Template RBAC |
| 6 | `pages/admin/config/sections/categories-section.component.ts` | Seccion categorias |
| 7 | `pages/admin/config/sections/categories-section.component.html` | Template categorias |
| 8 | `pages/admin/config/sections/price-categories-section.component.ts` | Seccion categorias de precio |
| 9 | `pages/admin/config/sections/price-categories-section.component.html` | Template categorias de precio |
| 10 | `pages/admin/config/sections/measurement-units-section.component.ts` | Seccion unidades |
| 11 | `pages/admin/config/sections/measurement-units-section.component.html` | Template unidades |

### Modificar (3 archivos)

| # | Archivo | Cambio |
|---|---------|--------|
| 12 | `app.routes.ts` | Agregar ruta `/admin/config`, remover `/admin/classifications` |
| 13 | `pages/admin/admin-layout.component.ts` | Agregar `canViewConfig`, remover `canViewProducts` de clasificaciones |
| 14 | `pages/admin/admin-layout.component.html` | Agregar "Configuracion", remover "Clasificaciones" |

### Modelos/Servicios existentes (NO crear)

- `category-api.service.ts` — ya existe, agregar `UpdateCategoryRequest` al modelo
- `price-category-api.service.ts` — ya existe
- `measurement-unit-api.service.ts` — ya existe
- `measurement-unit-classification-api.service.ts` — ya existe
- `rbac-api.service.ts` — ya existe

---

## 11. Orden de implementacion

### Fase 1: Infraestructura
1. Crear `ConfigPageComponent` con sistema de tags
2. Actualizar rutas y sidebar (agregar config, remover clasificaciones)
3. Agregar `UpdateCategoryRequest` al modelo de categorias

### Fase 2: Secciones Inventario
4. Crear `CategoriesSectionComponent`
5. Crear `PriceCategoriesSectionComponent`
6. Crear `MeasurementUnitsSectionComponent`

### Fase 3: Seccion RBAC
7. Crear `RbacSectionComponent` con tabs (Perfiles, Roles, Recursos)
8. Implementar dialogs de RBAC (profile form, role form, resource form)
9. Implementar dialogs de asignacion (profile roles, role resources)

### Fase 4: Verificacion
10. Typecheck frontend
