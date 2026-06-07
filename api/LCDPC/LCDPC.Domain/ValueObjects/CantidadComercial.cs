using LCDPC.Domain.Common;

namespace LCDPC.Domain.ValueObjects;

public sealed record CantidadComercial
{
    public decimal Cantidad { get; }
    public UnidadCompra UnidadCompra { get; }

    private CantidadComercial(decimal cantidad, UnidadCompra unidadCompra)
    {
        if (cantidad <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cantidad), "cantidad must be greater than zero");
        }

        Cantidad = cantidad;
        UnidadCompra = unidadCompra;
    }

    public static CantidadComercial Create(decimal cantidad, UnidadCompra unidadCompra) => new(cantidad, unidadCompra);
}