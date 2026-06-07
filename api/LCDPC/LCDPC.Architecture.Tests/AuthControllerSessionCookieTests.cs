using LCDPC.API.Controllers;
using LCDPC.Application.Users.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace LCDPC.Architecture.Tests;

public class AuthControllerSessionCookieTests
{
    [Fact]
    public async Task Login_SetsSessionCookies_WithExpectedPolicy()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnLoginAsync = _ => Task.FromResult(new LoginResponse(
                new TokenPairResponse("access-token", "refresh-token", 3600),
                new UserSummaryResponse(
                    Guid.NewGuid(),
                    "admin@example.com",
                    "Admin Global",
                    "activo",
                    "administrador",
                    "active",
                    DateTime.UtcNow,
                    ["admin_global"]),
                []))
        };

        var controller = CreateController(fakeService);

        var result = await controller.Login(new LoginRequest("admin@example.com", "secret"), CancellationToken.None);

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
    public async Task Logout_DeletesSessionCookies_WithExpectedPaths()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnLogoutAsync = _ => Task.CompletedTask
        };

        var controller = CreateController(fakeService);
        controller.ControllerContext.HttpContext.Request.Headers.Cookie = "lcdpc_rt=refresh-token";

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
    public async Task Login_ReturnsUnauthorized_WhenCredentialsAreInvalid()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnLoginAsync = _ => throw new InvalidOperationException("INVALID_CREDENTIALS")
        };

        var controller = CreateController(fakeService);

        var result = await controller.Login(new LoginRequest("admin@example.com", "wrong-password"), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        Assert.Equal(0, controller.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public async Task Refresh_SetsSessionCookies_WhenSessionIsValid()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnRefreshAsync = _ => Task.FromResult(new RefreshSessionResponse(
                new TokenPairResponse("new-access-token", "new-refresh-token", 1800),
                new UserSummaryResponse(
                    Guid.NewGuid(),
                    "admin@example.com",
                    "Admin Global",
                    "activo",
                    "administrador",
                    "active",
                    DateTime.UtcNow,
                    ["admin_global"]),
                []))
        };

        var controller = CreateController(fakeService);
        controller.ControllerContext.HttpContext.Request.Headers.Cookie = "lcdpc_rt=valid-refresh-token";

        var result = await controller.Refresh(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<RefreshSessionResponse>(ok.Value);

        var setCookies = controller.Response.Headers.SetCookie;
        Assert.Equal(2, setCookies.Count);

        var accessCookie = setCookies.SingleOrDefault(x => x is not null && x.StartsWith("lcdpc_at=", StringComparison.Ordinal));
        var refreshCookie = setCookies.SingleOrDefault(x => x is not null && x.StartsWith("lcdpc_rt=", StringComparison.Ordinal));

        Assert.NotNull(accessCookie);
        Assert.NotNull(refreshCookie);
    }

    [Fact]
    public async Task Refresh_ClearsSessionCookies_WhenSessionIsExpired()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnRefreshAsync = _ => throw new InvalidOperationException("INVALID_SESSION")
        };

        var controller = CreateController(fakeService);
        controller.ControllerContext.HttpContext.Request.Headers.Cookie = "lcdpc_rt=expired-refresh-token";

        var result = await controller.Refresh(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);

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
    public async Task ForgotPassword_ReturnsBadRequest_WhenEmailIsInvalid()
    {
        var controller = CreateController(new FakeRegistrationFlowService());

        var result = await controller.ForgotPassword(new ForgotPasswordRequest("not-an-email"), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsAccepted_WhenEmailIsValid()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnForgotPasswordAsync = _ => Task.FromResult(new ForgotPasswordResponse(
                "accepted",
                "If the email exists, reset instructions will be sent.",
                null))
        };
        var controller = CreateController(fakeService);

        var result = await controller.ForgotPassword(new ForgotPasswordRequest("user@example.com"), CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var payload = Assert.IsType<ForgotPasswordResponse>(accepted.Value);
        Assert.Equal("accepted", payload.Status);
    }

    [Fact]
    public async Task ResetPassword_ClearsSessionCookies_WhenResetIsSuccessful()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnResetPasswordAsync = _ => Task.FromResult(new ResetPasswordResponse("password_reset", true))
        };
        var controller = CreateController(fakeService);

        var result = await controller.ResetPassword(new ResetPasswordRequest("valid-token", "new-secret"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ResetPasswordResponse>(ok.Value);
        Assert.Equal("password_reset", payload.Status);

        var setCookies = controller.Response.Headers.SetCookie;
        Assert.Equal(2, setCookies.Count);
        Assert.Contains(setCookies, cookie => cookie is not null && cookie.StartsWith("lcdpc_at=", StringComparison.Ordinal));
        Assert.Contains(setCookies, cookie => cookie is not null && cookie.StartsWith("lcdpc_rt=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResetPassword_ReturnsUnauthorized_WhenTokenIsInvalidOrExpired()
    {
        var fakeService = new FakeRegistrationFlowService
        {
            OnResetPasswordAsync = _ => throw new InvalidOperationException("INVALID_OR_EXPIRED_RESET_TOKEN")
        };
        var controller = CreateController(fakeService);

        var result = await controller.ResetPassword(new ResetPasswordRequest("expired-token", "new-secret"), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
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
        public Func<LoginRequest, Task<LoginResponse>>? OnLoginAsync { get; set; }
        public Func<string?, Task<RefreshSessionResponse>>? OnRefreshAsync { get; set; }
        public Func<string?, Task>? OnLogoutAsync { get; set; }
        public Func<ForgotPasswordRequest, Task<ForgotPasswordResponse>>? OnForgotPasswordAsync { get; set; }
        public Func<ResetPasswordRequest, Task<ResetPasswordResponse>>? OnResetPasswordAsync { get; set; }

        public Task<StartRegistrationResponse> StartAsync(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<VerifyEmailRegistrationResponse> VerifyEmailAsync(Guid flowId, string otp, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CompleteProfileRegistrationResponse> CompleteProfileAsync(CompleteProfileRegistrationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MeResponse> MeAsync(string? accessToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RefreshSessionResponse> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default)
        {
            if (OnRefreshAsync is null)
            {
                throw new NotImplementedException();
            }

            return OnRefreshAsync(refreshToken);
        }
        public Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default)
        {
            if (OnForgotPasswordAsync is null)
            {
                throw new NotImplementedException();
            }

            return OnForgotPasswordAsync(request);
        }

        public Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default)
        {
            if (OnResetPasswordAsync is null)
            {
                throw new NotImplementedException();
            }

            return OnResetPasswordAsync(request);
        }
        public Task<AuthSecurityPolicyResponse> GetSecurityPolicyAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AuthSecurityPolicyResponse> UpdateSecurityPolicyAsync(UpdateAuthSecurityPolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GoogleRegisterStartResponse> StartGoogleRegistrationAsync(string state, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GoogleRegisterCallbackResponse> CompleteGoogleRegistrationAsync(string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        {
            if (OnLoginAsync is null)
            {
                throw new NotImplementedException();
            }

            return OnLoginAsync(request);
        }

        public Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
        {
            if (OnLogoutAsync is null)
            {
                throw new NotImplementedException();
            }

            return OnLogoutAsync(refreshToken);
        }
    }
}