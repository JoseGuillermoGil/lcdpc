namespace LCDPC.Application.OAuth2;

public interface IOAuth2TokenService
{
    /// <summary>
    /// Generates a signed JWT access token (RS256) for the given user.
    /// Includes claims: iss, aud, sub, client_id, scope, iat, exp, jti, roles, permissions.
    /// </summary>
    Task<string> GenerateAccessTokenAsync(Guid userId, string clientId, string scope, CancellationToken ct = default);

    /// <summary>
    /// Generates a cryptographically random opaque refresh token and its SHA256 hash.
    /// Returns (token, hash).
    /// </summary>
    Task<(string token, string hash)> GenerateRefreshTokenAsync(CancellationToken ct = default);

    /// <summary>
    /// Validates a JWT access token against the JWKS public key.
    /// Returns true if the token is valid and not expired.
    /// </summary>
    Task<bool> ValidateAccessTokenAsync(string token, CancellationToken ct = default);
}
