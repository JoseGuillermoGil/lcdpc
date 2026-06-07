using LCDPC.Domain.Common;
using LCDPC.Domain.ValueObjects;

namespace LCDPC.Domain.Entities.Pricing;

public class LineaCarrito
{
    public Guid LineaId { get; private set; }
    public Guid ProductoId { get; private set; }
    public decimal Cantidad { get; private set; }
    public UnidadCompra UnidadCompra { get; private set; }
    public Money PrecioAplicado { get; private set; } = Money.Usd(0);
    public OrigenPrecioLinea OrigenPrecio { get; private set; }

    private LineaCarrito()
    {
    }

    public static LineaCarrito Create(
        Guid lineaId,
        Guid productoId,
        decimal cantidad,
        UnidadCompra unidadCompra,
        Money precioAplicado,
        OrigenPrecioLinea origenPrecio)
    {
        if (lineaId == Guid.Empty)
        {
            throw new ArgumentException("lineaId is required", nameof(lineaId));
        }

        if (productoId == Guid.Empty)
        {
            throw new ArgumentException("productoId is required", nameof(productoId));
        }

        if (cantidad <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cantidad), "cantidad must be greater than zero");
        }

        return new LineaCarrito
        {
            LineaId = lineaId,
            ProductoId = productoId,
            Cantidad = cantidad,
            UnidadCompra = unidadCompra,
            PrecioAplicado = precioAplicado,
            OrigenPrecio = origenPrecio
        };
    }
}