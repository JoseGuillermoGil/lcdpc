# Domain Model: Pricing por sede + Mayor/Detal

## Objetivo

Definir el modelo de dominio ejecutable para catálogo, carrito, pricing por sede, productos por peso/unidad y combos promocionales.

## Bounded Contexts involucrados

- `CatalogoContext`
- `PricingContext`
- `CarritoContext`
- `CombosContext`
- `SedesContext`

## Entidades

### Producto

- `productoId: UUID`
- `nombre: string`
- `sku: string`
- `tipoMedidaBase: enum { unidad, gramos, kilo }`
- `tipoComercialMayor: enum { pieza, caja, bulto }`
- `unidadesPorCaja?: number`
- `unidadesPorBulto?: number`
- `activo: boolean`

### Sede

- `sedeId: UUID`
- `nombre: string`
- `codigo: string`
- `activa: boolean`

### PrecioProductoSede

- `precioProductoSedeId: UUID`
- `productoId: UUID`
- `sedeId: UUID`
- `precio1Unidad: Money`
- `precio2CajaBultoPieza: Money`
- `precio3MayorDesde2: Money`
- `precio4MayoristaNegociable?: Money`
- `precio4RequiereAcuerdo: boolean`
- `vigenteDesde: DateTime`
- `vigenteHasta?: DateTime`

### Carrito

- `carritoId: UUID`
- `sedeId: UUID` (obligatoria)
- `estado: enum { abierto, confirmado, cancelado }`
- `lineas: LineaCarrito[]`

### LineaCarrito

- `lineaId: UUID`
- `productoId: UUID`
- `cantidad: number`
- `unidadCompra: enum { unidad, caja, bulto, pieza, gramos, kilo }`
- `precioAplicado: Money`
- `origenPrecio: enum { precio1, precio2, precio3, precio4, comboPromocional, referencial }`

### Combo

- `comboId: UUID`
- `nombre: string`
- `estado: enum { borrador, publicado, pausado }`
- `items: ComboItem[]`
- `precioTotal: Money`
- `precioPromocional?: Money`
- `sedeIdsHabilitadas: UUID[]`

### ComboItem

- `productoId: UUID`
- `cantidad: number`

## Value Objects

### Money

- `amount: decimal`
- `currency: string` (v1: `USD`)

### CantidadComercial

- `cantidad: number`
- `unidadCompra: UnidadCompra`

### UmbralEmpaque

- `unidadesPorCaja: number`
- `unidadesPorBulto: number`

## Agregados

### Agregado `Carrito`

- Raíz: `Carrito`
- Contiene: `LineaCarrito`
- Invariantes:
  - El carrito siempre tiene una sola `sedeId`.
  - No admite líneas de productos no disponibles en su sede.
  - Rechaza cualquier intento de agregar productos de otra sede.
  - Toda línea debe tener `precioAplicado` resuelto al momento de persistir.

### Agregado `Combo`

- Raíz: `Combo`
- Contiene: `ComboItem`
- Invariantes:
  - `precioTotal` se recalcula desde items.
  - `precioPromocional`, si existe, debe ser `<= precioTotal` (si no, requiere confirmación admin explícita).
  - Solo `publicado` es visible en catálogo.

## Servicios de dominio

### PricingService

Responsable de resolver precio final por línea según:

- `sedeId`
- `tipoMedidaBase`
- `tipoComercialMayor`
- `unidadCompra`
- `cantidad`
- tabla `PrecioProductoSede`

### UnidadComercialResolver

Responsable de clasificar compra para productos `tipoMedidaBase = unidad`:

- si `cantidad >= unidadesPorBulto` => `bulto`
- else if `cantidad >= unidadesPorCaja` => `caja`
- else => `unidad`

### ComboPricingService

- calcula `precioTotal` del combo
- valida `precioPromocional`
- resuelve precio visible por sede

## Reglas de pricing (ejecutables)

1. Sin sede seleccionada, precio visible es `referencial` (`~25$`) y no transaccional.
2. Con sede seleccionada, precio por línea usa `PrecioProductoSede`.
3. Para `unidadCompra in {unidad,gramos,kilo}` => `precio1`.
4. Para `unidadCompra in {caja,bulto,pieza}` y `cantidad = 1` => `precio2`.
5. Para `unidadCompra in {caja,bulto,pieza}` y `cantidad > 1` y `<= 50` => `precio3`.
6. Para `unidadCompra in {caja,bulto,pieza}` y `cantidad > 50` => `precio4` (si requiere acuerdo, marcar estado negociable).
7. Para `tipoMedidaBase in {gramos,kilo}` en detal, se respeta la unidad de peso; en mayor puede usar `pieza`.

## Reglas de carrito

1. No se crea ni abre carrito sin `sedeId`.
2. Toda operación del carrito (altas, cálculos, totales, combos) se evalúa contra la sede seleccionada.
3. Si el usuario cambia sede con carrito abierto:
   - requiere confirmación explícita.
   - política v1: limpiar carrito y recalcular contexto.
4. No se permiten líneas de otra sede en el mismo carrito.

## Reglas de combos

1. Un combo solo es comprable si está `publicado` y disponible para la sede activa.
2. Combo hereda regla de sede única del carrito.
3. Si un item del combo no está disponible en la sede, el combo no es comprable.

## Eventos de dominio (propuestos)

- `SedeSeleccionadaEnSesion`
- `CarritoCreado`
- `LineaCarritoAgregada`
- `SedeCarritoCambiada`
- `PrecioLineaResuelto`
- `ComboCreado`
- `ComboPublicado`
- `PrecioPromocionalComboActualizado`

## Tabla de decisión resumida

| Escenario | Resultado |
|---|---|
| Sin sede seleccionada | Mostrar `~25$` referencial |
| Con sede + compra unidad | aplicar `precio1` |
| Con sede + compra caja/bulto/pieza + cantidad 1 | aplicar `precio2` |
| Con sede + compra caja/bulto/pieza + cantidad > 1 y <= 50 | aplicar `precio3` |
| Con sede + compra caja/bulto/pieza + cantidad > 50 | aplicar `precio4` (negociable si aplica) |
| Combo publicado + sede válida | comprable |
| Combo publicado + item sin disponibilidad sede | no comprable |

## Contratos de aplicación (v1)

- `SeleccionarSede(sedeId)`
- `AgregarProductoAlCarrito(sedeId, productoId, cantidad, unidadCompra)`
- `CrearCombo(nombre, items[])`
- `PublicarCombo(comboId)`
- `ActualizarPrecioPromocionalCombo(comboId, precioPromocional)`
