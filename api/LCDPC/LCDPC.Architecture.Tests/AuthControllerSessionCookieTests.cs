using System.Security.Cryptography;
using System.Text;
using LCDPC.API.Controllers;
using LCDPC.Application.OAuth2;
using LCDPC.Application.Users.Auth;
using LCDPC.Domain.Entities.OAuth2;
using LCDPC.Domain.Entities.Users;
using LCDPC.Infrastructure.OAuth2;
using LCDPC.Infrastructure.Persistence;
using LCDPC.Infrastructure.Users.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.Architecture.Tests;

public class AuthControllerSessionCookieTests
{
    private const string TestUserId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
    private const string TestEmail = "admin@example.com";
    private const string TestPassword = "TestP@ssw0rd!";

    [Fact]
    public async Task Login_SetsSessionCookies_WithExpectedPolicy()
    {
        await using var ctx = CreateTestDbContext();
        await SeedTestUserAsync(ctx);

        var controller = CreateController(ctx);

        var result = await controller.Login(new LoginRequest(TestEmail, TestPassword), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<LoginResponse>(ok.Value);

        var setCookies = controller.Response.Headers.SetCookie;
        Assert.Equal(2, setCookies.Count);

        var accessCookie = setCookies.SingleOrDefault(x => x is not null && x.StartsWith("lcdpc_at=", StringComparison.Ordinal));
        var refreshCookie = setCookies.SingleOrDefault(x => x is not null && x.StartsWith("lcdpc_rt=", StringComparison.Ordinal));

        Assert.NotNull(accessCookie);
        Assert.NotNull(refreshCookie);
        var accessCookieValue = accessCookie!;
        var refreshCookieValue = refreshCookie!;

        Assert.Contains("httponly", accessCookieValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", accessCookieValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", accessCookieValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", accessCookieValue, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("httponly", refreshCookieValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", refreshCookieValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", refreshCookieValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", refreshCookieValue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenCredentialsAreInvalid()
    {
        await using var ctx = CreateTestDbContext();
        await SeedTestUserAsync(ctx);

        var controller = CreateController(ctx);

        var result = await controller.Login(new LoginRequest(TestEmail, "wrong-password"), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        Assert.Equal(0, controller.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public async Task Login_ReturnsDeprecationHeaders()
    {
        await using var ctx = CreateTestDbContext();
        await SeedTestUserAsync(ctx);

        var controller = CreateController(ctx);
        await controller.Login(new LoginRequest(TestEmail, TestPassword), CancellationToken.None);

        Assert.Equal("true", controller.Response.Headers["Deprecation"].ToString());
        Assert.False(string.IsNullOrEmpty(controller.Response.Headers["Sunset"].ToString()));
        Assert.Contains("successor-version", controller.Response.Headers["Link"].ToString());
    }

    [Fact]
    public async Task Logout_DeletesSessionCookies_WithExpectedPaths()
    {
        await using var ctx = CreateTestDbContext();
        var controller = CreateController(ctx);
        controller.ControllerContext.HttpContext.Request.Headers.Cookie = "lcdpc_rt=some-refresh-token";

        var result = await controller.Logout(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);

        var setCookies = controller.Response.Headers.SetCookie;
        Assert.Equal(2, setCookies.Count);

        var accessCookie = setCookies.SingleOrDefault(x => x is not null && x.StartsWith("lcdpc_at=", StringComparison.Ordinal));
        var refreshCookie = setCookies.SingleOrDefault(x => x is not null && x.StartsWith("lcdpc_rt=", StringComparison.Ordinal));

        Assert.NotNull(accessCookie);
        Assert.NotNull(refreshCookie);
        var accessCookieValue = accessCookie!;
        var refreshCookieValue = refreshCookie!;

        Assert.Contains("path=/", accessCookieValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", refreshCookieValue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_ReturnsDeprecationHeaders()
    {
        await using var ctx = CreateTestDbContext();
        var controller = CreateController(ctx);
        controller.ControllerContext.HttpContext.Request.Headers.Cookie = "lcdpc_rt=some-refresh-token";

        await controller.Logout(CancellationToken.None);

        Assert.Equal("true", controller.Response.Headers["Deprecation"].ToString());
        Assert.False(string.IsNullOrEmpty(controller.Response.Headers["Sunset"].ToString()));
    }

    [Fact]
    public async Task Refresh_ReturnsDeprecationHeaders()
    {
        await using var ctx = CreateTestDbContext();
        var controller = CreateController(ctx);

        // Seed an OAuth2 refresh token for the test user
        var (refreshToken, refreshHash) = GenerateTestRefreshToken();

        ctx.OAuth2RefreshTokens.Add(new OAuth2RefreshToken
        {
            TokenHash = refreshHash,
            ClientId = "lcdpc-web",
            UserId = Guid.Parse(TestUserId),
            Scope = "openid email profile",
            FamilyId = Guid.NewGuid(),
            PreviousTokenHash = null,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            RevokedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        controller.ControllerContext.HttpContext.Request.Headers.Cookie = $"lcdpc_rt={refreshToken}";
        await controller.Refresh(CancellationToken.None);

        Assert.Equal("true", controller.Response.Headers["Deprecation"].ToString());
        Assert.False(string.IsNullOrEmpty(controller.Response.Headers["Sunset"].ToString()));
    }

    [Fact]
    public async Task ForgotPassword_ReturnsBadRequest_WhenEmailIsInvalid()
    {
        await using var ctx = CreateTestDbContext();
        var controller = CreateController(ctx);

        var result = await controller.ForgotPassword(new ForgotPasswordRequest("not-an-email"), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsAccepted_WhenEmailIsValid()
    {
        await using var ctx = CreateTestDbContext();
        var controller = CreateController(ctx);

        var result = await controller.ForgotPassword(new ForgotPasswordRequest("user@example.com"), CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ClearsSessionCookies_WhenResetIsSuccessful()
    {
        await using var ctx = CreateTestDbContext();
        var controller = CreateController(ctx);

        // Seed a valid reset token
        var resetToken = "valid-reset-token";
        ctx.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse(TestUserId),
            TokenHash = TokenHashing.Hash(resetToken),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
            UsedAtUtc = null,
            RevokedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var result = await controller.ResetPassword(new ResetPasswordRequest(resetToken, "new-secret"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ResetPasswordResponse>(ok.Value);
        Assert.Equal("completed", payload.Status);

        var setCookies = controller.Response.Headers.SetCookie;
        Assert.Equal(2, setCookies.Count);
        Assert.Contains(setCookies, cookie => cookie is not null && cookie.StartsWith("lcdpc_at=", StringComparison.Ordinal));
        Assert.Contains(setCookies, cookie => cookie is not null && cookie.StartsWith("lcdpc_rt=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResetPassword_ReturnsUnauthorized_WhenTokenIsInvalidOrExpired()
    {
        await using var ctx = CreateTestDbContext();
        var controller = CreateController(ctx);

        var result = await controller.ResetPassword(new ResetPasswordRequest("expired-token", "new-secret"), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsDeprecationHeaders()
    {
        await using var ctx = CreateTestDbContext();
        var controller = CreateController(ctx);

        await controller.Me(CancellationToken.None);

        Assert.Equal("true", controller.Response.Headers["Deprecation"].ToString());
        Assert.False(string.IsNullOrEmpty(controller.Response.Headers["Sunset"].ToString()));
    }

    // ──────────────────────────────────────────────
    // Test infrastructure
    // ──────────────────────────────────────────────

    private static AppDbContext CreateTestDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"lcdpc_test_{Guid.NewGuid()}")
            .Options;

        var ctx = new AppDbContext(options);

        // Seed minimal required data
        var adminRole = new Role
        {
            Id = Guid.NewGuid(),
            Code = "admin_global",
            Name = "Administrador Global",
            Description = "Full admin access"
        };
        var clientRole = new Role
        {
            Id = Guid.NewGuid(),
            Code = "cliente",
            Name = "Cliente",
            Description = "Standard customer"
        };
        ctx.Roles.AddRange(adminRole, clientRole);

        // Seed OAuth2 client
        var oauth2Client = new OAuth2Client
        {
            ClientId = "lcdpc-web",
            ClientName = "LCDPC Web SPA",
            RedirectUris = ["http://localhost:4200", "http://localhost:4200/auth/callback"],
            GrantTypes = ["authorization_code", "refresh_token"],
            AllowedScopes = "openid email profile admin admin:sedes admin:users",
            RequirePkce = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        ctx.OAuth2Clients.Add(oauth2Client);

        ctx.SaveChanges();
        return ctx;
    }

    private static async Task SeedTestUserAsync(AppDbContext ctx)
    {
        var adminRole = await ctx.Roles.FirstAsync(r => r.Code == "admin_global");

        var passwordHash = HashPassword(TestPassword);

        var user = new User
        {
            Id = Guid.Parse(TestUserId),
            Email = TestEmail,
            PasswordHash = passwordHash,
            EmailVerifiedAtUtc = DateTime.UtcNow,
            OnboardingStatus = "active",
            Status = UserStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };

        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FirstName = "Admin",
            LastName = "Global",
            IdentityDocument = "12345678",
            WhatsAppPhone = "+584141234567",
            FullAddress = "Test Address",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var assignment = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = adminRole.Id,
            Active = true,
            SedeIds = Array.Empty<Guid>(),
            CreatedAtUtc = DateTime.UtcNow
        };

        ctx.Users.Add(user);
        ctx.Profiles.Add(profile);
        ctx.UserRoleAssignments.Add(assignment);
        await ctx.SaveChangesAsync();
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2$100000$SHA256${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static (string token, string hash) GenerateTestRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var hash = TokenHashing.Hash(token);
        return (token, hash);
    }

    private static AuthController CreateController(AppDbContext dbContext)
    {
        var oauth2Options = new OAuth2Options
        {
            Issuer = "http://localhost:8080",
            Audience = "lcdpc-api",
            AccessTokenTtlMinutes = 60,
            RefreshTokenTtlDays = 30,
            AuthorizationCodeTtlMinutes = 10,
            Clients = []
        };

        var tokenService = new FakeOAuth2TokenService();
        var clientService = new FakeOAuth2ClientService();
        var authorizationService = new FakeOAuth2AuthorizationService(tokenService, dbContext, oauth2Options);

        var registrationFlowService = new FakeRegistrationFlowService();

        return new AuthController(
            registrationFlowService,
            authorizationService,
            tokenService,
            clientService,
            dbContext,
            oauth2Options)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    // ──────────────────────────────────────────────
    // Fake implementations
    // ──────────────────────────────────────────────

    private sealed class FakeOAuth2TokenService : IOAuth2TokenService
    {
        public Task<string> GenerateAccessTokenAsync(Guid userId, string clientId, string scope, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"sub\":\"{userId}\",\"client_id\":\"{clientId}\",\"scope\":\"{scope}\",\"iat\":{new DateTimeOffset(now).ToUnixTimeSeconds()},\"exp\":{new DateTimeOffset(now.AddMinutes(60)).ToUnixTimeSeconds()}}}"));
            var signature = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            return Task.FromResult($"{header}.{payload}.{signature}");
        }

        public Task<(string token, string hash)> GenerateRefreshTokenAsync(CancellationToken ct = default)
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            var hash = TokenHashing.Hash(token);
            return Task.FromResult((token, hash));
        }

        public Task<bool> ValidateAccessTokenAsync(string token, CancellationToken ct = default)
        {
            return Task.FromResult(!string.IsNullOrEmpty(token));
        }
    }

    private sealed class FakeOAuth2ClientService : IOAuth2ClientService
    {
        public Task<OAuth2Client?> GetClientAsync(string clientId, CancellationToken ct = default)
        {
            if (clientId == "lcdpc-web")
            {
                return Task.FromResult<OAuth2Client?>(new OAuth2Client
                {
                    ClientId = "lcdpc-web",
                    ClientName = "LCDPC Web SPA",
                    RedirectUris = ["http://localhost:4200"],
                    GrantTypes = ["authorization_code", "refresh_token"],
                    AllowedScopes = "openid email profile admin admin:sedes admin:users",
                    RequirePkce = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            return Task.FromResult<OAuth2Client?>(null);
        }

        public bool ValidateRedirectUri(OAuth2Client client, string redirectUri) => true;
        public bool ValidateScopes(OAuth2Client client, string requestedScopes) => true;
    }

    private sealed class FakeOAuth2AuthorizationService(
        FakeOAuth2TokenService tokenService,
        AppDbContext dbContext,
        OAuth2Options options) : IOAuth2AuthorizationService
    {
        public Task<OAuth2AuthorizeResult> AuthorizeAsync(OAuth2AuthorizeRequest request, Guid? userId, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<OAuth2TokenResponse?> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, string clientId, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public async Task<OAuth2TokenResponse?> RefreshTokenAsync(string refreshToken, string clientId, CancellationToken ct = default)
        {
            var hash = TokenHashing.Hash(refreshToken);
            var existingToken = await dbContext.OAuth2RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);

            if (existingToken is null || existingToken.ExpiresAtUtc <= DateTime.UtcNow || existingToken.RevokedAtUtc.HasValue)
            {
                return null;
            }

            var accessToken = await tokenService.GenerateAccessTokenAsync(
                existingToken.UserId, clientId, existingToken.Scope, ct);
            var (newRefreshToken, newRefreshHash) = await tokenService.GenerateRefreshTokenAsync(ct);

            var nowUtc = DateTime.UtcNow;
            dbContext.OAuth2RefreshTokens.Add(new OAuth2RefreshToken
            {
                TokenHash = newRefreshHash,
                ClientId = clientId,
                UserId = existingToken.UserId,
                Scope = existingToken.Scope,
                FamilyId = existingToken.FamilyId,
                PreviousTokenHash = hash,
                ExpiresAtUtc = nowUtc.AddDays(options.RefreshTokenTtlDays),
                RevokedAtUtc = null,
                CreatedAtUtc = nowUtc
            });

            existingToken.RevokedAtUtc = nowUtc;
            await dbContext.SaveChangesAsync(ct);

            return new OAuth2TokenResponse(
                AccessToken: accessToken,
                TokenType: "Bearer",
                ExpiresIn: options.AccessTokenTtlMinutes * 60,
                RefreshToken: newRefreshToken,
                Scope: existingToken.Scope);
        }

        public async Task<OAuth2RevokeResponse> RevokeTokenAsync(string token, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return new OAuth2RevokeResponse(Success: true);
            }

            var hash = TokenHashing.Hash(token);
            var refreshToken = await dbContext.OAuth2RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);

            if (refreshToken is not null)
            {
                var nowUtc = DateTime.UtcNow;
                await dbContext.OAuth2RefreshTokens
                    .Where(rt => rt.FamilyId == refreshToken.FamilyId && !rt.RevokedAtUtc.HasValue)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(rt => rt.RevokedAtUtc, nowUtc),
                    ct);
            }

            return new OAuth2RevokeResponse(Success: true);
        }

        public Task<OAuth2IntrospectResponse> IntrospectTokenAsync(string token, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeRegistrationFlowService : IRegistrationFlowService
    {
        public Task<StartRegistrationResponse> StartAsync(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<VerifyEmailRegistrationResponse> VerifyEmailAsync(Guid flowId, string otp, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CompleteProfileRegistrationResponse> CompleteProfileAsync(CompleteProfileRegistrationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MeResponse> MeAsync(string? accessToken, CancellationToken cancellationToken = default) => Task.FromResult(new MeResponse(false, null, Array.Empty<ResourcePermissionResponse>(), null));
        public Task<RefreshSessionResponse> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ForgotPasswordResponse(
                "accepted",
                "if the account exists, a reset instruction has been generated",
                null));
        }

        public Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ResetPasswordResponse("completed", true));
        }
        public Task<AuthSecurityPolicyResponse> GetSecurityPolicyAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AuthSecurityPolicyResponse> UpdateSecurityPolicyAsync(UpdateAuthSecurityPolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GoogleRegisterStartResponse> StartGoogleRegistrationAsync(string state, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GoogleRegisterCallbackResponse> CompleteGoogleRegistrationAsync(string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
