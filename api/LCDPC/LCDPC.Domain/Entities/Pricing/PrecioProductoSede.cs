using LCDPC.Domain.Common;
using LCDPC.Domain.ValueObjects;

namespace LCDPC.Domain.Entities.Pricing;

public class PrecioProductoSede
{
    public Guid PrecioProductoSedeId { get; private set; }
    public Guid ProductoId { get; private set; }
    public Guid SedeId { get; private set; }
    public Money Precio1Unidad { get; private set; } = Money.Usd(0);
    public Money Precio2CajaBultoPieza { get; private set; } = Money.Usd(0);
    public Money Precio3MayorDesde2 { get; private set; } = Money.Usd(0);
    public Money? Precio4MayoristaNegociable { get; private set; }
    public bool Precio4RequiereAcuerdo { get; private set; }
    public DateTime VigenteDesde { get; private set; }
    public DateTime? VigenteHasta { get; private set; }

    private PrecioProductoSede()
    {
    }

    public static PrecioProductoSede Create(
        Guid precioProductoSedeId,
        Guid productoId,
        Guid sedeId,
        Money precio1Unidad,
        Money precio2CajaBultoPieza,
        Money precio3MayorDesde2,
        Money? precio4MayoristaNegociable,
        bool precio4RequiereAcuerdo,
        DateTime vigenteDesde,
        DateTime? vigenteHasta = null)
    {
        if (precioProductoSedeId == Guid.Empty)
        {
            throw new ArgumentException("precioProductoSedeId is required", nameof(precioProductoSedeId));
        }

        if (productoId == Guid.Empty)
        {
            throw new ArgumentException("productoId is required", nameof(productoId));
        }

        if (sedeId == Guid.Empty)
        {
            throw new ArgumentException("sedeId is required", nameof(sedeId));
        }

        if (vigenteHasta.HasValue && vigenteHasta <= vigenteDesde)
        {
            throw new ArgumentException("vigenteHasta must be greater than vigenteDesde", nameof(vigenteHasta));
        }

        return new PrecioProductoSede
        {
            PrecioProductoSedeId = precioProductoSedeId,
            ProductoId = productoId,
            SedeId = sedeId,
            Precio1Unidad = precio1Unidad,
            Precio2CajaBultoPieza = precio2CajaBultoPieza,
            Precio3MayorDesde2 = precio3MayorDesde2,
            Precio4MayoristaNegociable = precio4MayoristaNegociable,
            Precio4RequiereAcuerdo = precio4RequiereAcuerdo,
            VigenteDesde = vigenteDesde,
            VigenteHasta = vigenteHasta
        };
    }

    public Money? GetPrice(PriceTier tier)
    {
        return tier switch
        {
            PriceTier.Precio1 => Precio1Unidad,
            PriceTier.Precio2 => Precio2CajaBultoPieza,
            PriceTier.Precio3 => Precio3MayorDesde2,
            PriceTier.Precio4 => Precio4MayoristaNegociable,
            _ => null
        };
    }

    public void Actualizar(
        Money precio1Unidad,
        Money precio2CajaBultoPieza,
        Money precio3MayorDesde2,
        Money? precio4MayoristaNegociable,
        bool precio4RequiereAcuerdo,
        DateTime vigenteDesde,
        DateTime? vigenteHasta = null)
    {
        if (vigenteHasta.HasValue && vigenteHasta <= vigenteDesde)
        {
            throw new ArgumentException("vigenteHasta must be greater than vigenteDesde", nameof(vigenteHasta));
        }

        Precio1Unidad = precio1Unidad;
        Precio2CajaBultoPieza = precio2CajaBultoPieza;
        Precio3MayorDesde2 = precio3MayorDesde2;
        Precio4MayoristaNegociable = precio4MayoristaNegociable;
        Precio4RequiereAcuerdo = precio4RequiereAcuerdo;
        VigenteDesde = vigenteDesde;
        VigenteHasta = vigenteHasta;
    }
}