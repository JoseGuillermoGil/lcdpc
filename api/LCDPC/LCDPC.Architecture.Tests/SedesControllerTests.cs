using LCDPC.API.Controllers;
using LCDPC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.Architecture.Tests;

public class SedesControllerTests
{
    [Fact]
    public async Task Create_ReturnsCreated_WhenPayloadIsValid()
    {
        await using var dbContext = BuildDbContext();
        var controller = new SedesController(dbContext);

        var result = await controller.Create(
            new CreateSedeRequest("La Casa Del Perro - Centro", "J123456789", "Av. Principal", "04121234567", "02121234567", "Lun-Sab 8:00-18:00"),
            CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        var payload = Assert.IsType<SedeResponse>(created.Value);

        Assert.Equal("La Casa Del Perro - Centro", payload.NombreTienda);
        Assert.Equal("J123456789", payload.Rif);
        Assert.Equal("02121234567", payload.TelefonoContactoSecundario);
        Assert.Equal("Lun-Sab 8:00-18:00", payload.HorarioAtencion);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenRifAlreadyExists()
    {
        await using var dbContext = BuildDbContext();
        var controller = new SedesController(dbContext);

        await controller.Create(
            new CreateSedeRequest("Tienda 1", "J987654321", "Dir 1", "02121234567", null, "Lun-Vie 8:00-17:00"),
            CancellationToken.None);

        var secondResult = await controller.Create(
            new CreateSedeRequest("Tienda 2", "J987654321", "Dir 2", "02127654321", null, "Lun-Vie 8:00-17:00"),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(secondResult);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsRegisteredSedes()
    {
        await using var dbContext = BuildDbContext();
        var controller = new SedesController(dbContext);

        await controller.Create(
            new CreateSedeRequest("Tienda A", "J111111111", "Dir A", "04120000001", "04120000011", "Lun-Vie 8:00-17:00"),
            CancellationToken.None);

        await controller.Create(
            new CreateSedeRequest("Tienda B", "J222222222", "Dir B", "04120000002", null, "Lun-Sab 9:00-18:00"),
            CancellationToken.None);

        var result = await controller.List(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<SedeResponse>>(ok.Value);
        Assert.Equal(2, payload.Count);
    }

    private static AppDbContext BuildDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
