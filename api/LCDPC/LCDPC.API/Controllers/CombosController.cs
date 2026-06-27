using LCDPC.API.Security;
using LCDPC.Application.Combos;
using Microsoft.AspNetCore.Mvc;

namespace LCDPC.API.Controllers;

[ApiController]
[Route("api/v1/combos")]
public class CombosController(IComboService comboService) : ControllerBase
{
    [HttpPost]
    [RequireRoles("admin_global", "admin_sede")]
    [ProducesResponseType(typeof(ComboResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateComboRequest request, CancellationToken ct)
    {
        var response = await comboService.CreateAsync(request, ct);
        return Created($"/api/v1/combos/{response.ComboId}", response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ComboResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await comboService.GetByIdAsync(id, ct);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ComboResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var response = await comboService.ListAsync(ct);
        return Ok(response);
    }

    [HttpPut("{id:guid}")]
    [RequireRoles("admin_global", "admin_sede")]
    [ProducesResponseType(typeof(ComboResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateComboRequest request, CancellationToken ct)
    {
        try
        {
            var response = await comboService.UpdateAsync(id, request, ct);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "COMBO_NOT_FOUND")
        {
            return NotFound(new { code = "COMBO_NOT_FOUND", message = "combo no encontrado" });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequireRoles("admin_global")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await comboService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "COMBO_NOT_FOUND")
        {
            return NotFound(new { code = "COMBO_NOT_FOUND", message = "combo no encontrado" });
        }
    }

    [HttpPost("{id:guid}/publicar")]
    [RequireRoles("admin_global", "admin_sede")]
    [ProducesResponseType(typeof(ComboResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Publicar(Guid id, CancellationToken ct)
    {
        try
        {
            var response = await comboService.PublicarAsync(id, ct);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "COMBO_NOT_FOUND")
        {
            return NotFound(new { code = "COMBO_NOT_FOUND", message = "combo no encontrado" });
        }
    }

    [HttpPost("{id:guid}/pausar")]
    [RequireRoles("admin_global", "admin_sede")]
    [ProducesResponseType(typeof(ComboResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pausar(Guid id, CancellationToken ct)
    {
        try
        {
            var response = await comboService.PausarAsync(id, ct);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "COMBO_NOT_FOUND")
        {
            return NotFound(new { code = "COMBO_NOT_FOUND", message = "combo no encontrado" });
        }
    }
}
