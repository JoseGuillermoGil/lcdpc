using LCDPC.Application.OAuth2;
using LCDPC.Infrastructure.OAuth2;
using Microsoft.AspNetCore.Mvc;

namespace LCDPC.API.Controllers;

/// <summary>
/// OAuth 2.0 / OpenID Connect discovery endpoints (RFC 8414).
/// Clients use these endpoints to auto-discover the authorization server's configuration.
/// These endpoints are unauthenticated by design.
/// </summary>
[ApiController]
[Route(".well-known")]
public class DiscoveryController(IOAuth2KeyService keyService, OAuth2Options options) : ControllerBase
{
    // ──────────────────────────────────────────────
    // GET /.well-known/openid-configuration — OIDC Discovery (RFC 8414)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Returns the OpenID Connect discovery document for this authorization server.
    /// </summary>
    [HttpGet("openid-configuration")]
    [ProducesResponseType(typeof(OpenIdConfigurationResponse), StatusCodes.Status200OK)]
    [Produces("application/json")]
    public IActionResult GetOpenIdConfiguration()
    {
        var issuer = options.Issuer;

        var response = new OpenIdConfigurationResponse
        {
            Issuer = issuer,
            AuthorizationEndpoint = $"{issuer}/oauth2/authorize",
            TokenEndpoint = $"{issuer}/oauth2/token",
            IntrospectionEndpoint = $"{issuer}/oauth2/introspect",
            RevocationEndpoint = $"{issuer}/oauth2/revoke",
            JwksUri = $"{issuer}/.well-known/jwks.json",
            ScopesSupported = GetScopesSupported(),
            ResponseTypesSupported = new[] { "code" },
            GrantTypesSupported = new[] { "authorization_code", "refresh_token" },
            CodeChallengeMethodsSupported = new[] { "S256" },
            TokenEndpointAuthMethodsSupported = new[] { "none" },
            SubjectTypesSupported = new[] { "public" },
            IdTokenSigningAlgValuesSupported = new[] { "RS256" }
        };

        return Ok(response);
    }

    // ──────────────────────────────────────────────
    // GET /.well-known/jwks.json — JWKS Endpoint (RFC 7517)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Returns the JSON Web Key Set used to validate RS256 access tokens.
    /// </summary>
    [HttpGet("jwks.json")]
    [ProducesResponseType(typeof(OAuth2JwksResponse), StatusCodes.Status200OK)]
    [Produces("application/json")]
    public IActionResult GetJwks()
    {
        var jwks = keyService.GetJwks();
        return Ok(jwks);
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────

    private string[] GetScopesSupported()
    {
        // Collect all allowed scopes from registered clients and merge them
        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add default scopes
        scopes.Add("openid");
        scopes.Add("email");
        scopes.Add("profile");

        // Add client-specific scopes
        foreach (var client in options.Clients)
        {
            if (!string.IsNullOrWhiteSpace(client.AllowedScopes))
            {
                foreach (var scope in client.AllowedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    scopes.Add(scope);
                }
            }
        }

        return scopes.OrderBy(s => s).ToArray();
    }
}

/// <summary>
/// OpenID Connect Discovery response per RFC 8414.
/// </summary>
public sealed record OpenIdConfigurationResponse
{
    public string Issuer { get; init; } = string.Empty;
    public string AuthorizationEndpoint { get; init; } = string.Empty;
    public string TokenEndpoint { get; init; } = string.Empty;
    public string IntrospectionEndpoint { get; init; } = string.Empty;
    public string RevocationEndpoint { get; init; } = string.Empty;
    public string JwksUri { get; init; } = string.Empty;
    public string[] ScopesSupported { get; init; } = [];
    public string[] ResponseTypesSupported { get; init; } = [];
    public string[] GrantTypesSupported { get; init; } = [];
    public string[] CodeChallengeMethodsSupported { get; init; } = [];
    public string[] TokenEndpointAuthMethodsSupported { get; init; } = [];
    public string[] SubjectTypesSupported { get; init; } = [];
    public string[] IdTokenSigningAlgValuesSupported { get; init; } = [];
}
