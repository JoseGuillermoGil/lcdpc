using LCDPC.API.Controllers;
using LCDPC.Application.Users.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace LCDPC.Architecture.Tests;

public class AuthControllerOtpTests
{
    [Fact]
    public async Task StartRegistration_ReturnsOtpPolicy_5Attempts_10MinutesTtl_10MinutesCooldown()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnStartAsync = _ => Task.FromResult(new StartRegistrationResponse(
                Guid.NewGuid(),
                "pending_email_verification",
                new OtpPolicyResponse(10, 5, 10)))
        };
        var controller = CreateController(fakeService);

        var result = await controller.StartRegistration(new StartRegistrationRequest("user@example.com"), CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var payload = Assert.IsType<StartRegistrationResponse>(accepted.Value);
        Assert.Equal(10, payload.OtpPolicy.TtlMinutes);
        Assert.Equal(5, payload.OtpPolicy.MaxAttempts);
        Assert.Equal(10, payload.OtpPolicy.CooldownMinutes);
    }

    [Fact]
    public async Task VerifyEmail_ReturnsBadRequest_WhenOtpIsInvalid()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnVerifyEmailAsync = (_, _) => throw new InvalidOperationException("OTP_INVALID")
        };
        var controller = CreateController(fakeService);

        var result = await controller.VerifyEmail(
            new VerifyEmailRegistrationRequest(Guid.NewGuid(), "000000"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_ReturnsConflict_WhenOtpAttemptsAreExceeded()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnVerifyEmailAsync = (_, _) => throw new InvalidOperationException("OTP_ATTEMPTS_EXCEEDED")
        };
        var controller = CreateController(fakeService);

        var result = await controller.VerifyEmail(
            new VerifyEmailRegistrationRequest(Guid.NewGuid(), "000000"),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_ReturnsGone_WhenOtpIsExpired()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnVerifyEmailAsync = (_, _) => throw new InvalidOperationException("OTP_EXPIRED")
        };
        var controller = CreateController(fakeService);

        var result = await controller.VerifyEmail(
            new VerifyEmailRegistrationRequest(Guid.NewGuid(), "000000"),
            CancellationToken.None);

        var gone = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status410Gone, gone.StatusCode);
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
        public Func<string, Task<StartRegistrationResponse>>? OnStartAsync { get; set; }
        public Func<Guid, string, Task<VerifyEmailRegistrationResponse>>? OnVerifyEmailAsync { get; set; }

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

        public Task<CompleteProfileRegistrationResponse> CompleteProfileAsync(CompleteProfileRegistrationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
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
