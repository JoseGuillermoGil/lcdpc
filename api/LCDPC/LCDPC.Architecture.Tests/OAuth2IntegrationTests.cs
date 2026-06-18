using System.Web;
using LCDPC.Application.OAuth2;
using LCDPC.Domain.Entities.OAuth2;
using LCDPC.Domain.Entities.Users;
using LCDPC.Infrastructure.OAuth2;
using LCDPC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LCDPC.Architecture.Tests;

public class OAuth2IntegrationTests
{
    [Fact]
    public async Task AuthorizeExchangeIntrospectRevoke_Flow_WorksEndToEnd()
    {
        await using var dbContext = CreateDbContext();
        var options = CreateOptions();
        var userId = await SeedUserAsync(dbContext);
        await SeedClientAsync(dbContext);

        using var keyService = new RsaKeyService(options, NullLogger<RsaKeyService>.Instance);
        var clientService = new OAuth2ClientService(dbContext);
        var tokenService = new OAuth2TokenService(dbContext, keyService, options);
        var authzService = new OAuth2AuthorizationService(dbContext, clientService, tokenService, options);

        const string verifier = "pkce-verifier-ok";
        var authorize = await authzService.AuthorizeAsync(
            new OAuth2AuthorizeRequest(
                ClientId: "lcdpc-web",
                RedirectUri: "http://localhost:4200/auth/callback",
                ResponseType: "code",
                Scope: "openid email profile",
                State: "abc123",
                CodeChallenge: BuildCodeChallenge(verifier),
                CodeChallengeMethod: "S256"),
            userId);

        Assert.True(authorize.Success);
        var code = HttpUtility.ParseQueryString(new Uri(authorize.RedirectUrl!).Query)["code"];
        Assert.False(string.IsNullOrWhiteSpace(code));

        var tokenResponse = await authzService.ExchangeCodeAsync(
            code!,
            verifier,
            "http://localhost:4200/auth/callback",
            "lcdpc-web");

        Assert.NotNull(tokenResponse);

        var accessIntrospection = await authzService.IntrospectTokenAsync(tokenResponse!.AccessToken);
        Assert.True(accessIntrospection.Active);
        Assert.Equal("lcdpc-web", accessIntrospection.ClientId);
        Assert.Equal(userId.ToString(), accessIntrospection.Sub);

        var revokeResponse = await authzService.RevokeTokenAsync(tokenResponse.RefreshToken);
        Assert.True(revokeResponse.Success);

        var refreshIntrospection = await authzService.IntrospectTokenAsync(tokenResponse.RefreshToken);
        Assert.False(refreshIntrospection.Active);
    }

    [Fact]
    public async Task RefreshRotation_ReuseOfConsumedToken_RevokesEntireFamily()
    {
        await using var dbContext = CreateDbContext();
        var options = CreateOptions();
        var userId = await SeedUserAsync(dbContext);
        await SeedClientAsync(dbContext);

        using var keyService = new RsaKeyService(options, NullLogger<RsaKeyService>.Instance);
        var clientService = new OAuth2ClientService(dbContext);
        var tokenService = new OAuth2TokenService(dbContext, keyService, options);
        var authzService = new OAuth2AuthorizationService(dbContext, clientService, tokenService, options);

        const string verifier = "pkce-reuse-verifier";
        var authorize = await authzService.AuthorizeAsync(
            new OAuth2AuthorizeRequest(
                ClientId: "lcdpc-web",
                RedirectUri: "http://localhost:4200/auth/callback",
                ResponseType: "code",
                Scope: "openid email profile",
                State: "reuse",
                CodeChallenge: BuildCodeChallenge(verifier),
                CodeChallengeMethod: "S256"),
            userId);

        var code = HttpUtility.ParseQueryString(new Uri(authorize.RedirectUrl!).Query)["code"];
        var firstPair = await authzService.ExchangeCodeAsync(code!, verifier, "http://localhost:4200/auth/callback", "lcdpc-web");
        Assert.NotNull(firstPair);

        var secondPair = await authzService.RefreshTokenAsync(firstPair!.RefreshToken, "lcdpc-web");
        Assert.NotNull(secondPair);

        var replay = await authzService.RefreshTokenAsync(firstPair.RefreshToken, "lcdpc-web");
        Assert.Null(replay);

        var familyHashes = await dbContext.OAuth2RefreshTokens
            .Where(x => x.UserId == userId)
            .ToListAsync();

        Assert.NotEmpty(familyHashes);
        Assert.All(familyHashes, token => Assert.NotNull(token.RevokedAtUtc));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"oauth2-int-tests-{Guid.NewGuid()}")
            .Options;

        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static OAuth2Options CreateOptions()
    {
        return new OAuth2Options
        {
            Issuer = "http://localhost:8080",
            Audience = "lcdpc-api",
            AccessTokenTtlMinutes = 60,
            RefreshTokenTtlDays = 30,
            AuthorizationCodeTtlMinutes = 10
        };
    }

    private static async Task SeedClientAsync(AppDbContext dbContext)
    {
        dbContext.OAuth2Clients.Add(new OAuth2Client
        {
            ClientId = "lcdpc-web",
            ClientName = "LCDPC Web SPA",
            RedirectUris = ["http://localhost:4200/auth/callback"],
            GrantTypes = ["authorization_code", "refresh_token"],
            RequirePkce = true,
            AllowedScopes = "openid email profile",
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> SeedUserAsync(AppDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "oauth2@example.com",
            PasswordHash = "PBKDF2$100000$SHA256$U0FMVA==$SEFTSA==",
            EmailVerifiedAtUtc = DateTime.UtcNow,
            OnboardingStatus = "active",
            Status = UserStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    private static string BuildCodeChallenge(string verifier)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(verifier));
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
