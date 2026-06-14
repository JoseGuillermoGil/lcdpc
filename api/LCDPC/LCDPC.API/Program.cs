using System.Security.Claims;
using System.Text;
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
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var jwtIssuer = string.IsNullOrWhiteSpace(builder.Configuration["Auth:Jwt:Issuer"])
    ? "LCDPC.API"
    : builder.Configuration["Auth:Jwt:Issuer"]!;
var jwtAudience = string.IsNullOrWhiteSpace(builder.Configuration["Auth:Jwt:Audience"])
    ? "LCDPC.Web"
    : builder.Configuration["Auth:Jwt:Audience"]!;
var jwtSigningKey = string.IsNullOrWhiteSpace(builder.Configuration["Auth:Jwt:SigningKey"])
    ? "CHANGE-ME-WITH-AT-LEAST-32-CHARS-DEV-ONLY"
    : builder.Configuration["Auth:Jwt:SigningKey"]!;

if (jwtSigningKey.Length < 32)
{
    jwtSigningKey = "CHANGE-ME-WITH-AT-LEAST-32-CHARS-DEV-ONLY";
}

var jwtSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Register OAuth2JwtBearerEvents as singleton
builder.Services.AddSingleton<OAuth2JwtBearerEvents>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtSecurityKey,
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

    await SuperUserSeeder.SeedAsync(dbContext, superUserPassword);
    await OAuth2ClientSeeder.SeedAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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

app.Run();
