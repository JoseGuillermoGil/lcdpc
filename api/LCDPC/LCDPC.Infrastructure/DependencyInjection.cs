using LCDPC.Infrastructure.OAuth2;
using LCDPC.Infrastructure.Persistence;
using LCDPC.Infrastructure.Users.Auth;
using LCDPC.Application.OAuth2;
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

        var jwtTokenOptions = new JwtTokenOptions
        {
            Issuer = string.IsNullOrWhiteSpace(configuration["Auth:Jwt:Issuer"])
                ? "LCDPC.API"
                : configuration["Auth:Jwt:Issuer"]!,
            Audience = string.IsNullOrWhiteSpace(configuration["Auth:Jwt:Audience"])
                ? "LCDPC.Web"
                : configuration["Auth:Jwt:Audience"]!,
            SigningKey = string.IsNullOrWhiteSpace(configuration["Auth:Jwt:SigningKey"])
                ? "CHANGE-ME-WITH-AT-LEAST-32-CHARS-DEV-ONLY"
                : configuration["Auth:Jwt:SigningKey"]!
        };

        if (jwtTokenOptions.SigningKey.Length < 32)
        {
            jwtTokenOptions.SigningKey = "CHANGE-ME-WITH-AT-LEAST-32-CHARS-DEV-ONLY";
        }

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(authOptions);
        services.AddSingleton(googleOAuthOptions);
        services.AddSingleton(jwtTokenOptions);

        // OAuth2 configuration
        var oauth2Options = new OAuth2Options
        {
            Issuer = string.IsNullOrWhiteSpace(configuration["OAuth2:Issuer"])
                ? "http://localhost:8080"
                : configuration["OAuth2:Issuer"]!,
            Audience = string.IsNullOrWhiteSpace(configuration["OAuth2:Audience"])
                ? "lcdpc-api"
                : configuration["OAuth2:Audience"]!,
            AccessTokenTtlMinutes = int.TryParse(configuration["OAuth2:AccessTokenTtlMinutes"], out var atTtl) && atTtl > 0
                ? atTtl
                : 60,
            RefreshTokenTtlDays = int.TryParse(configuration["OAuth2:RefreshTokenTtlDays"], out var rtTtl) && rtTtl > 0
                ? rtTtl
                : 30,
            AuthorizationCodeTtlMinutes = int.TryParse(configuration["OAuth2:AuthorizationCodeTtlMinutes"], out var acTtl) && acTtl > 0
                ? acTtl
                : 10,
            RsaKeyPath = configuration["OAuth2:RsaKeyPath"],
            Clients = configuration.GetSection("OAuth2:Clients").Get<List<OAuth2ClientOptions>>() ?? []
        };

        services.AddSingleton(oauth2Options);
        services.AddSingleton<IOAuth2KeyService, RsaKeyService>();

        // OAuth2 Application Services (implemented in Infrastructure)
        services.AddScoped<IOAuth2ClientService, OAuth2ClientService>();
        services.AddScoped<IOAuth2TokenService, OAuth2TokenService>();
        services.AddScoped<IOAuth2AuthorizationService, OAuth2AuthorizationService>();

        // Google OAuth Service
        services.AddHttpClient<IGoogleOAuthService, GoogleOAuthService>(client =>
        {
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("postgres");
        services.AddScoped<IRegistrationFlowService, RegistrationFlowService>();

        return services;
    }
}
