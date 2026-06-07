using LCDPC.Domain.Common;
using LCDPC.Domain.ValueObjects;

namespace LCDPC.Domain.Entities.Pricing;

public class Combo
{
    public Guid ComboId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public EstadoCombo Estado { get; private set; }
    public List<ComboItem> Items { get; private set; } = [];
    public Money PrecioTotal { get; private set; } = Money.Usd(0);
    public Money? PrecioPromocional { get; private set; }
    public IReadOnlyCollection<Guid> SedeIdsHabilitadas => sedeIdsHabilitadas;

    private readonly List<Guid> sedeIdsHabilitadas = [];

    private Combo()
    {
    }

    public static Combo Create(Guid comboId, string nombre, IEnumerable<ComboItem> items, IEnumerable<Guid>? sedeIdsHabilitadas = null)
    {
        if (comboId == Guid.Empty)
        {
            throw new ArgumentException("comboId is required", nameof(comboId));
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException("nombre is required", nameof(nombre));
        }

        var combo = new Combo
        {
            ComboId = comboId,
            Nombre = nombre.Trim(),
            Estado = EstadoCombo.Borrador
        };

        combo.Items.AddRange(items ?? throw new ArgumentNullException(nameof(items)));

        if (sedeIdsHabilitadas is not null)
        {
            combo.sedeIdsHabilitadas.AddRange(sedeIdsHabilitadas.Where(x => x != Guid.Empty));
        }

        return combo;
    }

    public void RecalcularPrecioTotal(Func<ComboItem, Money> priceResolver)
    {
        if (priceResolver is null)
        {
            throw new ArgumentNullException(nameof(priceResolver));
        }

        var total = Money.Usd(0);
        foreach (var item in Items)
        {
            total = total.Add(priceResolver(item).Multiply(item.Cantidad));
        }

        PrecioTotal = total;
    }

    public void Publicar()
    {
        Estado = EstadoCombo.Publicado;
    }

    public void Pausar()
    {
        Estado = EstadoCombo.Pausado;
    }

    public void ActualizarPrecioPromocional(Money? precioPromocional, bool requiereConfirmacionExplicita = false)
    {
        if (precioPromocional is null)
        {
            PrecioPromocional = null;
            return;
        }

        if (precioPromocional > PrecioTotal && !requiereConfirmacionExplicita)
        {
            throw new InvalidOperationException("promo_price_greater_than_total_requires_explicit_confirmation");
        }

        PrecioPromocional = precioPromocional;
    }

    public bool EsVisibleEnCatalogo() => Estado == EstadoCombo.Publicado;
}