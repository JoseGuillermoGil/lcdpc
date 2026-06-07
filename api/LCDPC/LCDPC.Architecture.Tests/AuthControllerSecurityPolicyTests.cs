using LCDPC.API.Controllers;
using LCDPC.Application.Users.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace LCDPC.Architecture.Tests;

public class AuthControllerSecurityPolicyTests
{
    [Fact]
    public async Task GetSecurityPolicy_ReturnsCurrentPolicy()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnGetSecurityPolicyAsync = () => Task.FromResult(new AuthSecurityPolicyResponse(45, false))
        };
        var controller = CreateController(fakeService);

        var result = await controller.GetSecurityPolicy(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AuthSecurityPolicyResponse>(ok.Value);
        Assert.Equal(45, payload.PasswordResetTtlMinutes);
        Assert.False(payload.RevokeSessionsOnPasswordReset);
    }

    [Fact]
    public async Task UpdateSecurityPolicy_ReturnsOk_WhenRequestIsValid()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnUpdateSecurityPolicyAsync = request => Task.FromResult(
                new AuthSecurityPolicyResponse(request.PasswordResetTtlMinutes, request.RevokeSessionsOnPasswordReset))
        };
        var controller = CreateController(fakeService);

        var result = await controller.UpdateSecurityPolicy(
            new UpdateAuthSecurityPolicyRequest(60, true),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AuthSecurityPolicyResponse>(ok.Value);
        Assert.Equal(60, payload.PasswordResetTtlMinutes);
        Assert.True(payload.RevokeSessionsOnPasswordReset);
    }

    [Fact]
    public async Task UpdateSecurityPolicy_ReturnsBadRequest_WhenTtlIsOutOfRange()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnUpdateSecurityPolicyAsync = _ => throw new InvalidOperationException("INVALID_RESET_TTL")
        };
        var controller = CreateController(fakeService);

        var result = await controller.UpdateSecurityPolicy(
            new UpdateAuthSecurityPolicyRequest(1, true),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
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
        public Func<Task<AuthSecurityPolicyResponse>>? OnGetSecurityPolicyAsync { get; set; }
        public Func<UpdateAuthSecurityPolicyRequest, Task<AuthSecurityPolicyResponse>>? OnUpdateSecurityPolicyAsync { get; set; }

        public Task<AuthSecurityPolicyResponse> GetSecurityPolicyAsync(CancellationToken cancellationToken = default)
        {
            if (OnGetSecurityPolicyAsync is null)
            {
                throw new NotImplementedException();
            }

            return OnGetSecurityPolicyAsync();
        }

        public Task<AuthSecurityPolicyResponse> UpdateSecurityPolicyAsync(UpdateAuthSecurityPolicyRequest request, CancellationToken cancellationToken = default)
        {
            if (OnUpdateSecurityPolicyAsync is null)
            {
                throw new NotImplementedException();
            }

            return OnUpdateSecurityPolicyAsync(request);
        }

        public Task<StartRegistrationResponse> StartAsync(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<VerifyEmailRegistrationResponse> VerifyEmailAsync(Guid flowId, string otp, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CompleteProfileRegistrationResponse> CompleteProfileAsync(CompleteProfileRegistrationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MeResponse> MeAsync(string? accessToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RefreshSessionResponse> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GoogleRegisterStartResponse> StartGoogleRegistrationAsync(string state, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GoogleRegisterCallbackResponse> CompleteGoogleRegistrationAsync(string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
