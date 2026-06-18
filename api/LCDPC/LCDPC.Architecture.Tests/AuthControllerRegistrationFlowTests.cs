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

public class AuthControllerRegistrationFlowTests
{
    [Fact]
    public async Task StartRegistration_ReturnsAccepted_WhenEmailIsValid()
    {
        var flowId = Guid.NewGuid();
        var fakeService = new FakeRegistrationFlowService
        {
            OnStartAsync = _ => Task.FromResult(new StartRegistrationResponse(
                flowId,
                "pending_email_verification",
                new OtpPolicyResponse(10, 5, 10)))
        };
        var controller = CreateController(fakeService);

        var result = await controller.StartRegistration(new StartRegistrationRequest("cliente@example.com"), CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var payload = Assert.IsType<StartRegistrationResponse>(accepted.Value);
        Assert.Equal(flowId, payload.FlowId);
        Assert.Equal("pending_email_verification", payload.Status);
    }

    [Fact]
    public async Task StartRegistration_ReturnsConflict_WhenEmailAlreadyExists()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnStartAsync = _ => throw new InvalidOperationException("EMAIL_ALREADY_REGISTERED")
        };
        var controller = CreateController(fakeService);

        var result = await controller.StartRegistration(new StartRegistrationRequest("cliente@example.com"), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task StartRegistration_ReturnsTooManyRequests_WhenOtpCooldownIsActive()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnStartAsync = _ => throw new InvalidOperationException("OTP_COOLDOWN_ACTIVE")
        };
        var controller = CreateController(fakeService);

        var result = await controller.StartRegistration(new StartRegistrationRequest("cliente@example.com"), CancellationToken.None);

        var tooManyRequests = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, tooManyRequests.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_ReturnsOk_WhenOtpIsValid()
    {
        var flowId = Guid.NewGuid();
        var fakeService = new FakeRegistrationFlowService
        {
            OnVerifyEmailAsync = (_, _) => Task.FromResult(new VerifyEmailRegistrationResponse(flowId, "pending_profile"))
        };
        var controller = CreateController(fakeService);

        var result = await controller.VerifyEmail(new VerifyEmailRegistrationRequest(flowId, "123456"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<VerifyEmailRegistrationResponse>(ok.Value);
        Assert.Equal(flowId, payload.FlowId);
        Assert.Equal("pending_profile", payload.Status);
    }

    [Fact]
    public async Task CompleteProfile_ReturnsCreated_WhenRequestIsValid()
    {
        var flowId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fakeService = new FakeRegistrationFlowService
        {
            OnCompleteProfileAsync = _ => Task.FromResult(new CompleteProfileRegistrationResponse(userId, "activo", "cliente"))
        };
        var controller = CreateController(fakeService);

        var result = await controller.CompleteProfile(
            new CompleteProfileRegistrationRequest(
                flowId,
                "Ana",
                "Perez",
                "V12345678",
                null,
                "+584121234567",
                "Caracas, Venezuela",
                "secret123"),
            CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        var payload = Assert.IsType<CompleteProfileRegistrationResponse>(created.Value);
        Assert.Equal(userId, payload.UsuarioId);
        Assert.Equal("activo", payload.Estado);
        Assert.Equal("cliente", payload.TipoCuenta);
    }

    [Fact]
    public async Task CompleteProfile_ReturnsBadRequest_WhenPasswordIsMissing()
    {
        var controller = CreateController(new FakeRegistrationFlowService());

        var result = await controller.CompleteProfile(
            new CompleteProfileRegistrationRequest(
                Guid.NewGuid(),
                "Ana",
                "Perez",
                "V12345678",
                null,
                "+584121234567",
                "Caracas, Venezuela",
                string.Empty),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
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
            .UseInMemoryDatabase(databaseName: $"lcdpc_reg_test_{Guid.NewGuid()}")
            .Options;
        var dbContext = new AppDbContext(options);

        var tokenService = new FakeOAuth2TokenService();
        var clientService = new FakeOAuth2ClientService();
        var authorizationService = new FakeOAuth2AuthorizationService();

        return new AuthController(
            service,
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
            => Task.FromResult(new OAuth2RevokeResponse(Success: true));
        public Task<OAuth2IntrospectResponse> IntrospectTokenAsync(string token, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeRegistrationFlowService : IRegistrationFlowService
    {
        public Func<string, Task<StartRegistrationResponse>>? OnStartAsync { get; set; }
        public Func<Guid, string, Task<VerifyEmailRegistrationResponse>>? OnVerifyEmailAsync { get; set; }
        public Func<CompleteProfileRegistrationRequest, Task<CompleteProfileRegistrationResponse>>? OnCompleteProfileAsync { get; set; }

        public Task<StartRegistrationResponse> StartAsync(string email, CancellationToken cancellationToken = default)
        {
            if (OnStartAsync is null)
            {
                throw new NotImplementedException();
            }

            return OnStartAsync(email);
        }

        public Task<VerifyEmailRegistrationResponse> VerifyEmailAsync(Guid flowId, string otp, CancellationToken cancellationToken = default)
        {
            if (OnVerifyEmailAsync is null)
            {
                throw new NotImplementedException();
            }

            return OnVerifyEmailAsync(flowId, otp);
        }

        public Task<CompleteProfileRegistrationResponse> CompleteProfileAsync(CompleteProfileRegistrationRequest request, CancellationToken cancellationToken = default)
        {
            if (OnCompleteProfileAsync is null)
            {
                throw new NotImplementedException();
            }

            return OnCompleteProfileAsync(request);
        }

        public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MeResponse> MeAsync(string? accessToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RefreshSessionResponse> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AuthSecurityPolicyResponse> GetSecurityPolicyAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AuthSecurityPolicyResponse> UpdateSecurityPolicyAsync(UpdateAuthSecurityPolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GoogleRegisterStartResponse> StartGoogleRegistrationAsync(string state, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GoogleRegisterCallbackResponse> CompleteGoogleRegistrationAsync(string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
