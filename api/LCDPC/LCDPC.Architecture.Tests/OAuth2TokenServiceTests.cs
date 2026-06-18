using System.IdentityModel.Tokens.Jwt;
using LCDPC.Infrastructure.OAuth2;
using LCDPC.Infrastructure.Persistence;
using LCDPC.Infrastructure.Users.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LCDPC.Architecture.Tests;

public class OAuth2TokenServiceTests
{
    [Fact]
    public async Task GenerateAccessTokenAsync_ProducesValidJwt()
    {
        await using var dbContext = CreateDbContext();
        var options = CreateOAuth2Options();
        using var keyService = new RsaKeyService(options, NullLogger<RsaKeyService>.Instance);
        var service = new OAuth2TokenService(dbContext, keyService, options);

        var userId = Guid.NewGuid();
        var token = await service.GenerateAccessTokenAsync(userId, "lcdpc-web", "openid email profile");

        var isValid = await service.ValidateAccessTokenAsync(token);
        Assert.True(isValid);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(userId.ToString(), jwt.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal("lcdpc-web", jwt.Claims.First(c => c.Type == "client_id").Value);
        Assert.Equal("openid email profile", jwt.Claims.First(c => c.Type == "scope").Value);
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_ReturnsFalse_ForTamperedToken()
    {
        await using var dbContext = CreateDbContext();
        var options = CreateOAuth2Options();
        using var keyService = new RsaKeyService(options, NullLogger<RsaKeyService>.Instance);
        var service = new OAuth2TokenService(dbContext, keyService, options);

        var token = await service.GenerateAccessTokenAsync(Guid.NewGuid(), "lcdpc-web", "openid");
        var tampered = token[..^1] + (token[^1] == 'a' ? 'b' : 'a');

        var isValid = await service.ValidateAccessTokenAsync(tampered);
        Assert.False(isValid);
    }

    [Fact]
    public async Task GenerateRefreshTokenAsync_ReturnsTokenAndMatchingHash()
    {
        await using var dbContext = CreateDbContext();
        var options = CreateOAuth2Options();
        using var keyService = new RsaKeyService(options, NullLogger<RsaKeyService>.Instance);
        var service = new OAuth2TokenService(dbContext, keyService, options);

        var (token, hash) = await service.GenerateRefreshTokenAsync();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(TokenHashing.Hash(token), hash);
        Assert.DoesNotContain("=", token, StringComparison.Ordinal);
        Assert.DoesNotContain("+", token, StringComparison.Ordinal);
        Assert.DoesNotContain("/", token, StringComparison.Ordinal);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"oauth2-token-tests-{Guid.NewGuid()}")
            .Options;

        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static OAuth2Options CreateOAuth2Options()
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
}
