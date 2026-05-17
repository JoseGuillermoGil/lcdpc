using LCDPC.API.Controllers;
using LCDPC.Application.Users.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:RefreshTokenTtlDays"] = "30"
            })
            .Build();

        return new AuthController(service, configuration)
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
}
