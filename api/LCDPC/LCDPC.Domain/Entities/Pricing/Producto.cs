using LCDPC.Domain.Common;
using LCDPC.Domain.ValueObjects;

namespace LCDPC.Domain.Entities.Pricing;

public class Producto
{
    public Guid ProductoId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public TipoMedidaBase TipoMedidaBase { get; private set; }
    public TipoComercialMayor TipoComercialMayor { get; private set; }
    public int? UnidadesPorCaja { get; private set; }
    public int? UnidadesPorBulto { get; private set; }
    public bool Activo { get; private set; }

    private Producto()
    {
    }

    public static Producto Create(
        Guid productoId,
        string nombre,
        string sku,
        TipoMedidaBase tipoMedidaBase,
        TipoComercialMayor tipoComercialMayor,
        int? unidadesPorCaja,
        int? unidadesPorBulto,
        bool activo)
    {
        if (productoId == Guid.Empty)
        {
            throw new ArgumentException("productoId is required", nameof(productoId));
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException("nombre is required", nameof(nombre));
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("sku is required", nameof(sku));
        }

        if (tipoMedidaBase == TipoMedidaBase.Unidad)
        {
            if (!unidadesPorCaja.HasValue || unidadesPorCaja <= 0)
            {
                throw new ArgumentException("unidadesPorCaja is required for unit-based products", nameof(unidadesPorCaja));
            }

            if (!unidadesPorBulto.HasValue || unidadesPorBulto <= 0)
            {
                throw new ArgumentException("unidadesPorBulto is required for unit-based products", nameof(unidadesPorBulto));
            }

            if (unidadesPorBulto < unidadesPorCaja)
            {
                throw new ArgumentException("unidadesPorBulto must be greater than or equal to unidadesPorCaja", nameof(unidadesPorBulto));
            }
        }

        if (tipoMedidaBase is TipoMedidaBase.Gramos or TipoMedidaBase.Kilo)
        {
            if (unidadesPorCaja.HasValue || unidadesPorBulto.HasValue)
            {
                throw new ArgumentException("weight-based products do not use box/bulk thresholds");
            }
        }

        return new Producto
        {
            ProductoId = productoId,
            Nombre = nombre.Trim(),
            Sku = sku.Trim().ToUpperInvariant(),
            TipoMedidaBase = tipoMedidaBase,
            TipoComercialMayor = tipoComercialMayor,
            UnidadesPorCaja = unidadesPorCaja,
            UnidadesPorBulto = unidadesPorBulto,
            Activo = activo
        };
    }

    public void Actualizar(
        string nombre,
        string sku,
        TipoMedidaBase tipoMedidaBase,
        TipoComercialMayor tipoComercialMayor,
        int? unidadesPorCaja,
        int? unidadesPorBulto)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException("nombre is required", nameof(nombre));
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("sku is required", nameof(sku));
        }

        if (tipoMedidaBase == TipoMedidaBase.Unidad)
        {
            if (!unidadesPorCaja.HasValue || unidadesPorCaja <= 0)
            {
                throw new ArgumentException("unidadesPorCaja is required for unit-based products", nameof(unidadesPorCaja));
            }

            if (!unidadesPorBulto.HasValue || unidadesPorBulto <= 0)
            {
                throw new ArgumentException("unidadesPorBulto is required for unit-based products", nameof(unidadesPorBulto));
            }

            if (unidadesPorBulto < unidadesPorCaja)
            {
                throw new ArgumentException("unidadesPorBulto must be greater than or equal to unidadesPorCaja", nameof(unidadesPorBulto));
            }
        }

        if (tipoMedidaBase is TipoMedidaBase.Gramos or TipoMedidaBase.Kilo)
        {
            if (unidadesPorCaja.HasValue || unidadesPorBulto.HasValue)
            {
                throw new ArgumentException("weight-based products do not use box/bulk thresholds");
            }
        }

        Nombre = nombre.Trim();
        Sku = sku.Trim().ToUpperInvariant();
        TipoMedidaBase = tipoMedidaBase;
        TipoComercialMayor = tipoComercialMayor;
        UnidadesPorCaja = unidadesPorCaja;
        UnidadesPorBulto = unidadesPorBulto;
    }

    public void Desactivar() => Activo = false;

    public void Activar() => Activo = true;
}