using LCDPC.API.Controllers;
using LCDPC.Application.OAuth2;
using LCDPC.Application.Users.Auth;
using LCDPC.Domain.Entities.OAuth2;
using LCDPC.Infrastructure.OAuth2;
using LCDPC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.Architecture.Tests;

public class AuthControllerGoogleTests
{
    [Fact]
    public async Task StartGoogleRegistration_ReturnsRedirectToProvider()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnStartGoogleRegistrationAsync = state => Task.FromResult(new GoogleRegisterStartResponse(
                $"https://accounts.google.com/o/oauth2/v2/auth?state={state}",
                state,
                600))
        };

        var controller = CreateController(fakeService);

        var result = await controller.StartGoogleRegistration(CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?state=", redirect.Url);
    }

    [Fact]
    public async Task CompleteGoogleRegistration_ReturnsBadRequest_WhenStateDoesNotMatch()
    {
        var fakeService = new FakeRegistrationFlowService();
        var controller = CreateController(fakeService);
        controller.ControllerContext.HttpContext.Request.Headers.Cookie = "lcdpc_google_state=expected-state";

        var result = await controller.CompleteGoogleRegistration("code-123", "wrong-state", CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task CompleteGoogleRegistration_ReturnsOk_WhenStateAndCodeAreValid()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnCompleteGoogleRegistrationAsync = code => Task.FromResult(new GoogleRegisterCallbackResponse(
                Guid.NewGuid(),
                "pending_profile",
                new GooglePrefillResponse(
                    "test@gmail.com",
                    true,
                    "Test User",
                    "Test",
                    "User",
                    "https://example.com/pic.jpg",
                    "es-419")))
        };

        var controller = CreateController(fakeService);
        controller.ControllerContext.HttpContext.Request.Headers.Cookie = "lcdpc_google_state=ok-state";

        var result = await controller.CompleteGoogleRegistration("code-123", "ok-state", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<GoogleRegisterCallbackResponse>(ok.Value);
        Assert.Equal("pending_profile", payload.Status);
        Assert.Equal("test@gmail.com", payload.Prefill.Email);
    }

    private static AuthController CreateController(IRegistrationFlowService service)
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

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"lcdpc_google_test_{Guid.NewGuid()}")
            .Options;
        var dbContext = new AppDbContext(options);

        return new AuthController(
            service,
            new FakeOAuth2AuthorizationService(),
            new FakeOAuth2TokenService(),
            new FakeOAuth2ClientService(),
            dbContext,
            oauth2Options)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private sealed class FakeRegistrationFlowService : IRegistrationFlowService
    {
        public Func<string, Task<GoogleRegisterStartResponse>>? OnStartGoogleRegistrationAsync { get; set; }
        public Func<string, Task<GoogleRegisterCallbackResponse>>? OnCompleteGoogleRegistrationAsync { get; set; }

        public Task<StartRegistrationResponse> StartAsync(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<VerifyEmailRegistrationResponse> VerifyEmailAsync(Guid flowId, string otp, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CompleteProfileRegistrationResponse> CompleteProfileAsync(CompleteProfileRegistrationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MeResponse> MeAsync(string? accessToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RefreshSessionResponse> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AuthSecurityPolicyResponse> GetSecurityPolicyAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AuthSecurityPolicyResponse> UpdateSecurityPolicyAsync(UpdateAuthSecurityPolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<GoogleRegisterStartResponse> StartGoogleRegistrationAsync(string state, CancellationToken cancellationToken = default)
        {
            if (OnStartGoogleRegistrationAsync is null)
            {
                throw new NotImplementedException();
            }

            return OnStartGoogleRegistrationAsync(state);
        }

        public Task<GoogleRegisterCallbackResponse> CompleteGoogleRegistrationAsync(string code, CancellationToken cancellationToken = default)
        {
            if (OnCompleteGoogleRegistrationAsync is null)
            {
                throw new NotImplementedException();
            }

            return OnCompleteGoogleRegistrationAsync(code);
        }
    }

    private sealed class FakeOAuth2TokenService : IOAuth2TokenService
    {
        public Task<string> GenerateAccessTokenAsync(Guid userId, string clientId, string scope, CancellationToken ct = default)
            => Task.FromResult("fake-access-token");
        public Task<(string token, string hash)> GenerateRefreshTokenAsync(CancellationToken ct = default)
            => Task.FromResult(("fake-refresh-token", "fake-hash"));
        public Task<bool> ValidateAccessTokenAsync(string token, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    private sealed class FakeOAuth2ClientService : IOAuth2ClientService
    {
        public Task<OAuth2Client?> GetClientAsync(string clientId, CancellationToken ct = default)
            => Task.FromResult<OAuth2Client?>(null);
        public bool ValidateRedirectUri(OAuth2Client client, string redirectUri) => true;
        public bool ValidateScopes(OAuth2Client client, string requestedScopes) => true;
    }

    private sealed class FakeOAuth2AuthorizationService : IOAuth2AuthorizationService
    {
        public Task<OAuth2AuthorizeResult> AuthorizeAsync(OAuth2AuthorizeRequest request, Guid? userId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<OAuth2TokenResponse?> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, string clientId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<OAuth2TokenResponse?> RefreshTokenAsync(string refreshToken, string clientId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<OAuth2RevokeResponse> RevokeTokenAsync(string token, CancellationToken ct = default)
            => Task.FromResult(new OAuth2RevokeResponse(true));
        public Task<OAuth2IntrospectResponse> IntrospectTokenAsync(string token, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
