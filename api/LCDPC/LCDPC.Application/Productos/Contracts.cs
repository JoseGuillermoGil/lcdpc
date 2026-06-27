namespace LCDPC.Application.Productos;

public sealed record CreateProductoRequest(
    string Nombre,
    string Sku,
    int TipoMedidaBase,
    int TipoComercialMayor,
    int? UnidadesPorCaja,
    int? UnidadesPorBulto);

public sealed record UpdateProductoRequest(
    string Nombre,
    string Sku,
    int TipoMedidaBase,
    int TipoComercialMayor,
    int? UnidadesPorCaja,
    int? UnidadesPorBulto);

public sealed record ProductoResponse(
    Guid ProductoId,
    string Nombre,
    string Sku,
    string TipoMedidaBase,
    string TipoComercialMayor,
    int? UnidadesPorCaja,
    int? UnidadesPorBulto,
    bool Activo);

public interface IProductoService
{
    Task<ProductoResponse> CreateAsync(CreateProductoRequest request, CancellationToken ct = default);
    Task<ProductoResponse?> GetByIdAsync(Guid productoId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductoResponse>> ListAsync(CancellationToken ct = default);
    Task<ProductoResponse> UpdateAsync(Guid productoId, UpdateProductoRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid productoId, CancellationToken ct = default);
}
