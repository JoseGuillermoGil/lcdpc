using LCDPC.Domain.Entities.Users;
using LCDPC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.API.Controllers;

public sealed record CreateSedeRequest(
    string NombreTienda,
    string Rif,
    string Direccion,
    string TelefonoContacto,
    string? TelefonoContactoSecundario,
    string HorarioAtencion);

public sealed record SedeResponse(
    Guid SedeId,
    string NombreTienda,
    string Rif,
    string Direccion,
    string TelefonoContacto,
    string? TelefonoContactoSecundario,
    string HorarioAtencion);

[ApiController]
[Route("api/v1/sedes")]
public class SedesController(AppDbContext dbContext) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(SedeResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateSedeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NombreTienda)
            || string.IsNullOrWhiteSpace(request.Rif)
            || string.IsNullOrWhiteSpace(request.Direccion)
            || string.IsNullOrWhiteSpace(request.TelefonoContacto)
            || string.IsNullOrWhiteSpace(request.HorarioAtencion))
        {
            return BadRequest(new
            {
                code = "INVALID_REQUEST",
                message = "nombreTienda, rif, direccion, telefonoContacto y horarioAtencion son requeridos"
            });
        }

        var normalizedRif = request.Rif.Trim().ToUpperInvariant();
        var exists = await dbContext.Sedes.AsNoTracking().AnyAsync(x => x.Rif == normalizedRif, cancellationToken);
        if (exists)
        {
            return Conflict(new
            {
                code = "RIF_ALREADY_EXISTS",
                message = "ya existe una sede con ese rif"
            });
        }

        var sede = new Sede
        {
            Id = Guid.NewGuid(),
            NombreTienda = request.NombreTienda.Trim(),
            Rif = normalizedRif,
            Direccion = request.Direccion.Trim(),
            TelefonoContacto = request.TelefonoContacto.Trim(),
            TelefonoContactoSecundario = string.IsNullOrWhiteSpace(request.TelefonoContactoSecundario)
                ? null
                : request.TelefonoContactoSecundario.Trim(),
            HorarioAtencion = request.HorarioAtencion.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        dbContext.Sedes.Add(sede);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created(string.Empty, Map(sede));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SedeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var sedes = await dbContext.Sedes
            .AsNoTracking()
            .OrderBy(x => x.NombreTienda)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);

        return Ok(sedes);
    }

    private static SedeResponse Map(Sede sede)
    {
        return new SedeResponse(
            sede.Id,
            sede.NombreTienda,
            sede.Rif,
            sede.Direccion,
            sede.TelefonoContacto,
            sede.TelefonoContactoSecundario,
            sede.HorarioAtencion);
    }
}
