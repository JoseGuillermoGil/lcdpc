using System.Security.Cryptography;
using System.Text;
using LCDPC.Application.OAuth2;

namespace LCDPC.Infrastructure.OAuth2;

/// <summary>
/// Generates and caches an RSA key pair for JWT RS256 signing.
/// Exports the public key in JWKS format for the discovery endpoint.
/// </summary>
public sealed class RsaKeyService : IOAuth2KeyService, IDisposable
{
    private readonly RSA _rsa;
    private readonly OAuth2JwksResponse _jwks;

    public RsaKeyService(string? keyId = null)
    {
        _rsa = RSA.Create(4096);
        var kid = keyId ?? GenerateKeyId();
        var parameters = _rsa.ExportParameters(false);

        _jwks = new OAuth2JwksResponse(
        [
            new OAuth2JwkKey(
                Kty: "RSA",
                Kid: kid,
                Alg: "RS256",
                Use: "sig",
                N: ToBase64Url(parameters.Modulus!),
                E: ToBase64Url(parameters.Exponent!)
            )
        ]);
    }

    public RSA GetSigningKey() => _rsa;

    public OAuth2JwksResponse GetJwks() => _jwks;

    private static string GenerateKeyId()
    {
        var now = DateTime.UtcNow;
        return $"lcdpc-{now:yyyy}-{now:MM}";
    }

    private static string ToBase64Url(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public void Dispose() => _rsa.Dispose();
}
