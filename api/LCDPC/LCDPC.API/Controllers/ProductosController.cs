using LCDPC.API.Security;
using LCDPC.Application.Productos;
using Microsoft.AspNetCore.Mvc;

namespace LCDPC.API.Controllers;

[ApiController]
[Route("api/v1/productos")]
public class ProductosController(IProductoService productoService) : ControllerBase
{
    [HttpPost]
    [RequireRoles("admin_global", "admin_sede")]
    [ProducesResponseType(typeof(ProductoResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateProductoRequest request, CancellationToken ct)
    {
        try
        {
            var response = await productoService.CreateAsync(request, ct);
            return Created($"/api/v1/productos/{response.ProductoId}", response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "SKU_ALREADY_EXISTS")
        {
            return Conflict(new { code = "SKU_ALREADY_EXISTS", message = "ya existe un producto con ese sku" });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await productoService.GetByIdAsync(id, ct);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductoResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var response = await productoService.ListAsync(ct);
        return Ok(response);
    }

    [HttpPut("{id:guid}")]
    [RequireRoles("admin_global", "admin_sede")]
    [ProducesResponseType(typeof(ProductoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductoRequest request, CancellationToken ct)
    {
        try
        {
            var response = await productoService.UpdateAsync(id, request, ct);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "PRODUCT_NOT_FOUND")
        {
            return NotFound(new { code = "PRODUCT_NOT_FOUND", message = "producto no encontrado" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "SKU_ALREADY_EXISTS")
        {
            return Conflict(new { code = "SKU_ALREADY_EXISTS", message = "ya existe un producto con ese sku" });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequireRoles("admin_global")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await productoService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "PRODUCT_NOT_FOUND")
        {
            return NotFound(new { code = "PRODUCT_NOT_FOUND", message = "producto no encontrado" });
        }
    }
}
