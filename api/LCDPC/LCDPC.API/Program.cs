using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using LCDPC.API.Security;
using LCDPC.Application;
using LCDPC.Application.OAuth2;
using LCDPC.Domain.Entities.Users;
using LCDPC.Infrastructure;
using LCDPC.Infrastructure.OAuth2;
using LCDPC.Infrastructure.Persistence;
using LCDPC.Infrastructure.Users.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Resend;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var oauth2Issuer = string.IsNullOrWhiteSpace(builder.Configuration["OAuth2:Issuer"])
    ? "http://localhost:8080"
    : builder.Configuration["OAuth2:Issuer"]!;
var oauth2Audience = string.IsNullOrWhiteSpace(builder.Configuration["OAuth2:Audience"])
    ? "lcdpc-api"
    : builder.Configuration["OAuth2:Audience"]!;

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddResend(options =>
{
    options.ApiToken = Environment.GetEnvironmentVariable("RESEND_APITOKEN")!;
});
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Register OAuth2JwtBearerEvents as singleton
builder.Services.AddSingleton<OAuth2JwtBearerEvents>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = oauth2Issuer,
            ValidateAudience = true,
            ValidAudience = oauth2Audience,
            // Signature validation is handled in OAuth2JwtBearerEvents via IOAuth2TokenService (RS256/JWKS).
            ValidateIssuerSigningKey = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Use OAuth2JwtBearerEvents for message resolution and enhanced token validation
        options.EventsType = typeof(OAuth2JwtBearerEvents);
    });
builder.Services.AddAuthorization();

if (allowedCorsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("WebClient", policy =>
        {
            policy.WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
}

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

    var superUserSeed = new SuperUserSeeder.SuperUserSeedOptions(
        Alias: app.Configuration["Auth:SuperUserAlias"] ?? "admin_inicial",
        Email: app.Configuration["Auth:SuperUserEmail"] ?? "admin@lcdpc.local",
        FirstName: app.Configuration["Auth:SuperUserFirstName"] ?? "Admin",
        LastName: app.Configuration["Auth:SuperUserLastName"] ?? "Inicial",
        IdentityDocument: app.Configuration["Auth:SuperUserIdentityDocument"] ?? "V00000001",
        WhatsAppPhone: app.Configuration["Auth:SuperUserWhatsAppPhone"] ?? "0000000000",
        FullAddress: app.Configuration["Auth:SuperUserFullAddress"] ?? "Usuario administrador inicial");

    await SuperUserSeeder.SeedAsync(dbContext, superUserPassword, superUserSeed);
    await OAuth2ClientSeeder.SeedAsync(dbContext);

    var initialApiToken = await ApiTokenSeeder.SeedAsync(dbContext);
    if (initialApiToken is not null)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  API TOKEN DE SYNC INICIAL (guardarlo, no se volverá a mostrar):");
        Console.WriteLine($"  {initialApiToken}");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
    }

    var keyService = scope.ServiceProvider.GetRequiredService<IOAuth2KeyService>();
    _ = keyService.GetJwks();
}

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

if (allowedCorsOrigins.Length > 0)
{
    app.UseCors("WebClient");
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new
{
    service = "LCDPC.API",
    status = "ok"
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
