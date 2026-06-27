using LCDPC.Application.Productos;
using LCDPC.Domain.Common;
using LCDPC.Domain.Entities.Pricing;
using LCDPC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.Infrastructure.Productos;

public class ProductoService(AppDbContext db) : IProductoService
{
    public async Task<ProductoResponse> CreateAsync(CreateProductoRequest request, CancellationToken ct = default)
    {
        var exists = await db.Productos.AsNoTracking().AnyAsync(x => x.Sku == request.Sku.Trim().ToUpperInvariant(), ct);
        if (exists)
        {
            throw new InvalidOperationException("SKU_ALREADY_EXISTS");
        }

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
        await db.SaveChangesAsync(ct);

        return Map(producto);
    }

    public async Task<ProductoResponse?> GetByIdAsync(Guid productoId, CancellationToken ct = default)
    {
        var producto = await db.Productos.AsNoTracking().FirstOrDefaultAsync(x => x.ProductoId == productoId, ct);
        return producto is null ? null : Map(producto);
    }

    public async Task<IReadOnlyList<ProductoResponse>> ListAsync(CancellationToken ct = default)
    {
        return await db.Productos
            .AsNoTracking()
            .OrderBy(x => x.Nombre)
            .Select(x => Map(x))
            .ToListAsync(ct);
    }

    public async Task<ProductoResponse> UpdateAsync(Guid productoId, UpdateProductoRequest request, CancellationToken ct = default)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(x => x.ProductoId == productoId, ct)
            ?? throw new InvalidOperationException("PRODUCT_NOT_FOUND");

        var skuTaken = await db.Productos.AsNoTracking()
            .AnyAsync(x => x.Sku == request.Sku.Trim().ToUpperInvariant() && x.ProductoId != productoId, ct);
        if (skuTaken)
        {
            throw new InvalidOperationException("SKU_ALREADY_EXISTS");
        }

        producto.Actualizar(
            request.Nombre,
            request.Sku,
            (TipoMedidaBase)request.TipoMedidaBase,
            (TipoComercialMayor)request.TipoComercialMayor,
            request.UnidadesPorCaja,
            request.UnidadesPorBulto);

        await db.SaveChangesAsync(ct);
        return Map(producto);
    }

    public async Task DeleteAsync(Guid productoId, CancellationToken ct = default)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(x => x.ProductoId == productoId, ct)
            ?? throw new InvalidOperationException("PRODUCT_NOT_FOUND");

        producto.Desactivar();
        await db.SaveChangesAsync(ct);
    }

    private static ProductoResponse Map(Producto p) => new(
        p.ProductoId,
        p.Nombre,
        p.Sku,
        p.TipoMedidaBase.ToString(),
        p.TipoComercialMayor.ToString(),
        p.UnidadesPorCaja,
        p.UnidadesPorBulto,
        p.Activo);
}
