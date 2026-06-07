using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using LCDPC.API.Security;
using LCDPC.Application;
using LCDPC.Domain.Entities.Users;
using LCDPC.Infrastructure;
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

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrWhiteSpace(context.Token))
                {
                    context.Token = AccessTokenResolver.Resolve(context.HttpContext.Request);
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var accessToken = AccessTokenResolver.Resolve(context.HttpContext.Request);
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    context.Fail("MISSING_ACCESS_TOKEN");
                    return;
                }

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var tokenHash = TokenHashing.Hash(accessToken);
                var nowUtc = DateTime.UtcNow;

                var session = await dbContext.UserSessions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.AccessTokenHash == tokenHash
                        && x.RevokedAtUtc == null
                        && x.AccessTokenExpiresAtUtc > nowUtc,
                        context.HttpContext.RequestAborted);

                if (session is null)
                {
                    context.Fail("INVALID_SESSION");
                    return;
                }

                var subject = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                              ?? context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(subject, out var subjectUserId) || subjectUserId != session.UserId)
                {
                    context.Fail("TOKEN_SUBJECT_MISMATCH");
                    return;
                }

                var sessionClaim = context.Principal?.FindFirstValue("sid");
                if (!Guid.TryParse(sessionClaim, out var tokenSessionId) || tokenSessionId != session.Id)
                {
                    context.Fail("TOKEN_SESSION_MISMATCH");
                    return;
                }

                var userStatus = await dbContext.Users
                    .AsNoTracking()
                    .Where(x => x.Id == session.UserId)
                    .Select(x => x.Status)
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                if (userStatus is UserStatus.Suspended or UserStatus.Deactivated)
                {
                    context.Fail("USER_NOT_ALLOWED");
                }
            }
        };
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
