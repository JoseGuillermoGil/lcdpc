using LCDPC.Domain.Common;
using LCDPC.Domain.Entities.Pricing;
using LCDPC.Domain.Services;
using LCDPC.Domain.ValueObjects;

namespace LCDPC.Architecture.Tests;

public class PricingDomainTests
{
    [Fact]
    public void ProductoCreate_RequiresBoxAndBulkThresholds_ForUnitBasedProducts()
    {
        var ex = Assert.Throws<ArgumentException>(() => Producto.Create(
            Guid.NewGuid(),
            "Arroz",
            "ARZ-001",
            TipoMedidaBase.Unidad,
            TipoComercialMayor.Pieza,
            null,
            null,
            true));

        Assert.Contains("unidadesPorCaja", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnidadComercialResolver_ClassifiesByThresholds()
    {
        var producto = Producto.Create(
            Guid.NewGuid(),
            "Arroz",
            "ARZ-001",
            TipoMedidaBase.Unidad,
            TipoComercialMayor.Pieza,
            12,
            60,
            true);

        Assert.Equal(UnidadCompra.Unidad, UnidadComercialResolver.ResolveForUnitBasedProduct(producto, 1));
        Assert.Equal(UnidadCompra.Caja, UnidadComercialResolver.ResolveForUnitBasedProduct(producto, 12));
        Assert.Equal(UnidadCompra.Bulto, UnidadComercialResolver.ResolveForUnitBasedProduct(producto, 60));
    }

    [Fact]
    public void PricingService_ResolvesExpectedTierByUnidadCompraAndCantidad()
    {
        Assert.Equal(PriceTier.Precio1, PricingService.ResolvePriceTier(UnidadCompra.Unidad, 1));
        Assert.Equal(PriceTier.Precio2, PricingService.ResolvePriceTier(UnidadCompra.Caja, 1));
        Assert.Equal(PriceTier.Precio3, PricingService.ResolvePriceTier(UnidadCompra.Pieza, 2));
        Assert.Equal(PriceTier.Precio4, PricingService.ResolvePriceTier(UnidadCompra.Bulto, 51));
        Assert.Equal(PriceTier.Precio1, PricingService.ResolvePriceTier(UnidadCompra.Kilo, 3));
    }

    [Fact]
    public void CarritoCreate_RequiresSedeAndClearsLinesWhenChangingSede()
    {
        var carrito = Carrito.Create(Guid.NewGuid(), Guid.NewGuid());
        carrito.AgregarLinea(LineaCarrito.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            2,
            UnidadCompra.Unidad,
            Money.Usd(10),
            OrigenPrecioLinea.Precio1));

        carrito.CambiarSede(Guid.NewGuid());

        Assert.Empty(carrito.Lineas);
    }

    [Fact]
    public void ComboPricingService_CalculatesTotalAndRejectsPromoAboveTotal()
    {
        var items = new[]
        {
            ComboItem.Create(Guid.NewGuid(), 2),
            ComboItem.Create(Guid.NewGuid(), 3)
        };

        var total = ComboPricingService.CalculateTotal(items, _ => Money.Usd(10));

        Assert.Equal(50m, total.Amount);
        Assert.Equal("USD", total.Currency);

        var ex = Assert.Throws<InvalidOperationException>(() => ComboPricingService.ValidatePromotionalPrice(
            total,
            Money.Usd(60)));

        Assert.Equal("promo_price_greater_than_total_requires_explicit_confirmation", ex.Message);
    }

    [Fact]
    public void MoneyRejectsNegativeAmounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.Usd(-1));
    }
}
