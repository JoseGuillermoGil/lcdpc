using System.Security.Cryptography;
using System.Text;
using LCDPC.Application.OAuth2;
using Microsoft.Extensions.Logging;

namespace LCDPC.Infrastructure.OAuth2;

/// <summary>
/// Generates and caches an RSA key pair for JWT RS256 signing.
/// Exports the public key in JWKS format for the discovery endpoint.
/// </summary>
public sealed class RsaKeyService : IOAuth2KeyService, IDisposable
{
    private readonly RSA _rsa;
    private readonly OAuth2JwksResponse _jwks;

    public RsaKeyService(OAuth2Options options, ILogger<RsaKeyService> logger)
    {
        _rsa = LoadOrCreateRsaKey(options.RsaKeyPath, logger);
        var parameters = _rsa.ExportParameters(false);
        var kid = GenerateKeyId(parameters.Modulus!);

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

    private static RSA LoadOrCreateRsaKey(string? rsaKeyPath, ILogger<RsaKeyService> logger)
    {
        if (!string.IsNullOrWhiteSpace(rsaKeyPath) && File.Exists(rsaKeyPath))
        {
            try
            {
                var pem = File.ReadAllText(rsaKeyPath);
                var rsa = RSA.Create();
                rsa.ImportFromPem(pem);
                logger.LogInformation("Loaded OAuth2 RSA key from path: {RsaKeyPath}", rsaKeyPath);
                return rsa;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load OAuth2 RSA key from path: {RsaKeyPath}. Falling back to generated ephemeral key.", rsaKeyPath);
            }
        }

        logger.LogInformation("Using generated OAuth2 RSA key (ephemeral) because no valid configured key file was found.");
        return RSA.Create(4096);
    }

    private static string GenerateKeyId(byte[] modulus)
    {
        var hash = SHA256.HashData(modulus);
        var suffix = ToBase64Url(hash)[..16];
        return $"lcdpc-{suffix}";
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
