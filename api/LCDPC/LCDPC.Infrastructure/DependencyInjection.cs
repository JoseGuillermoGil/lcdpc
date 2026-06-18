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

        var passwordResetTtlMinutes = int.TryParse(configuration["Auth:PasswordResetTtlMinutes"], out var configuredPasswordResetTtlMinutes)
            ? configuredPasswordResetTtlMinutes
            : 30;
        var revokeSessionsOnPasswordReset = bool.TryParse(configuration["Auth:RevokeSessionsOnPasswordReset"], out var configuredRevoke)
            ? configuredRevoke
            : true;

        var authOptions = new AuthSecurityOptions
        {
            PasswordResetTtlMinutes = passwordResetTtlMinutes,
            RevokeSessionsOnPasswordReset = revokeSessionsOnPasswordReset
        };

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

        // OAuth2 configuration
        var configuredOAuth2Clients = configuration.GetSection("OAuth2:Clients").Get<List<OAuth2ClientOptions>>() ?? [];
        if (configuredOAuth2Clients.Count == 0)
        {
            configuredOAuth2Clients =
            [
                new OAuth2ClientOptions
                {
                    ClientId = "lcdpc-web",
                    ClientName = "LCDPC Web SPA",
                    RedirectUris = ["http://localhost:4200", "http://localhost:4200/auth/callback"],
                    GrantTypes = ["authorization_code", "refresh_token"],
                    RequirePkce = true,
                    AllowedScopes = "openid email profile admin admin:sedes admin:users"
                }
            ];
        }

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
            Clients = configuredOAuth2Clients
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
