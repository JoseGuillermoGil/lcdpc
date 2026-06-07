using LCDPC.Domain.Common;

namespace LCDPC.Domain.Entities.Pricing;

public class Carrito
{
    public Guid CarritoId { get; private set; }
    public Guid SedeId { get; private set; }
    public EstadoCarrito Estado { get; private set; }
    public List<LineaCarrito> Lineas { get; private set; } = [];

    private Carrito()
    {
    }

    public static Carrito Create(Guid carritoId, Guid sedeId)
    {
        if (carritoId == Guid.Empty)
        {
            throw new ArgumentException("carritoId is required", nameof(carritoId));
        }

        if (sedeId == Guid.Empty)
        {
            throw new ArgumentException("sedeId is required", nameof(sedeId));
        }

        return new Carrito
        {
            CarritoId = carritoId,
            SedeId = sedeId,
            Estado = EstadoCarrito.Abierto
        };
    }

    public void AgregarLinea(LineaCarrito linea)
    {
        if (linea is null)
        {
            throw new ArgumentNullException(nameof(linea));
        }

        if (Estado != EstadoCarrito.Abierto)
        {
            throw new InvalidOperationException("cart is not open");
        }

        Lineas.Add(linea);
    }

    public void CambiarSede(Guid nuevaSedeId)
    {
        if (nuevaSedeId == Guid.Empty)
        {
            throw new ArgumentException("nuevaSedeId is required", nameof(nuevaSedeId));
        }

        if (SedeId == nuevaSedeId)
        {
            return;
        }

        SedeId = nuevaSedeId;
        Lineas.Clear();
    }

    public void Confirmar()
    {
        if (Lineas.Count == 0)
        {
            throw new InvalidOperationException("cart cannot be confirmed without lines");
        }

        Estado = EstadoCarrito.Confirmado;
    }

    public void Cancelar()
    {
        Estado = EstadoCarrito.Cancelado;
    }
}