using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LCDPC.Application.OAuth2;
using LCDPC.Infrastructure.Persistence;
using LCDPC.Infrastructure.Users.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LCDPC.Infrastructure.OAuth2;

/// <summary>
/// Generates and validates OAuth2 tokens: JWT access tokens (RS256) and opaque refresh tokens.
/// </summary>
public sealed class OAuth2TokenService : IOAuth2TokenService
{
    private readonly AppDbContext _dbContext;
    private readonly IOAuth2KeyService _keyService;
    private readonly OAuth2Options _options;

    public OAuth2TokenService(
        AppDbContext dbContext,
        IOAuth2KeyService keyService,
        OAuth2Options options)
    {
        _dbContext = dbContext;
        _keyService = keyService;
        _options = options;
    }

    public async Task<string> GenerateAccessTokenAsync(Guid userId, string clientId, string scope, CancellationToken ct = default)
    {
        var rsa = _keyService.GetSigningKey();
        var jwks = _keyService.GetJwks();
        var keyId = jwks.Keys.Length > 0 ? jwks.Keys[0].Kid : null;

        var signingCredentials = new SigningCredentials(
            new RsaSecurityKey(rsa),
            SecurityAlgorithms.RsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };

        if (!string.IsNullOrEmpty(keyId))
        {
            signingCredentials.Key.KeyId = keyId;
        }

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenTtlMinutes);

        // Resolve user email and roles
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.RoleAssignments)
            .ThenInclude(ra => ra.Role)
            .ThenInclude(r => r.ResourcePermissions)
            .ThenInclude(rrp => rrp.Resource)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        var email = user?.Email ?? userId.ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Iss, _options.Issuer),
            new(JwtRegisteredClaimNames.Aud, _options.Audience),
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("client_id", clientId),
            new("scope", scope),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Exp, new DateTimeOffset(expires).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, email)
        };

        // Add roles
        if (user?.RoleAssignments is not null)
        {
            foreach (var assignment in user.RoleAssignments.Where(a => a.Active))
            {
                claims.Add(new Claim("roles", assignment.Role.Code));

                // Add permissions as nested claims
                foreach (var perm in assignment.Role.ResourcePermissions)
                {
                    var permKey = $"permissions:{perm.Resource.Code}";
                    claims.Add(new Claim(permKey, SerializePermissions(perm)));
                }
            }
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = signingCredentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    public Task<(string token, string hash)> GenerateRefreshTokenAsync(CancellationToken ct = default)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var hash = TokenHashing.Hash(token);
        return Task.FromResult((token, hash));
    }

    public Task<bool> ValidateAccessTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var rsa = _keyService.GetSigningKey();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, validationParameters, out _);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static string SerializePermissions(LCDPC.Domain.Entities.Users.RoleResourcePermission perm)
    {
        var parts = new List<string>(5);
        if (perm.CanView) parts.Add("view");
        if (perm.CanWrite) parts.Add("write");
        if (perm.CanUpdate) parts.Add("update");
        if (perm.CanDelete) parts.Add("delete");
        if (perm.CanAll) parts.Add("all");
        return string.Join(",", parts);
    }
}
