# Plan: Stock en creación/actualización de productos

## Reglas de negocio

1. **Crear**: el usuario asigna `stock` → `stock_available` se copia automáticamente (= `stock`), `stock_blocked` = 0.
2. **Actualizar**: el usuario cambia `stock`. `stock_blocked` se mantiene. `stock_available` se ajusta por el delta:
   - `delta = nuevo_stock - viejo_stock`
   - `nuevo_stock_available = viejo_stock_available + delta`
3. **Validaciones**: `stock >= 0`, `stock_available >= 0`, `stock_blocked <= stock`.

## Estado: IMPLEMENTADO

## Correcciones al plan original

- `emptyForm()`: `stock` es `number | undefined` (opcional), no acepta `null`. Se usa `undefined`.
- En el template, `[useGrouping]="false"` en `p-inputNumber` para evitar separadores de miles.
- El label del campo stock no lleva `*` porque es opcional (el backend defaultea a 0).

## Archivos modificados

| Archivo | Cambio |
|---|---|
| `api/internal/pricing/service.go` | `CreateProduct`: stock_available = stock, stock_blocked = 0. `UpdateProduct`: lee current, calcula delta, valida |
| `web/src/app/pages/admin/products/product-form-dialog.component.ts` | Agrega `stock` al form (undefined), al edit mode, y validación `< 0` en save() |
| `web/src/app/pages/admin/products/product-form-dialog.component.html` | Agrega `p-inputNumber` para stock |
