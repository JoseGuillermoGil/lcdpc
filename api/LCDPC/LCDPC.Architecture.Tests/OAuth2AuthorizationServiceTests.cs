using System.Security.Cryptography;
using System.Text;
using LCDPC.Application.OAuth2;
using LCDPC.Domain.Entities.OAuth2;
using LCDPC.Infrastructure.OAuth2;
using LCDPC.Infrastructure.Persistence;
using LCDPC.Infrastructure.Users.Auth;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.Architecture.Tests;

public class OAuth2AuthorizationServiceTests
{
    [Fact]
    public async Task AuthorizeAsync_ReturnsRedirectUrl_WhenRequestIsValid()
    {
        await using var dbContext = CreateDbContext();
        var clientService = new FakeClientService(requirePkce: true);
        var tokenService = new FakeTokenService();
        var service = CreateService(dbContext, clientService, tokenService);

        var request = new OAuth2AuthorizeRequest(
            ClientId: "lcdpc-web",
            RedirectUri: "http://localhost:4200/auth/callback",
            ResponseType: "code",
            Scope: "openid email profile",
            State: "state-ok",
            CodeChallenge: BuildCodeChallenge("valid-verifier"),
            CodeChallengeMethod: "S256");

        var result = await service.AuthorizeAsync(request, Guid.NewGuid());

        Assert.True(result.Success);
        Assert.NotNull(result.RedirectUrl);
        Assert.Contains("code=", result.RedirectUrl, StringComparison.Ordinal);
        Assert.Contains("state=state-ok", result.RedirectUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeAsync_ReturnsError_WhenPkceChallengeIsMissing()
    {
        await using var dbContext = CreateDbContext();
        var clientService = new FakeClientService(requirePkce: true);
        var tokenService = new FakeTokenService();
        var service = CreateService(dbContext, clientService, tokenService);

        var request = new OAuth2AuthorizeRequest(
            ClientId: "lcdpc-web",
            RedirectUri: "http://localhost:4200/auth/callback",
            ResponseType: "code",
            Scope: "openid email profile",
            State: "state-1",
            CodeChallenge: null,
            CodeChallengeMethod: "S256");

        var result = await service.AuthorizeAsync(request, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("invalid_request", result.ErrorCode);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ReturnsTokenPair_WhenPkceVerifierMatches()
    {
        await using var dbContext = CreateDbContext();
        var clientService = new FakeClientService(requirePkce: true);
        var tokenService = new FakeTokenService();
        var service = CreateService(dbContext, clientService, tokenService);

        var rawCode = "code-success";
        var userId = Guid.NewGuid();
        dbContext.OAuth2AuthorizationCodes.Add(new OAuth2AuthorizationCode
        {
            Code = TokenHashing.Hash(rawCode),
            ClientId = "lcdpc-web",
            UserId = userId,
            RedirectUri = "http://localhost:4200/auth/callback",
            Scope = "openid email profile",
            CodeChallenge = BuildCodeChallenge("expected-verifier"),
            CodeChallengeMethod = "S256",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var response = await service.ExchangeCodeAsync(
            code: rawCode,
            codeVerifier: "expected-verifier",
            redirectUri: "http://localhost:4200/auth/callback",
            clientId: "lcdpc-web");

        Assert.NotNull(response);
        Assert.Equal("Bearer", response!.TokenType);
        Assert.Equal("fake-access-token", response.AccessToken);

        var storedCode = await dbContext.OAuth2AuthorizationCodes.FirstAsync();
        Assert.NotNull(storedCode.UsedAtUtc);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ReturnsNull_WhenPkceVerifierDoesNotMatch()
    {
        await using var dbContext = CreateDbContext();
        var clientService = new FakeClientService(requirePkce: true);
        var tokenService = new FakeTokenService();
        var service = CreateService(dbContext, clientService, tokenService);

        var rawCode = "code-abc";
        dbContext.OAuth2AuthorizationCodes.Add(new OAuth2AuthorizationCode
        {
            Code = TokenHashing.Hash(rawCode),
            ClientId = "lcdpc-web",
            UserId = Guid.NewGuid(),
            RedirectUri = "http://localhost:4200/auth/callback",
            Scope = "openid email profile",
            CodeChallenge = BuildCodeChallenge("expected-verifier"),
            CodeChallengeMethod = "S256",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var response = await service.ExchangeCodeAsync(
            code: rawCode,
            codeVerifier: "wrong-verifier",
            redirectUri: "http://localhost:4200/auth/callback",
            clientId: "lcdpc-web");

        Assert.Null(response);

        var storedCode = await dbContext.OAuth2AuthorizationCodes.FirstAsync();
        Assert.Null(storedCode.UsedAtUtc);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ReturnsNull_WhenCodeWasAlreadyUsed()
    {
        await using var dbContext = CreateDbContext();
        var clientService = new FakeClientService(requirePkce: true);
        var tokenService = new FakeTokenService();
        var service = CreateService(dbContext, clientService, tokenService);

        var rawCode = "code-reused";
        dbContext.OAuth2AuthorizationCodes.Add(new OAuth2AuthorizationCode
        {
            Code = TokenHashing.Hash(rawCode),
            ClientId = "lcdpc-web",
            UserId = Guid.NewGuid(),
            RedirectUri = "http://localhost:4200/auth/callback",
            Scope = "openid email profile",
            CodeChallenge = BuildCodeChallenge("expected-verifier"),
            CodeChallengeMethod = "S256",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            UsedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var response = await service.ExchangeCodeAsync(
            code: rawCode,
            codeVerifier: "expected-verifier",
            redirectUri: "http://localhost:4200/auth/callback",
            clientId: "lcdpc-web");

        Assert.Null(response);
    }

    [Fact]
    public async Task RefreshTokenAsync_RotatesTokenFamily_WhenTokenIsValid()
    {
        await using var dbContext = CreateDbContext();
        var clientService = new FakeClientService(requirePkce: true);
        var tokenService = new FakeTokenService();
        var service = CreateService(dbContext, clientService, tokenService);

        var rawRefreshToken = "refresh-token-1";
        var familyId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        dbContext.OAuth2RefreshTokens.Add(new OAuth2RefreshToken
        {
            TokenHash = TokenHashing.Hash(rawRefreshToken),
            ClientId = "lcdpc-web",
            UserId = userId,
            Scope = "openid email profile",
            FamilyId = familyId,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(10),
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var response = await service.RefreshTokenAsync(rawRefreshToken, "lcdpc-web");

        Assert.NotNull(response);
        Assert.Equal("Bearer", response!.TokenType);
        Assert.Equal("fake-access-token", response.AccessToken);

        var tokens = await dbContext.OAuth2RefreshTokens
            .Where(t => t.FamilyId == familyId)
            .ToListAsync();

        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens, t => t.PreviousTokenHash == TokenHashing.Hash(rawRefreshToken));
        Assert.Single(tokens, t => t.TokenHash == TokenHashing.Hash(rawRefreshToken) && t.RevokedAtUtc.HasValue);
    }

    [Fact]
    public async Task RefreshTokenAsync_RevokesEntireFamily_WhenConsumedTokenIsReused()
    {
        await using var dbContext = CreateDbContext();
        var clientService = new FakeClientService(requirePkce: true);
        var tokenService = new FakeTokenService();
        var service = CreateService(dbContext, clientService, tokenService);

        var rawRefreshToken = "refresh-token-reuse";
        var rawNextToken = "refresh-token-next";
        var familyId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        dbContext.OAuth2RefreshTokens.AddRange(
            new OAuth2RefreshToken
            {
                TokenHash = TokenHashing.Hash(rawRefreshToken),
                ClientId = "lcdpc-web",
                UserId = userId,
                Scope = "openid email profile",
                FamilyId = familyId,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(10),
                RevokedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2)
            },
            new OAuth2RefreshToken
            {
                TokenHash = TokenHashing.Hash(rawNextToken),
                ClientId = "lcdpc-web",
                UserId = userId,
                Scope = "openid email profile",
                FamilyId = familyId,
                PreviousTokenHash = TokenHashing.Hash(rawRefreshToken),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(10),
                RevokedAtUtc = null,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
            });
        await dbContext.SaveChangesAsync();

        var response = await service.RefreshTokenAsync(rawRefreshToken, "lcdpc-web");

        Assert.Null(response);

        var familyTokens = await dbContext.OAuth2RefreshTokens
            .Where(t => t.FamilyId == familyId)
            .ToListAsync();

        Assert.All(familyTokens, token => Assert.NotNull(token.RevokedAtUtc));
    }

    [Fact]
    public async Task RevokeTokenAsync_RevokesEntireFamily_WhenTokenExists()
    {
        await using var dbContext = CreateDbContext();
        var clientService = new FakeClientService(requirePkce: true);
        var tokenService = new FakeTokenService();
        var service = CreateService(dbContext, clientService, tokenService);

        var rawRefreshToken = "refresh-token-revoke";
        var familyId = Guid.NewGuid();

        dbContext.OAuth2RefreshTokens.AddRange(
            new OAuth2RefreshToken
            {
                TokenHash = TokenHashing.Hash(rawRefreshToken),
                ClientId = "lcdpc-web",
                UserId = Guid.NewGuid(),
                Scope = "openid email profile",
                FamilyId = familyId,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(10),
                CreatedAtUtc = DateTime.UtcNow
            },
            new OAuth2RefreshToken
            {
                TokenHash = TokenHashing.Hash("refresh-token-sibling"),
                ClientId = "lcdpc-web",
                UserId = Guid.NewGuid(),
                Scope = "openid email profile",
                FamilyId = familyId,
                PreviousTokenHash = TokenHashing.Hash(rawRefreshToken),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(10),
                CreatedAtUtc = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var response = await service.RevokeTokenAsync(rawRefreshToken);

        Assert.True(response.Success);

        var familyTokens = await dbContext.OAuth2RefreshTokens
            .Where(t => t.FamilyId == familyId)
            .ToListAsync();
        Assert.All(familyTokens, token => Assert.NotNull(token.RevokedAtUtc));
    }

    private static OAuth2AuthorizationService CreateService(
        AppDbContext dbContext,
        IOAuth2ClientService clientService,
        IOAuth2TokenService tokenService)
    {
        var options = new OAuth2Options
        {
            Issuer = "http://localhost:8080",
            Audience = "lcdpc-api",
            AccessTokenTtlMinutes = 60,
            RefreshTokenTtlDays = 30,
            AuthorizationCodeTtlMinutes = 10
        };

        return new OAuth2AuthorizationService(dbContext, clientService, tokenService, options);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"oauth2-authz-tests-{Guid.NewGuid()}")
            .Options;

        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static string BuildCodeChallenge(string verifier)
    {
        var verifierHash = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        return Convert.ToBase64String(verifierHash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed class FakeClientService(bool requirePkce) : IOAuth2ClientService
    {
        public Task<OAuth2Client?> GetClientAsync(string clientId, CancellationToken ct = default)
        {
            if (clientId != "lcdpc-web")
            {
                return Task.FromResult<OAuth2Client?>(null);
            }

            return Task.FromResult<OAuth2Client?>(new OAuth2Client
            {
                ClientId = "lcdpc-web",
                ClientName = "LCDPC Web SPA",
                RedirectUris = ["http://localhost:4200/auth/callback"],
                GrantTypes = ["authorization_code", "refresh_token"],
                RequirePkce = requirePkce,
                AllowedScopes = "openid email profile",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        public bool ValidateRedirectUri(OAuth2Client client, string redirectUri)
            => client.RedirectUris.Contains(redirectUri, StringComparer.Ordinal);

        public bool ValidateScopes(OAuth2Client client, string requestedScopes)
            => true;
    }

    private sealed class FakeTokenService : IOAuth2TokenService
    {
        private int _counter;

        public Task<string> GenerateAccessTokenAsync(Guid userId, string clientId, string scope, CancellationToken ct = default)
            => Task.FromResult("fake-access-token");

        public Task<(string token, string hash)> GenerateRefreshTokenAsync(CancellationToken ct = default)
        {
            _counter++;
            var token = $"new-refresh-token-{_counter}";
            return Task.FromResult((token, TokenHashing.Hash(token)));
        }

        public Task<bool> ValidateAccessTokenAsync(string token, CancellationToken ct = default)
            => Task.FromResult(true);
    }
}
