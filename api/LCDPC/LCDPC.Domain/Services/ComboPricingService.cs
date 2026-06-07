using LCDPC.Domain.Entities.Pricing;
using LCDPC.Domain.ValueObjects;

namespace LCDPC.Domain.Services;

public static class ComboPricingService
{
    public static Money CalculateTotal(IEnumerable<ComboItem> items, Func<Guid, Money> unitPriceResolver)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(unitPriceResolver);

        var total = Money.Usd(0);
        foreach (var item in items)
        {
            total = total.Add(unitPriceResolver(item.ProductoId).Multiply(item.Cantidad));
        }

        return total;
    }

    public static void ValidatePromotionalPrice(Money precioTotal, Money? precioPromocional, bool requiereConfirmacionExplicita = false)
    {
        if (precioPromocional is null)
        {
            return;
        }

        if (precioPromocional > precioTotal && !requiereConfirmacionExplicita)
        {
            throw new InvalidOperationException("promo_price_greater_than_total_requires_explicit_confirmation");
        }
    }
}