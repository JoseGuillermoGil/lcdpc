namespace LCDPC.Application.OAuth2;

public interface IOAuth2AuthorizationService
{
    /// <summary>
    /// Handles the OAuth2 Authorization Code flow (authorize endpoint).
    /// Validates client, redirect_uri, response_type, scopes, and PKCE requirements.
    /// Generates an opaque authorization code and returns the redirect URL.
    /// </summary>
    Task<OAuth2AuthorizeResult> AuthorizeAsync(OAuth2AuthorizeRequest request, Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// Exchanges an authorization code for an access token + refresh token pair.
    /// Validates the code, PKCE code_verifier, and redirect_uri.
    /// Implements refresh token rotation.
    /// </summary>
    Task<OAuth2TokenResponse?> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, string clientId, CancellationToken ct = default);

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// Implements refresh token rotation with token theft detection.
    /// If a consumed refresh token is reused, revokes the entire token family.
    /// </summary>
    Task<OAuth2TokenResponse?> RefreshTokenAsync(string refreshToken, string clientId, CancellationToken ct = default);

    /// <summary>
    /// RFC 7662 Token Introspection.
    /// Checks if the token is an active access token (JWT) or refresh token (DB lookup).
    /// </summary>
    Task<OAuth2IntrospectResponse> IntrospectTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// RFC 7009 Token Revocation.
    /// Revokes a refresh token and its entire family.
    /// </summary>
    Task<OAuth2RevokeResponse> RevokeTokenAsync(string token, CancellationToken ct = default);
}
