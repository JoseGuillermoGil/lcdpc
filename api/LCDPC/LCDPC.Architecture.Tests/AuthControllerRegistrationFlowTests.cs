using LCDPC.API.Controllers;
using LCDPC.Application.Users.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

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
