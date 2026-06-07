---
name: primeng
description: Guía de componentes PrimeNG usados en People. Úsala para trabajar con UI y componentes.
---

# PrimeNG Skill

Esta skill te guía en el uso de PrimeNG en People.

## Componentes Más Usados

### Buttons

```typescript
import { ButtonModule } from 'primeng/button';

// Template
<p-button
  label="Guardar"
  icon="pi pi-check"
  [loading]="isLoading()"
  [disabled]="form.invalid"
  (onClick)="save()"
/>

// Severities: primary, secondary, success, info, warn, danger
<p-button label="Eliminar" severity="danger" />
<p-button label="Cancelar" severity="secondary" />
```

### Tables

```typescript
import { TableModule } from 'primeng/table';

<p-table
  [value]="items()"
  [paginator]="true"
  [rows]="10"
  [rowsPerPageOptions]="[10, 25, 50]"
  styleClass="p-datatable-striped"
>
  <ng-template pTemplate="header">
    <tr>
      <th pSortableColumn="name">Nombre <p-sortIcon field="name" /></th>
      <th>Acciones</th>
    </tr>
  </ng-template>
  <ng-template pTemplate="body" let-item>
    <tr>
      <td>{{ item.name }}</td>
      <td>
        <p-button icon="pi pi-pencil" (onClick)="edit(item)" />
      </td>
    </tr>
  </ng-template>
</p-table>
```

### Dialogs

> [!IMPORTANT] > **Regla de UX**: Todos los diálogos deben incluir `[dismissableMask]="true"` para que se cierren al hacer clic fuera. Esto mejora la experiencia del usuario.

```typescript
import { DialogModule } from 'primeng/dialog';

<p-dialog
  header="Título"
  [(visible)]="dialogVisible"
  [modal]="true"
  [dismissableMask]="true"
  [closable]="true"
  [style]="{ width: '500px' }"
>
  <ng-template pTemplate="content">
    <!-- Contenido -->
  </ng-template>
  <ng-template pTemplate="footer">
    <p-button label="Cancelar" severity="secondary" (onClick)="close()" />
    <p-button label="Guardar" (onClick)="save()" />
  </ng-template>
</p-dialog>
```

### Forms

```typescript
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { CalendarModule } from 'primeng/calendar';

// Input
<input pInputText formControlName="name" />

// Select (antes Dropdown)
<p-select
  formControlName="branch_id"
  [options]="branches()"
  optionLabel="name"
  optionValue="id"
  placeholder="Seleccione..."
  [filter]="true"
/>

// Calendar (DatePicker)
<p-datepicker
  formControlName="date"
  dateFormat="dd/mm/yy"
  [showIcon]="true"
/>
```

### Cards

```typescript
import { CardModule } from 'primeng/card';

<p-card>
  <ng-template pTemplate="title">Título</ng-template>
  <ng-template pTemplate="subtitle">Subtítulo</ng-template>
  <ng-template pTemplate="content">
    <!-- Contenido -->
  </ng-template>
  <ng-template pTemplate="footer">
    <p-button label="Action" />
  </ng-template>
</p-card>
```

### Toast (Notificaciones)

```typescript
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

// En providers
providers: [MessageService]

// Template
<p-toast />

// Uso
private message = inject(MessageService);

showSuccess(): void {
  this.message.add({
    severity: 'success',
    summary: 'Éxito',
    detail: 'Operación completada'
  });
}

showError(error: string): void {
  this.message.add({
    severity: 'error',
    summary: 'Error',
    detail: error
  });
}
```

### Tags

```typescript
import { TagModule } from 'primeng/tag';

<p-tag value="Activo" severity="success" />
<p-tag value="Pendiente" severity="warn" />
<p-tag value="Error" severity="danger" />
<p-tag value="Info" severity="info" />
```

### Avatar

```typescript
import { AvatarModule } from 'primeng/avatar';

<p-avatar
  [image]="employee.photo_url"
  [label]="employee.first_name[0]"
  shape="circle"
  size="large"
/>
```

### Progress

```typescript
import { ProgressSpinnerModule } from 'primeng/progressspinner';

@if (isLoading()) {
  <p-progressSpinner strokeWidth="4" />
}
```

## Iconos (PrimeIcons)

```html
<i class="pi pi-check"></i>
<i class="pi pi-times"></i>
<i class="pi pi-pencil"></i>
<i class="pi pi-trash"></i>
<i class="pi pi-plus"></i>
<i class="pi pi-search"></i>
<i class="pi pi-user"></i>
<i class="pi pi-calendar"></i>
<i class="pi pi-clock"></i>
<i class="pi pi-file"></i>
```

## Estilos Comunes

```css
/* Tabla striped */
styleClass="p-datatable-striped"

/* Tabla con grid */
styleClass="p-datatable-gridlines"

/* Hover en filas */
.cursor-pointer:hover {
  background: var(--surface-hover);
}

/* Responsive */
@media (max-width: 640px) {
  ::ng-deep .p-dialog {
    width: 95vw !important;
  }
}
```
