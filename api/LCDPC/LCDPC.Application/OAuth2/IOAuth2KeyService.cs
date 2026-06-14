using System.Security.Cryptography;

namespace LCDPC.Application.OAuth2;

public interface IOAuth2KeyService
{
    /// <summary>
    /// Returns the RSA private key for JWT token signing.
    /// </summary>
    RSA GetSigningKey();

    /// <summary>
    /// Returns the public key material in JWKS format for the discovery endpoint.
    /// </summary>
    OAuth2JwksResponse GetJwks();
}

public record OAuth2JwksResponse(OAuth2JwkKey[] Keys);

public record OAuth2JwkKey(string Kty, string Kid, string Alg, string Use, string N, string E);
