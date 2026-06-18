using LCDPC.API.Controllers;
using LCDPC.Application.OAuth2;
using LCDPC.Application.Users.Auth;
using LCDPC.Domain.Entities.OAuth2;
using LCDPC.Domain.Entities.Users;
using LCDPC.Infrastructure.OAuth2;
using LCDPC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LCDPC.Architecture.Tests;

public class AuthControllerCompatibilityIntegrationTests
{
    [Fact]
    public async Task Login_WithSeededInitialAdmin_ReturnsAdminRoleAndPermissions()
    {
        await using var dbContext = CreateDbContext();
        var options = CreateOptions();
        await SeedClientAsync(dbContext);

        var adminSeed = new SuperUserSeeder.SuperUserSeedOptions(
            Alias: "admin_seed_test",
            Email: "admin.seed@lcdpc.local",
            FirstName: "Admin",
            LastName: "Seed",
            IdentityDocument: "V12345678",
            WhatsAppPhone: "04120000000",
            FullAddress: "Caracas");

        await SuperUserSeeder.SeedAsync(dbContext, "AdminSeedP@ss1", adminSeed, CancellationToken.None);

        using var keyService = new RsaKeyService(options, NullLogger<RsaKeyService>.Instance);
        var tokenService = new OAuth2TokenService(dbContext, keyService, options);
        var clientService = new OAuth2ClientService(dbContext);
        var authorizationService = new OAuth2AuthorizationService(dbContext, clientService, tokenService, options);

        var controller = new AuthController(
            new FakeRegistrationFlowService(),
            authorizationService,
            tokenService,
            clientService,
            dbContext,
            options)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var loginResult = await controller.Login(new LoginRequest(adminSeed.Email, "AdminSeedP@ss1"), CancellationToken.None);
        var loginOk = Assert.IsType<OkObjectResult>(loginResult);
        var loginPayload = Assert.IsType<LoginResponse>(loginOk.Value);

        Assert.Equal("administrador", loginPayload.UserSummary.TipoCuenta);
        Assert.Contains("admin_global", loginPayload.UserSummary.Roles);
        Assert.NotEmpty(loginPayload.Permissions);
    }

    [Fact]
    public async Task LoginRefreshLogout_CompatibilityFlow_Works()
    {
        await using var dbContext = CreateDbContext();
        var options = CreateOptions();
        await SeedClientAsync(dbContext);
        await SeedUserAsync(dbContext);

        using var keyService = new RsaKeyService(options, NullLogger<RsaKeyService>.Instance);
        var tokenService = new OAuth2TokenService(dbContext, keyService, options);
        var clientService = new OAuth2ClientService(dbContext);
        var authorizationService = new OAuth2AuthorizationService(dbContext, clientService, tokenService, options);

        var controller = new AuthController(
            new FakeRegistrationFlowService(),
            authorizationService,
            tokenService,
            clientService,
            dbContext,
            options)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var loginResult = await controller.Login(new LoginRequest("compat@example.com", TestPassword), CancellationToken.None);
        var loginOk = Assert.IsType<OkObjectResult>(loginResult);
        var loginPayload = Assert.IsType<LoginResponse>(loginOk.Value);
        Assert.False(string.IsNullOrWhiteSpace(loginPayload.TokenPair.RefreshToken));

        var refreshToken = ExtractCookieValue(controller.Response.Headers.SetCookie, "lcdpc_rt");
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));

        controller.ControllerContext.HttpContext = new DefaultHttpContext();
        controller.ControllerContext.HttpContext.Request.Headers.Cookie = $"lcdpc_rt={refreshToken}";
        var refreshResult = await controller.Refresh(CancellationToken.None);
        var refreshOk = Assert.IsType<OkObjectResult>(refreshResult);
        var refreshPayload = Assert.IsType<RefreshSessionResponse>(refreshOk.Value);
        Assert.False(string.IsNullOrWhiteSpace(refreshPayload.TokenPair.RefreshToken));

        var rotatedRefreshToken = ExtractCookieValue(controller.Response.Headers.SetCookie, "lcdpc_rt");
        Assert.False(string.IsNullOrWhiteSpace(rotatedRefreshToken));

        controller.ControllerContext.HttpContext = new DefaultHttpContext();
        controller.ControllerContext.HttpContext.Request.Headers.Cookie = $"lcdpc_rt={rotatedRefreshToken}";
        var logoutResult = await controller.Logout(CancellationToken.None);
        Assert.IsType<NoContentResult>(logoutResult);

        var revokedTokens = await dbContext.OAuth2RefreshTokens
            .Where(x => x.UserId == Guid.Parse(TestUserId))
            .ToListAsync();
        Assert.NotEmpty(revokedTokens);
        Assert.All(revokedTokens, token => Assert.NotNull(token.RevokedAtUtc));
    }

    private static string ExtractCookieValue(Microsoft.Extensions.Primitives.StringValues setCookies, string cookieName)
    {
        var header = setCookies.First(x => x is not null && x.StartsWith(cookieName + "=", StringComparison.Ordinal));
        Assert.NotNull(header);
        var pair = header.Split(';', 2)[0];
        return pair[(cookieName.Length + 1)..];
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"auth-compat-int-tests-{Guid.NewGuid()}")
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
            RedirectUris = ["http://localhost:4200", "http://localhost:4200/auth/callback"],
            GrantTypes = ["authorization_code", "refresh_token"],
            RequirePkce = true,
            AllowedScopes = "openid email profile admin admin:sedes admin:users",
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedUserAsync(AppDbContext dbContext)
    {
        var roleId = Guid.NewGuid();
        dbContext.Roles.Add(new Role
        {
            Id = roleId,
            Code = "admin_global",
            Name = "Administrador Global",
            Description = "Compatibility test role"
        });

        var userId = Guid.Parse(TestUserId);
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "compat@example.com",
            PasswordHash = HashPassword(TestPassword),
            EmailVerifiedAtUtc = DateTime.UtcNow,
            OnboardingStatus = "active",
            Status = UserStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        });

        dbContext.Profiles.Add(new Profile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FirstName = "Compat",
            LastName = "User",
            IdentityDocument = "V1234567",
            WhatsAppPhone = "+584121234567",
            FullAddress = "Caracas",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        dbContext.UserRoleAssignments.Add(new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            Active = true,
            SedeIds = [],
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private static string HashPassword(string password)
    {
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var hash = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        return $"PBKDF2$100000$SHA256${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private sealed class FakeRegistrationFlowService : IRegistrationFlowService
    {
        public Task<StartRegistrationResponse> StartAsync(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<VerifyEmailRegistrationResponse> VerifyEmailAsync(Guid flowId, string otp, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CompleteProfileRegistrationResponse> CompleteProfileAsync(CompleteProfileRegistrationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MeResponse> MeAsync(string? accessToken, CancellationToken cancellationToken = default) => Task.FromResult(new MeResponse(false, null, [], null));
        public Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AuthSecurityPolicyResponse> GetSecurityPolicyAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AuthSecurityPolicyResponse> UpdateSecurityPolicyAsync(UpdateAuthSecurityPolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GoogleRegisterStartResponse> StartGoogleRegistrationAsync(string state, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GoogleRegisterCallbackResponse> CompleteGoogleRegistrationAsync(string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private const string TestUserId = "d4b1724a-4d8f-4f0b-9d72-5bd5458bf4de";
    private const string TestPassword = "TestP@ssw0rd!";
}
