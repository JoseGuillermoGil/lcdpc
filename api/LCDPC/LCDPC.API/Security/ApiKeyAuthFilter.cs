using LCDPC.Infrastructure.Persistence;
using LCDPC.Infrastructure.Users.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.API.Security;

public sealed class ApiKeyAuthFilter(AppDbContext dbContext) : IAsyncActionFilter
{
    private const string ApiKeyHeaderName = "X-API-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var apiKey = context.HttpContext.Request.Headers[ApiKeyHeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            context.Result = new ObjectResult(new
            {
                code = "API_KEY_MISSING",
                message = "X-API-Key header is required"
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        var tokenHash = TokenHashing.Hash(apiKey);

        var apiToken = await dbContext.ApiTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Activo, context.HttpContext.RequestAborted);

        if (apiToken is null)
        {
            context.Result = new ObjectResult(new
            {
                code = "API_KEY_INVALID",
                message = "invalid or inactive API key"
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        await next();
    }
}
