namespace LCDPC.Domain.Entities.Pricing;

public class ComboItem
{
    public Guid ProductoId { get; private set; }
    public decimal Cantidad { get; private set; }

    private ComboItem()
    {
    }

    public static ComboItem Create(Guid productoId, decimal cantidad)
    {
        if (productoId == Guid.Empty)
        {
            throw new ArgumentException("productoId is required", nameof(productoId));
        }

        if (cantidad <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cantidad), "cantidad must be greater than zero");
        }

        return new ComboItem
        {
            ProductoId = productoId,
            Cantidad = cantidad
        };
    }
}