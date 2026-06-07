namespace LCDPC.Domain.ValueObjects;

public sealed record UmbralEmpaque
{
    public int UnidadesPorCaja { get; }
    public int UnidadesPorBulto { get; }

    private UmbralEmpaque(int unidadesPorCaja, int unidadesPorBulto)
    {
        if (unidadesPorCaja <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unidadesPorCaja), "unidadesPorCaja must be greater than zero");
        }

        if (unidadesPorBulto <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unidadesPorBulto), "unidadesPorBulto must be greater than zero");
        }

        if (unidadesPorBulto < unidadesPorCaja)
        {
            throw new ArgumentException("unidadesPorBulto must be greater than or equal to unidadesPorCaja", nameof(unidadesPorBulto));
        }

        UnidadesPorCaja = unidadesPorCaja;
        UnidadesPorBulto = unidadesPorBulto;
    }

    public static UmbralEmpaque Create(int unidadesPorCaja, int unidadesPorBulto) => new(unidadesPorCaja, unidadesPorBulto);
}