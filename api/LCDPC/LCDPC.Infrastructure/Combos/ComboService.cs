using LCDPC.Application.Combos;
using LCDPC.Domain.Common;
using LCDPC.Domain.Entities.Pricing;
using LCDPC.Domain.ValueObjects;
using LCDPC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.Infrastructure.Combos;

public class ComboService(AppDbContext db) : IComboService
{
    public async Task<ComboResponse> CreateAsync(CreateComboRequest request, CancellationToken ct = default)
    {
        var items = request.Items.Select(i => ComboItem.Create(i.ProductoId, i.Cantidad));

        var combo = Combo.Create(
            Guid.NewGuid(),
            request.Codigo,
            request.Nombre,
            items,
            request.SedeIdsHabilitadas);

        db.Combos.Add(combo);
        await db.SaveChangesAsync(ct);

        return Map(combo);
    }

    public async Task<ComboResponse?> GetByIdAsync(Guid comboId, CancellationToken ct = default)
    {
        var combo = await db.Combos
            .Include(c => c.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ComboId == comboId, ct);

        return combo is null ? null : Map(combo);
    }

    public async Task<IReadOnlyList<ComboResponse>> ListAsync(CancellationToken ct = default)
    {
        var combos = await db.Combos
            .Include(c => c.Items)
            .AsNoTracking()
            .OrderBy(x => x.Nombre)
            .ToListAsync(ct);

        return combos.Select(Map).ToList();
    }

    public async Task<ComboResponse> UpdateAsync(Guid comboId, UpdateComboRequest request, CancellationToken ct = default)
    {
        var combo = await db.Combos
            .Include(c => c.Items)
            .FirstOrDefaultAsync(x => x.ComboId == comboId, ct)
            ?? throw new InvalidOperationException("COMBO_NOT_FOUND");

        combo.ActualizarNombre(request.Nombre);

        var items = request.Items.Select(i => ComboItem.Create(i.ProductoId, i.Cantidad)).ToList();
        combo.ActualizarItems(items);

        combo.ActualizarSedes(request.SedeIdsHabilitadas);

        await db.SaveChangesAsync(ct);
        return Map(combo);
    }

    public async Task DeleteAsync(Guid comboId, CancellationToken ct = default)
    {
        var combo = await db.Combos.FirstOrDefaultAsync(x => x.ComboId == comboId, ct)
            ?? throw new InvalidOperationException("COMBO_NOT_FOUND");

        db.Combos.Remove(combo);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ComboResponse> PublicarAsync(Guid comboId, CancellationToken ct = default)
    {
        var combo = await db.Combos
            .Include(c => c.Items)
            .FirstOrDefaultAsync(x => x.ComboId == comboId, ct)
            ?? throw new InvalidOperationException("COMBO_NOT_FOUND");

        combo.Publicar();
        await db.SaveChangesAsync(ct);
        return Map(combo);
    }

    public async Task<ComboResponse> PausarAsync(Guid comboId, CancellationToken ct = default)
    {
        var combo = await db.Combos
            .Include(c => c.Items)
            .FirstOrDefaultAsync(x => x.ComboId == comboId, ct)
            ?? throw new InvalidOperationException("COMBO_NOT_FOUND");

        combo.Pausar();
        await db.SaveChangesAsync(ct);
        return Map(combo);
    }

    private static ComboResponse Map(Combo c) => new(
        c.ComboId,
        c.Codigo,
        c.Nombre,
        c.Estado.ToString(),
        c.Items.Select(i => new ComboItemResponse(i.ProductoId, i.Cantidad)).ToList(),
        c.PrecioTotal.Amount,
        c.PrecioTotal.Currency,
        c.PrecioPromocional?.Amount,
        c.PrecioPromocional?.Currency,
        c.SedeIdsHabilitadas.ToList());
}
