using System.Security.Cryptography;
using LCDPC.Infrastructure.OAuth2;
using Microsoft.Extensions.Logging.Abstractions;

namespace LCDPC.Architecture.Tests;

public class RsaKeyServiceTests
{
    [Fact]
    public void Constructor_WithMissingPath_GeneratesEphemeralKeyAndJwks()
    {
        var options = new OAuth2Options
        {
            RsaKeyPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid()}.pem")
        };

        using var service = new RsaKeyService(options, NullLogger<RsaKeyService>.Instance);

        var key = service.GetSigningKey();
        var jwks = service.GetJwks();

        Assert.NotNull(key);
        Assert.Single(jwks.Keys);
        Assert.Equal("RSA", jwks.Keys[0].Kty);
        Assert.Equal("RS256", jwks.Keys[0].Alg);
    }

    [Fact]
    public void Constructor_WithPemPath_LoadsConfiguredPrivateKey()
    {
        using var expected = RSA.Create(2048);
        var pem = expected.ExportPkcs8PrivateKeyPem();

        var tempPath = Path.Combine(Path.GetTempPath(), $"oauth2-rsa-{Guid.NewGuid()}.pem");
        File.WriteAllText(tempPath, pem);

        try
        {
            var options = new OAuth2Options { RsaKeyPath = tempPath };
            using var service = new RsaKeyService(options, NullLogger<RsaKeyService>.Instance);

            var expectedModulus = expected.ExportParameters(false).Modulus;
            var actualModulus = service.GetSigningKey().ExportParameters(false).Modulus;

            Assert.Equal(expectedModulus, actualModulus);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
