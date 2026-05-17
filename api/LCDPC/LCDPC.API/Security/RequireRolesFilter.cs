using LCDPC.Application.Users.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LCDPC.API.Security;

public sealed class RequireRolesFilter(IRegistrationFlowService registrationFlowService, string[] roles) : IAsyncActionFilter
{
    private const string AccessTokenCookieName = "lcdpc_at";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var accessToken = context.HttpContext.Request.Cookies[AccessTokenCookieName];
        var me = await registrationFlowService.MeAsync(accessToken, context.HttpContext.RequestAborted);

        if (!me.Authenticated)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                code = "UNAUTHENTICATED",
                message = "authentication required"
            });
            return;
        }

        if (roles.Length == 0)
        {
            await next();
            return;
        }

        var hasAnyRequiredRole = me.UserSummary?.Roles.Any(userRole =>
            roles.Any(requiredRole => string.Equals(userRole, requiredRole, StringComparison.OrdinalIgnoreCase))) == true;

        if (!hasAnyRequiredRole)
        {
            context.Result = new ObjectResult(new
            {
                code = "FORBIDDEN",
                message = "required role not granted"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}
