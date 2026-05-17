# Spec: Pricing por sede + Mayor/Detal

## Estado

- Propuesto: 2026-05-16
- Prioridad: Alta

## Contexto

La plataforma vende productos al mayor y al detal, con inventario y precios por sede.
El cliente puede navegar catálogo, buscar, agregar al carrito y confirmar pedido por WhatsApp.

## Problema

Sin reglas explícitas de pricing:

- El usuario ve precios ambiguos antes de elegir sede.
- El carrito puede mezclar sedes, rompiendo el cálculo real.
- No existe una estrategia formal de niveles de precio para mayor/detal.

## Decisiones de negocio

1. Antes de seleccionar sede, se muestra un precio referencial cercano (`~25$`) para orientación comercial.
2. El precio real del producto se determina cuando el usuario selecciona una sede.
3. Para pasar al carrito, debe existir una única sede seleccionada para toda la orden.
4. Una vez seleccionada la sede, todo cálculo del flujo comercial (precio por línea, subtotales, total y combos) se realiza exclusivamente con esa sede.
5. En administración, cada producto define 4 niveles de precios:
   - Precio 1: por unidad.
   - Precio 2: por caja/bulto/pieza.
   - Precio 3: mayor (a partir de 2 o más cajas/bultos/piezas).
   - Precio 4: proveedor mayorista (negociable), para cantidades mayores de 50 cajas/bultos/piezas.
6. El precio 1 también aplica a productos por peso (`gramos`/`kilo`) en venta al detal.
7. Existen productos por peso (gramos/kilo), por ejemplo queso; en venta al mayor estos se manejan por pieza.
8. Para productos por unidad, debe configurarse en admin a partir de qué cantidad la compra se considera caja o bulto.
9. Debe existir un módulo de combos en admin para construir, publicar y promocionar conjuntos de productos.

## Alcance funcional

### Catálogo y búsqueda

- Mostrar precio referencial `~25$` cuando no hay sede seleccionada.
- Mostrar precio de sede al seleccionar sede.
- Etiquetar claramente el tipo de precio mostrado (`referencial` o `sede`).

### Carrito

- Bloquear ingreso al carrito sin sede seleccionada.
- Impedir mezcla de sedes en una misma orden.
- Recalcular precios al cambiar sede (si existiera cambio antes de confirmar).
- Una vez que el carrito tiene líneas, no permitir agregar productos de otra sede.

### Administración (inventario/precios)

- Formulario de producto por sede con campos de precio 1..4.
- Reglas de validación por umbral de cantidad para aplicar precio.
- Precio 4 marcado como negociable y sujeto a acuerdo comercial.

### Modelado de producto

- Cada producto debe definir:
  - Tipo de medida base (`unidad`, `gramos`, `kilo`).
  - Tipo comercial para mayor (`pieza`, `caja`, `bulto`).
  - Umbral configurable de conversión para `caja` y `bulto` cuando aplica.
- Productos por peso (ej: queso) pueden venderse al detal por peso y al mayor por pieza.

### Módulo de combos (admin)

- Crear combo con múltiples productos y cantidades por ítem.
- Calcular `precio total` (suma base de sus componentes).
- Definir `precio promocional` editable para publicar oferta.
- Publicar/despublicar combo para visibilidad en catálogo.
- Mantener estado de combo (`borrador`, `publicado`, `pausado`).

## Reglas de dominio

- `SedeSeleccionada` es obligatoria para calcular total final.
- Una `Orden` pertenece a una sola `Sede`.
- Un `Carrito` pertenece a una sola `Sede` y no admite mezcla de sedes bajo ninguna circunstancia.
- El precio aplicado por ítem depende de:
  - Sede.
  - Unidad comercial (unidad/caja/bulto/pieza).
  - Cantidad.
  - Tabla de precios configurada para el producto.
- Un producto define su lógica de medida y su conversión comercial (si aplica).
- Un combo se trata como una oferta compuesta con precio promocional explícito.

## Política de aplicación de precios (v1)

1. Precio 1: si la compra es por `unidad` o por `peso` (`gramos`/`kilo`), usar `precio1`.
2. Precio 2: si la compra es por `caja`, `bulto` o `pieza` y la cantidad es exactamente `1`, usar `precio2`.
3. Precio 3: si la compra es por `caja`, `bulto` o `pieza` y la cantidad es `> 1` y `<= 50`, usar `precio3`.
4. Precio 4: si la compra es por `caja`, `bulto` o `pieza` y la cantidad es `> 50`, usar `precio4`.

## Política de unidades y conversión (v1)

1. Si `tipoMedidaBase` es `gramos` o `kilo`, el detal usa medida de peso.
2. Para esos mismos productos, el mayor puede operar con `tipoComercialMayor = pieza`.
3. Si `tipoMedidaBase` es `unidad`, la admin define:
  - `unidadesPorCaja` (umbral de caja).
  - `unidadesPorBulto` (umbral de bulto).
4. El motor de precios debe identificar automáticamente si la cantidad cae en unidad/caja/bulto según esos umbrales.

## Política de combos (v1)

1. El `precioTotal` del combo se calcula como la suma de (precio base vigente por ítem x cantidad).
2. El `precioPromocional` del combo es editable en admin y puede ser menor al total base.
3. Solo combos `publicados` aparecen en catálogo.
4. Un combo usa sede única igual que el resto de la orden; no mezcla productos de sedes distintas.

## Criterios de aceptación

1. Usuario sin sede seleccionada ve `~25$` en catálogo.
2. Al seleccionar sede, todos los precios visibles cambian a precio real de esa sede.
3. El sistema impide entrar al carrito sin sede seleccionada.
4. El sistema impide agregar productos de una segunda sede a un carrito activo.
5. En admin se pueden guardar y editar los 4 precios por producto/sede.
6. El cálculo del carrito usa reglas de umbral y tipo de compra.
7. Para `precio4`, el sistema muestra estado `negociable` y no cierra automáticamente si falta acuerdo.
8. Admin puede configurar `tipoMedidaBase`, `tipoComercialMayor` y umbrales de caja/bulto por producto.
9. Producto por peso (ej: queso) se muestra y vende por peso en detal, y por pieza en mayor cuando corresponda.
10. Admin puede crear combo con `precioTotal` calculado y `precioPromocional` editable.
11. Solo combos publicados son visibles para clientes.
12. Con carrito abierto, el sistema rechaza cualquier intento de agregar ítems de una sede diferente.
13. La estrategia de 4 niveles se aplica en este orden: `precio1` (unidad/peso), `precio2` (1 caja/bulto/pieza), `precio3` (>1 y <=50 caja/bulto/pieza), `precio4` (>50 caja/bulto/pieza).

## Casos borde

- Producto sin precio configurado para una sede: marcar como no disponible para compra en esa sede.
- Cambio de sede con carrito cargado: solicitar confirmación y limpiar/recalcular carrito según política de UX.
- Producto con solo precio1 y precio2: reglas 3 y 4 no aplican.
- Producto por unidad sin umbrales configurados: bloquear publicación hasta completar configuración.
- Combo con producto no disponible en la sede activa: marcar combo no comprable en esa sede.
- Combo con `precioPromocional` mayor a `precioTotal`: advertir y requerir confirmación explícita en admin.

## No funcionales

- Mostrar origen del precio en UI para evitar confusión.
- Trazabilidad de cambios de precios en admin por sede.
- Preparar estructura para extender reglas de pricing futuras (promos, temporadas, cliente frecuente).
