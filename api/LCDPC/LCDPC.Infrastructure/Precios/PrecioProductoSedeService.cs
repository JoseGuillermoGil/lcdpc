using LCDPC.Application.Precios;
using LCDPC.Domain.Entities.Pricing;
using LCDPC.Domain.ValueObjects;
using LCDPC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.Infrastructure.Precios;

public class PrecioProductoSedeService(AppDbContext db) : IPrecioProductoSedeService
{
    public async Task<PrecioResponse> CreateAsync(CreatePrecioRequest request, CancellationToken ct = default)
    {
        var exists = await db.PreciosProductoSede.AsNoTracking()
            .AnyAsync(x => x.ProductoId == request.ProductoId && x.SedeId == request.SedeId && x.VigenteHasta == null, ct);
        if (exists)
        {
            throw new InvalidOperationException("ACTIVE_PRICE_ALREADY_EXISTS");
        }

        var precio = PrecioProductoSede.Create(
            Guid.NewGuid(),
            request.ProductoId,
            request.SedeId,
            Money.Usd(request.Precio1Unidad),
            Money.Usd(request.Precio2CajaBultoPieza),
            Money.Usd(request.Precio3MayorDesde2),
            request.Precio4MayoristaNegociable.HasValue ? Money.Usd(request.Precio4MayoristaNegociable.Value) : null,
            request.Precio4RequiereAcuerdo,
            request.VigenteDesde,
            request.VigenteHasta);

        db.PreciosProductoSede.Add(precio);
        await db.SaveChangesAsync(ct);

        return Map(precio);
    }

    public async Task<PrecioResponse?> GetByIdAsync(Guid precioId, CancellationToken ct = default)
    {
        var precio = await db.PreciosProductoSede.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PrecioProductoSedeId == precioId, ct);

        return precio is null ? null : Map(precio);
    }

    public async Task<IReadOnlyList<PrecioResponse>> ListByProductoAsync(Guid productoId, CancellationToken ct = default)
    {
        return await db.PreciosProductoSede
            .AsNoTracking()
            .Where(x => x.ProductoId == productoId)
            .OrderBy(x => x.VigenteDesde)
            .Select(x => Map(x))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PrecioResponse>> ListBySedeAsync(Guid sedeId, CancellationToken ct = default)
    {
        return await db.PreciosProductoSede
            .AsNoTracking()
            .Where(x => x.SedeId == sedeId)
            .OrderBy(x => x.VigenteDesde)
            .Select(x => Map(x))
            .ToListAsync(ct);
    }

    public async Task<PrecioResponse> UpdateAsync(Guid precioId, UpdatePrecioRequest request, CancellationToken ct = default)
    {
        var precio = await db.PreciosProductoSede
            .FirstOrDefaultAsync(x => x.PrecioProductoSedeId == precioId, ct)
            ?? throw new InvalidOperationException("PRICE_NOT_FOUND");

        precio.Actualizar(
            Money.Usd(request.Precio1Unidad),
            Money.Usd(request.Precio2CajaBultoPieza),
            Money.Usd(request.Precio3MayorDesde2),
            request.Precio4MayoristaNegociable.HasValue ? Money.Usd(request.Precio4MayoristaNegociable.Value) : null,
            request.Precio4RequiereAcuerdo,
            request.VigenteDesde,
            request.VigenteHasta);

        await db.SaveChangesAsync(ct);
        return Map(precio);
    }

    public async Task DeleteAsync(Guid precioId, CancellationToken ct = default)
    {
        var precio = await db.PreciosProductoSede
            .FirstOrDefaultAsync(x => x.PrecioProductoSedeId == precioId, ct)
            ?? throw new InvalidOperationException("PRICE_NOT_FOUND");

        db.PreciosProductoSede.Remove(precio);
        await db.SaveChangesAsync(ct);
    }

    private static PrecioResponse Map(PrecioProductoSede p) => new(
        p.PrecioProductoSedeId,
        p.ProductoId,
        p.SedeId,
        p.Precio1Unidad.Amount,
        p.Precio1Unidad.Currency,
        p.Precio2CajaBultoPieza.Amount,
        p.Precio3MayorDesde2.Amount,
        p.Precio4MayoristaNegociable?.Amount,
        p.Precio4RequiereAcuerdo,
        p.VigenteDesde,
        p.VigenteHasta);
}
