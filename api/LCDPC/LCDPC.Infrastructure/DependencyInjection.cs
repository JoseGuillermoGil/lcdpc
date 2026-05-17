using LCDPC.Infrastructure.Persistence;
using LCDPC.Infrastructure.Users.Auth;
using LCDPC.Application.Users.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LCDPC.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("LCDPC")
                               ?? "Host=postgres;Port=5432;Database=lcdpc_db;Username=lcdpc;Password=lcdpc123";

        var accessTokenTtlMinutes = int.TryParse(configuration["Auth:AccessTokenTtlMinutes"], out var configuredAccessTokenTtlMinutes)
            ? configuredAccessTokenTtlMinutes
            : 60;
        var refreshTokenTtlDays = int.TryParse(configuration["Auth:RefreshTokenTtlDays"], out var configuredRefreshTokenTtlDays)
            ? configuredRefreshTokenTtlDays
            : 30;
        var passwordResetTtlMinutes = int.TryParse(configuration["Auth:PasswordResetTtlMinutes"], out var configuredPasswordResetTtlMinutes)
            ? configuredPasswordResetTtlMinutes
            : 30;
        var revokeSessionsOnPasswordReset = bool.TryParse(configuration["Auth:RevokeSessionsOnPasswordReset"], out var configuredRevoke)
            ? configuredRevoke
            : true;

        var authOptions = new AuthSecurityOptions
        {
            AccessTokenTtlMinutes = accessTokenTtlMinutes,
            RefreshTokenTtlDays = refreshTokenTtlDays,
            PasswordResetTtlMinutes = passwordResetTtlMinutes,
            RevokeSessionsOnPasswordReset = revokeSessionsOnPasswordReset
        };

        if (authOptions.AccessTokenTtlMinutes <= 0)
        {
            authOptions.AccessTokenTtlMinutes = 60;
        }

        if (authOptions.RefreshTokenTtlDays <= 0)
        {
            authOptions.RefreshTokenTtlDays = 30;
        }

        if (authOptions.PasswordResetTtlMinutes <= 0)
        {
            authOptions.PasswordResetTtlMinutes = 30;
        }

        var googleOAuthOptions = new GoogleOAuthOptions
        {
            ClientId = configuration["Auth:Google:ClientId"],
            ClientSecret = configuration["Auth:Google:ClientSecret"],
            RedirectUri = configuration["Auth:Google:RedirectUri"],
            Scope = string.IsNullOrWhiteSpace(configuration["Auth:Google:Scope"])
                ? "openid email profile"
                : configuration["Auth:Google:Scope"]!
        };

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(authOptions);
        services.AddSingleton(googleOAuthOptions);

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("postgres");
        services.AddScoped<IRegistrationFlowService, RegistrationFlowService>();

        return services;
    }
}
