# Tasks: Pricing por sede + Mayor/Detal

## Fase 1 - Dominio

- [x] Definir entidades y value objects: `Producto`, `Sede`, `PriceTier`, `Carrito`, `Orden`, `LineaCarrito`.
- [x] Definir invariantes:
  - [x] Carrito con sede única.
  - [x] Orden con sede obligatoria.
  - [x] Precio aplicado por sede + unidad comercial + cantidad.
- [x] Definir tabla de aplicación de `precio1..precio4`.
- [x] Formalizar regla de 4 niveles de precio:
  - [x] `precio1`: unidad o peso.
  - [x] `precio2`: 1 caja/bulto/pieza.
  - [x] `precio3`: más de 1 y hasta 50 cajas/bultos/piezas.
  - [x] `precio4`: más de 50 cajas/bultos/piezas.
- [x] Definir atributos de producto para medidas y empaque:
  - [x] `tipoMedidaBase` (`unidad`, `gramos`, `kilo`).
  - [x] `tipoComercialMayor` (`pieza`, `caja`, `bulto`).
  - [x] `unidadesPorCaja` y `unidadesPorBulto` cuando aplique.
- [x] Definir reglas para productos por peso (detal por peso, mayor por pieza).

## Fase 2 - Front cliente

- [ ] Catálogo: mostrar `~25$` cuando no hay sede.
- [ ] Selector de sede global en flujo comercial.
- [ ] Actualizar tarjetas/listas con precio real por sede seleccionada.
- [ ] Bloquear acceso a carrito sin sede.
- [ ] Bloquear mezcla de sedes en carrito.
- [ ] Mostrar de forma persistente la sede activa durante todo el flujo comercial.

## Fase 3 - Checkout WhatsApp

- [ ] Confirmación de sede y datos de envío antes de enviar pedido.
- [ ] Generar payload/mensaje de WhatsApp con:
  - [ ] Sede.
  - [ ] Líneas con cantidades.
  - [ ] Precio aplicado por línea.
  - [ ] Total.

## Fase 4 - Admin

- [ ] CRUD de precios por producto/sede con 4 niveles.
- [ ] Validaciones de umbrales (2+ y >50).
- [ ] Estado negociable para `precio4`.
- [ ] Auditoría básica de cambios de precio.
- [ ] Configuración de medida/unidad comercial por producto.
- [ ] Validación de umbrales caja/bulto antes de publicar producto.

## Fase 5 - Admin Combos

- [ ] CRUD de combos (`borrador`, `publicado`, `pausado`).
- [ ] Constructor de combo con productos y cantidades.
- [ ] Cálculo automático de `precioTotal` del combo.
- [ ] Campo editable de `precioPromocional`.
- [ ] Reglas de publicación y visibilidad en catálogo.
- [ ] Validación de disponibilidad por sede para compra del combo.

## Fase 6 - QA

- [ ] Tests de reglas de pricing por cantidad.
- [ ] Tests de restricción de sede única.
- [ ] Tests de flujo de confirmación WhatsApp.
- [ ] Tests de guardado/edición de matriz de precios por sede.
- [ ] Tests de conversión unidad/caja/bulto y productos por peso.
- [ ] Tests de creación/publicación/compra de combos.
- [ ] Tests de rechazo al agregar productos de otra sede con carrito activo.
- [ ] Tests de cálculo por cada nivel de precio (1, 2, 3, 4).
