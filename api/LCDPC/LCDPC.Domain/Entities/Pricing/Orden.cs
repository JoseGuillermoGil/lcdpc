using LCDPC.Domain.Common;

namespace LCDPC.Domain.Entities.Pricing;

public class Orden
{
    public Guid OrdenId { get; private set; }
    public Guid SedeId { get; private set; }
    public EstadoOrden Estado { get; private set; }
    public List<LineaCarrito> Lineas { get; private set; } = [];

    private Orden()
    {
    }

    public static Orden Create(Guid ordenId, Guid sedeId, IEnumerable<LineaCarrito> lineas)
    {
        if (ordenId == Guid.Empty)
        {
            throw new ArgumentException("ordenId is required", nameof(ordenId));
        }

        if (sedeId == Guid.Empty)
        {
            throw new ArgumentException("sedeId is required", nameof(sedeId));
        }

        var order = new Orden
        {
            OrdenId = ordenId,
            SedeId = sedeId,
            Estado = EstadoOrden.Borrador
        };

        order.Lineas.AddRange(lineas ?? throw new ArgumentNullException(nameof(lineas)));
        return order;
    }

    public void Confirmar()
    {
        if (Lineas.Count == 0)
        {
            throw new InvalidOperationException("order cannot be confirmed without lines");
        }

        Estado = EstadoOrden.Confirmada;
    }
}