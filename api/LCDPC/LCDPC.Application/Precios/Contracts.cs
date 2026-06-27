namespace LCDPC.Application.Precios;

public sealed record CreatePrecioRequest(
    Guid ProductoId,
    Guid SedeId,
    decimal Precio1Unidad,
    decimal Precio2CajaBultoPieza,
    decimal Precio3MayorDesde2,
    decimal? Precio4MayoristaNegociable,
    bool Precio4RequiereAcuerdo,
    DateTime VigenteDesde,
    DateTime? VigenteHasta);

public sealed record UpdatePrecioRequest(
    decimal Precio1Unidad,
    decimal Precio2CajaBultoPieza,
    decimal Precio3MayorDesde2,
    decimal? Precio4MayoristaNegociable,
    bool Precio4RequiereAcuerdo,
    DateTime VigenteDesde,
    DateTime? VigenteHasta);

public sealed record PrecioResponse(
    Guid PrecioProductoSedeId,
    Guid ProductoId,
    Guid SedeId,
    decimal Precio1Unidad,
    string Moneda,
    decimal Precio2CajaBultoPieza,
    decimal Precio3MayorDesde2,
    decimal? Precio4MayoristaNegociable,
    bool Precio4RequiereAcuerdo,
    DateTime VigenteDesde,
    DateTime? VigenteHasta);

public interface IPrecioProductoSedeService
{
    Task<PrecioResponse> CreateAsync(CreatePrecioRequest request, CancellationToken ct = default);
    Task<PrecioResponse?> GetByIdAsync(Guid precioId, CancellationToken ct = default);
    Task<IReadOnlyList<PrecioResponse>> ListByProductoAsync(Guid productoId, CancellationToken ct = default);
    Task<IReadOnlyList<PrecioResponse>> ListBySedeAsync(Guid sedeId, CancellationToken ct = default);
    Task<PrecioResponse> UpdateAsync(Guid precioId, UpdatePrecioRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid precioId, CancellationToken ct = default);
}
