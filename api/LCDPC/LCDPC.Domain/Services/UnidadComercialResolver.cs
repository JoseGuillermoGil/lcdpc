using LCDPC.Domain.Common;
using LCDPC.Domain.Entities.Pricing;

namespace LCDPC.Domain.Services;

public static class UnidadComercialResolver
{
    public static UnidadCompra ResolveForUnitBasedProduct(Producto producto, decimal cantidad)
    {
        ArgumentNullException.ThrowIfNull(producto);

        if (producto.TipoMedidaBase != TipoMedidaBase.Unidad)
        {
            throw new InvalidOperationException("resolver only applies to unit-based products");
        }

        if (cantidad <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cantidad), "cantidad must be greater than zero");
        }

        if (producto.UnidadesPorBulto.HasValue && cantidad >= producto.UnidadesPorBulto.Value)
        {
            return UnidadCompra.Bulto;
        }

        if (producto.UnidadesPorCaja.HasValue && cantidad >= producto.UnidadesPorCaja.Value)
        {
            return UnidadCompra.Caja;
        }

        return UnidadCompra.Unidad;
    }
}