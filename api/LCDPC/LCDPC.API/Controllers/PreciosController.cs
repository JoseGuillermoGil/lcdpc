using LCDPC.API.Security;
using LCDPC.Application.Precios;
using Microsoft.AspNetCore.Mvc;

namespace LCDPC.API.Controllers;

[ApiController]
[Route("api/v1/precios")]
public class PreciosController(IPrecioProductoSedeService precioService) : ControllerBase
{
    [HttpPost]
    [RequireRoles("admin_global", "admin_sede")]
    [ProducesResponseType(typeof(PrecioResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreatePrecioRequest request, CancellationToken ct)
    {
        try
        {
            var response = await precioService.CreateAsync(request, ct);
            return Created($"/api/v1/precios/{response.PrecioProductoSedeId}", response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "ACTIVE_PRICE_ALREADY_EXISTS")
        {
            return Conflict(new { code = "ACTIVE_PRICE_ALREADY_EXISTS", message = "ya existe un precio activo para este producto y sede" });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PrecioResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await precioService.GetByIdAsync(id, ct);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("producto/{productoId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PrecioResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByProducto(Guid productoId, CancellationToken ct)
    {
        var response = await precioService.ListByProductoAsync(productoId, ct);
        return Ok(response);
    }

    [HttpGet("sede/{sedeId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PrecioResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListBySede(Guid sedeId, CancellationToken ct)
    {
        var response = await precioService.ListBySedeAsync(sedeId, ct);
        return Ok(response);
    }

    [HttpPut("{id:guid}")]
    [RequireRoles("admin_global", "admin_sede")]
    [ProducesResponseType(typeof(PrecioResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePrecioRequest request, CancellationToken ct)
    {
        try
        {
            var response = await precioService.UpdateAsync(id, request, ct);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "PRICE_NOT_FOUND")
        {
            return NotFound(new { code = "PRICE_NOT_FOUND", message = "precio no encontrado" });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequireRoles("admin_global")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await precioService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "PRICE_NOT_FOUND")
        {
            return NotFound(new { code = "PRICE_NOT_FOUND", message = "precio no encontrado" });
        }
    }
}
