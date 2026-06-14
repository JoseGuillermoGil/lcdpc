using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using LCDPC.Application.Users.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LCDPC.API.Security;

/// <summary>
/// Action filter that validates user roles.
/// Supports both OAuth2 JWT tokens (with embedded role claims) and legacy cookie-based sessions.
/// For OAuth2 tokens: extracts roles directly from JWT claims.
/// For legacy tokens: falls back to IRegistrationFlowService.MeAsync.
/// </summary>
public sealed class RequireRolesFilter(IRegistrationFlowService registrationFlowService, string[] roles) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Try OAuth2 JWT claim-based authorization first (fast path)
        var jwtRoles = ExtractRolesFromJwt(context.HttpContext.User);
        var isOAuth2Token = context.HttpContext.Items["IsOAuth2Token"] as bool? == true;

        if (isOAuth2Token && jwtRoles is not null)
        {
            if (roles.Length == 0)
            {
                await next();
                return;
            }

            var hasAnyRequiredRole = jwtRoles.Any(userRole =>
                roles.Any(requiredRole => string.Equals(userRole, requiredRole, StringComparison.OrdinalIgnoreCase)));

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
            return;
        }

        // Fall back to legacy session-based authorization
        var accessToken = AccessTokenResolver.Resolve(context.HttpContext.Request);
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

        var hasAnyRequiredRoleLegacy = me.UserSummary?.Roles.Any(userRole =>
            roles.Any(requiredRole => string.Equals(userRole, requiredRole, StringComparison.OrdinalIgnoreCase))) == true;

        if (!hasAnyRequiredRoleLegacy)
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

    /// <summary>
    /// Extracts roles from JWT claims (OAuth2 token).
    /// Checks both "roles" (array) and "role" (single) claim types.
    /// Returns null if no role claims are found.
    /// </summary>
    private static IReadOnlyList<string>? ExtractRolesFromJwt(ClaimsPrincipal principal)
    {
        var roles = new List<string>();

        // Check for "roles" claim (our OAuth2 token format uses multiple "roles" claims)
        var roleClaims = principal.FindAll("roles");
        foreach (var claim in roleClaims)
        {
            if (!string.IsNullOrWhiteSpace(claim.Value))
            {
                roles.Add(claim.Value);
            }
        }

        // Also check for "role" claim (alternative format)
        var roleClaim = principal.FindFirst("role");
        if (roleClaim is not null && !string.IsNullOrWhiteSpace(roleClaim.Value))
        {
            roles.Add(roleClaim.Value);
        }

        // Check standard claim types
        if (roles.Count == 0)
        {
            var standardRoles = principal.FindAll(ClaimTypes.Role);
            foreach (var claim in standardRoles)
            {
                if (!string.IsNullOrWhiteSpace(claim.Value))
                {
                    roles.Add(claim.Value);
                }
            }
        }

        return roles.Count > 0 ? roles : null;
    }
}
