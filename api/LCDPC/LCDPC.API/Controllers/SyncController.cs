using LCDPC.API.Security;
using LCDPC.Application.Sync;
using Microsoft.AspNetCore.Mvc;

namespace LCDPC.API.Controllers;

[ApiController]
[Route("api/v1/sync")]
public class SyncController(ISyncService syncService) : ControllerBase
{
    [HttpPost("productos")]
    [ApiKeyAuth]
    [ProducesResponseType(typeof(SyncBatchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SyncProductos([FromBody] List<SyncProductoRequest> request, CancellationToken ct)
    {
        var response = await syncService.SyncProductosAsync(request, ct);
        return Ok(response);
    }

    [HttpPost("combos")]
    [ApiKeyAuth]
    [ProducesResponseType(typeof(SyncBatchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SyncCombos([FromBody] List<SyncComboRequest> request, CancellationToken ct)
    {
        var response = await syncService.SyncCombosAsync(request, ct);
        return Ok(response);
    }
}
