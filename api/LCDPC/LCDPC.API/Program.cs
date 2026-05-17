using LCDPC.Application;
using LCDPC.Infrastructure;
using LCDPC.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();

    var superUserPassword = app.Configuration["Auth:SuperUserPassword"];
    if (string.IsNullOrWhiteSpace(superUserPassword))
    {
        superUserPassword = "SuperPerro123!";
    }

    await SuperUserSeeder.SeedAsync(dbContext, superUserPassword);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new
{
    service = "LCDPC.API",
    status = "ok"
}));

app.Run();
