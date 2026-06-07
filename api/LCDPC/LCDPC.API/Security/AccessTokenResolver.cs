namespace LCDPC.API.Security;

public static class AccessTokenResolver
{
    private const string AccessTokenCookieName = "lcdpc_at";

    public static string? Resolve(HttpRequest request)
    {
        var authorizationHeader = request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authorizationHeader)
            && authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var bearerToken = authorizationHeader["Bearer ".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                return bearerToken;
            }
        }

        return request.Cookies[AccessTokenCookieName];
    }
}