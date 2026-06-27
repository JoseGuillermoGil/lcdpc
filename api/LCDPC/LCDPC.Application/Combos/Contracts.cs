namespace LCDPC.Application.Combos;

public sealed record ComboItemRequest(Guid ProductoId, decimal Cantidad);

public sealed record CreateComboRequest(
    string Codigo,
    string Nombre,
    List<ComboItemRequest> Items,
    List<Guid>? SedeIdsHabilitadas);

public sealed record UpdateComboRequest(
    string Nombre,
    List<ComboItemRequest> Items,
    List<Guid>? SedeIdsHabilitadas);

public sealed record ComboItemResponse(Guid ProductoId, decimal Cantidad);

public sealed record ComboResponse(
    Guid ComboId,
    string Codigo,
    string Nombre,
    string Estado,
    List<ComboItemResponse> Items,
    decimal PrecioTotal,
    string PrecioTotalMoneda,
    decimal? PrecioPromocional,
    string? PrecioPromocionalMoneda,
    List<Guid> SedeIdsHabilitadas);

public interface IComboService
{
    Task<ComboResponse> CreateAsync(CreateComboRequest request, CancellationToken ct = default);
    Task<ComboResponse?> GetByIdAsync(Guid comboId, CancellationToken ct = default);
    Task<IReadOnlyList<ComboResponse>> ListAsync(CancellationToken ct = default);
    Task<ComboResponse> UpdateAsync(Guid comboId, UpdateComboRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid comboId, CancellationToken ct = default);
    Task<ComboResponse> PublicarAsync(Guid comboId, CancellationToken ct = default);
    Task<ComboResponse> PausarAsync(Guid comboId, CancellationToken ct = default);
}
