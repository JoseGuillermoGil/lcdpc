using LCDPC.API.Security;
using LCDPC.Application.Users.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace LCDPC.Architecture.Tests;

public class RequireRolesFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_ReturnsUnauthorized_WhenUserIsNotAuthenticated()
    {
        var service = new FakeRegistrationFlowService(new MeResponse(false, null, Array.Empty<ResourcePermissionResponse>(), null));
        var filter = new RequireRolesFilter(service, ["admin_global"]);
        var context = BuildContext();
        var nextWasCalled = false;

        await filter.OnActionExecutionAsync(context, BuildNextDelegate(context, () => nextWasCalled = true));

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        Assert.False(nextWasCalled);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ReturnsForbidden_WhenUserHasNoRequiredRole()
    {
        var meResponse = new MeResponse(
            true,
            BuildUserSummary(["cliente"]),
            Array.Empty<ResourcePermissionResponse>(),
            1200);
        var service = new FakeRegistrationFlowService(meResponse);
        var filter = new RequireRolesFilter(service, ["admin_global"]);
        var context = BuildContext();
        var nextWasCalled = false;

        await filter.OnActionExecutionAsync(context, BuildNextDelegate(context, () => nextWasCalled = true));

        var forbidden = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.False(nextWasCalled);
    }

    [Fact]
    public async Task OnActionExecutionAsync_AllowsExecution_WhenUserHasRequiredRole()
    {
        var meResponse = new MeResponse(
            true,
            BuildUserSummary(["Admin_Global"]),
            Array.Empty<ResourcePermissionResponse>(),
            1200);
        var service = new FakeRegistrationFlowService(meResponse);
        var filter = new RequireRolesFilter(service, ["admin_global"]);
        var context = BuildContext();
        var nextWasCalled = false;

        await filter.OnActionExecutionAsync(context, BuildNextDelegate(context, () => nextWasCalled = true));

        Assert.Null(context.Result);
        Assert.True(nextWasCalled);
    }

    private static ActionExecutingContext BuildContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer test-token";

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());
    }

    private static ActionExecutionDelegate BuildNextDelegate(ActionContext actionContext, Action onCalled)
    {
        return () =>
        {
            onCalled();
            var executedContext = new ActionExecutedContext(
                actionContext,
                new List<IFilterMetadata>(),
                controller: new object());
            return Task.FromResult(executedContext);
        };
    }

    private static UserSummaryResponse BuildUserSummary(IReadOnlyList<string> roles)
    {
        return new UserSummaryResponse(
            Guid.NewGuid(),
            "tester@example.com",
            "Test User",
            "activo",
            "administrador",
            "active",
            DateTime.UtcNow,
            roles);
    }

    private sealed class FakeRegistrationFlowService(MeResponse meResponse) : IRegistrationFlowService
    {
        public Task<StartRegistrationResponse> StartAsync(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<VerifyEmailRegistrationResponse> VerifyEmailAsync(Guid flowId, string otp, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CompleteProfileRegistrationResponse> CompleteProfileAsync(CompleteProfileRegistrationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MeResponse> MeAsync(string? accessToken, CancellationToken cancellationToken = default) => Task.FromResult(meResponse);
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