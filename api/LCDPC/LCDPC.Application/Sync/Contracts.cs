namespace LCDPC.Application.Sync;

public sealed record SyncProductoRequest(
    string Sku,
    string Nombre,
    int TipoMedidaBase,
    int TipoComercialMayor,
    int? UnidadesPorCaja,
    int? UnidadesPorBulto);

public sealed record SyncComboRequest(
    string Codigo,
    string Nombre,
    List<SyncComboItemRequest> Items,
    List<Guid>? SedeIdsHabilitadas);

public sealed record SyncComboItemRequest(Guid ProductoId, decimal Cantidad);

public sealed record SyncBatchResponse(
    int Creados,
    int Actualizados,
    int Errores,
    List<string> Detalles);

public interface ISyncService
{
    Task<SyncBatchResponse> SyncProductosAsync(List<SyncProductoRequest> productos, CancellationToken ct = default);
    Task<SyncBatchResponse> SyncCombosAsync(List<SyncComboRequest> combos, CancellationToken ct = default);
}
