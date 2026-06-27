using LCDPC.Application.Sync;
using LCDPC.Domain.Common;
using LCDPC.Domain.Entities.Pricing;
using LCDPC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.Infrastructure.Sync;

public class SyncService(AppDbContext db) : ISyncService
{
    public async Task<SyncBatchResponse> SyncProductosAsync(List<SyncProductoRequest> productos, CancellationToken ct = default)
    {
        var creados = 0;
        var actualizados = 0;
        var errores = 0;
        var detalles = new List<string>();

        foreach (var request in productos)
        {
            try
            {
                var sku = request.Sku.Trim().ToUpperInvariant();
                var existente = await db.Productos.FirstOrDefaultAsync(x => x.Sku == sku, ct);

                if (existente is not null)
                {
                    existente.Actualizar(
                        request.Nombre,
                        request.Sku,
                        (TipoMedidaBase)request.TipoMedidaBase,
                        (TipoComercialMayor)request.TipoComercialMayor,
                        request.UnidadesPorCaja,
                        request.UnidadesPorBulto);
                    actualizados++;
                    detalles.Add($"Producto {sku}: actualizado");
                }
                else
                {
                    var producto = Producto.Create(
                        Guid.NewGuid(),
                        request.Nombre,
                        request.Sku,
                        (TipoMedidaBase)request.TipoMedidaBase,
                        (TipoComercialMayor)request.TipoComercialMayor,
                        request.UnidadesPorCaja,
                        request.UnidadesPorBulto,
                        activo: true);
                    db.Productos.Add(producto);
                    creados++;
                    detalles.Add($"Producto {sku}: creado");
                }
            }
            catch (Exception ex)
            {
                errores++;
                detalles.Add($"Producto {request.Sku}: error - {ex.Message}");
            }
        }

        await db.SaveChangesAsync(ct);

        return new SyncBatchResponse(creados, actualizados, errores, detalles);
    }

    public async Task<SyncBatchResponse> SyncCombosAsync(List<SyncComboRequest> combos, CancellationToken ct = default)
    {
        var creados = 0;
        var actualizados = 0;
        var errores = 0;
        var detalles = new List<string>();

        foreach (var request in combos)
        {
            try
            {
                var codigo = request.Codigo.Trim().ToUpperInvariant();
                var existente = await db.Combos
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(x => x.Codigo == codigo, ct);

                if (existente is not null)
                {
                    existente.ActualizarNombre(request.Nombre);

                    var items = request.Items.Select(i => ComboItem.Create(i.ProductoId, i.Cantidad)).ToList();
                    existente.ActualizarItems(items);

                    existente.ActualizarSedes(request.SedeIdsHabilitadas);

                    actualizados++;
                    detalles.Add($"Combo {codigo}: actualizado");
                }
                else
                {
                    var items = request.Items.Select(i => ComboItem.Create(i.ProductoId, i.Cantidad));
                    var combo = Combo.Create(
                        Guid.NewGuid(),
                        request.Codigo,
                        request.Nombre,
                        items,
                        request.SedeIdsHabilitadas);
                    db.Combos.Add(combo);
                    creados++;
                    detalles.Add($"Combo {codigo}: creado");
                }
            }
            catch (Exception ex)
            {
                errores++;
                detalles.Add($"Combo {request.Codigo}: error - {ex.Message}");
            }
        }

        await db.SaveChangesAsync(ct);

        return new SyncBatchResponse(creados, actualizados, errores, detalles);
    }
}
