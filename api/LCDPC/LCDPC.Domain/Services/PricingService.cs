using LCDPC.Domain.Common;
using LCDPC.Domain.Entities.Pricing;
using LCDPC.Domain.ValueObjects;

namespace LCDPC.Domain.Services;

public static class PricingService
{
    public static PriceTier ResolvePriceTier(UnidadCompra unidadCompra, decimal cantidad)
    {
        if (cantidad <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cantidad), "cantidad must be greater than zero");
        }

        if (unidadCompra is UnidadCompra.Unidad or UnidadCompra.Gramos or UnidadCompra.Kilo)
        {
            return PriceTier.Precio1;
        }

        if (cantidad == 1)
        {
            return PriceTier.Precio2;
        }

        if (cantidad <= 50)
        {
            return PriceTier.Precio3;
        }

        return PriceTier.Precio4;
    }

    public static Money? ResolvePrice(PrecioProductoSede precios, UnidadCompra unidadCompra, decimal cantidad)
    {
        ArgumentNullException.ThrowIfNull(precios);

        var tier = ResolvePriceTier(unidadCompra, cantidad);
        return precios.GetPrice(tier);
    }
}