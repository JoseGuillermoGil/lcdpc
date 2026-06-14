using System.Security.Cryptography;
using System.Text;
using LCDPC.Application.OAuth2;
using LCDPC.Domain.Entities.OAuth2;
using LCDPC.Infrastructure.Persistence;
using LCDPC.Infrastructure.Users.Auth;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.Infrastructure.OAuth2;

/// <summary>
/// Orchestrates the OAuth2 Authorization Code flow with PKCE.
/// Handles: authorize, code exchange, refresh token rotation, token theft detection, introspection, revocation.
/// </summary>
public sealed class OAuth2AuthorizationService : IOAuth2AuthorizationService
{
    private readonly AppDbContext _dbContext;
    private readonly IOAuth2ClientService _clientService;
    private readonly IOAuth2TokenService _tokenService;
    private readonly OAuth2Options _options;

    public OAuth2AuthorizationService(
        AppDbContext dbContext,
        IOAuth2ClientService clientService,
        IOAuth2TokenService tokenService,
        OAuth2Options options)
    {
        _dbContext = dbContext;
        _clientService = clientService;
        _tokenService = tokenService;
        _options = options;
    }

    public async Task<OAuth2AuthorizeResult> AuthorizeAsync(OAuth2AuthorizeRequest request, Guid? userId, CancellationToken ct = default)
    {
        // 1. Validate client exists
        var client = await _clientService.GetClientAsync(request.ClientId, ct);
        if (client is null)
        {
            return new OAuth2AuthorizeResult(
                Success: false,
                RedirectUrl: null,
                ErrorCode: "invalid_client",
                ErrorDescription: "The client_id is not registered.");
        }

        // 2. Validate redirect_uri matches
        if (!_clientService.ValidateRedirectUri(client, request.RedirectUri))
        {
            return new OAuth2AuthorizeResult(
                Success: false,
                RedirectUrl: null,
                ErrorCode: "invalid_redirect_uri",
                ErrorDescription: "The redirect_uri is not registered for this client.");
        }

        // 3. Validate response_type == "code"
        if (!string.Equals(request.ResponseType, "code", StringComparison.Ordinal))
        {
            return BuildErrorRedirect(request.RedirectUri, request.State,
                "unsupported_response_type",
                "Only authorization_code (code) response type is supported.");
        }

        // 4. Validate scopes
        if (!_clientService.ValidateScopes(client, request.Scope))
        {
            return BuildErrorRedirect(request.RedirectUri, request.State,
                "invalid_scope",
                "One or more requested scopes are not allowed for this client.");
        }

        // 5. PKCE validation: if client requires PKCE, code_challenge and method S256 are mandatory
        if (client.RequirePkce)
        {
            if (string.IsNullOrWhiteSpace(request.CodeChallenge))
            {
                return BuildErrorRedirect(request.RedirectUri, request.State,
                    "invalid_request",
                    "PKCE is required for this client. code_challenge is missing.");
            }

            if (!string.Equals(request.CodeChallengeMethod, "S256", StringComparison.Ordinal))
            {
                return BuildErrorRedirect(request.RedirectUri, request.State,
                    "invalid_request",
                    "Only S256 code_challenge_method is supported.");
            }
        }

        if (!userId.HasValue)
        {
            return BuildErrorRedirect(request.RedirectUri, request.State,
                "login_required",
                "User authentication is required.");
        }

        // 6. Generate authorization code (opaque, store hash)
        var codeBytes = RandomNumberGenerator.GetBytes(32);
        var rawCode = Convert.ToBase64String(codeBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var codeHash = TokenHashing.Hash(rawCode);

        // 7. Save to DB
        var nowUtc = DateTime.UtcNow;
        var authCode = new OAuth2AuthorizationCode
        {
            Code = codeHash,
            ClientId = request.ClientId,
            UserId = userId.Value,
            RedirectUri = request.RedirectUri,
            Scope = request.Scope ?? string.Empty,
            CodeChallenge = request.CodeChallenge ?? string.Empty,
            CodeChallengeMethod = request.CodeChallengeMethod ?? string.Empty,
            ExpiresAtUtc = nowUtc.AddMinutes(_options.AuthorizationCodeTtlMinutes),
            UsedAtUtc = null,
            CreatedAtUtc = nowUtc
        };

        _dbContext.OAuth2AuthorizationCodes.Add(authCode);
        await _dbContext.SaveChangesAsync(ct);

        // 8. Return redirect URL
        var redirectUrl = $"{request.RedirectUri}?code={rawCode}&state={request.State}";

        return new OAuth2AuthorizeResult(
            Success: true,
            RedirectUrl: redirectUrl,
            ErrorCode: null,
            ErrorDescription: null);
    }

    public async Task<OAuth2TokenResponse?> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, string clientId, CancellationToken ct = default)
    {
        // 1. Validate client exists
        var client = await _clientService.GetClientAsync(clientId, ct);
        if (client is null)
        {
            return null;
        }

        // 2. Search authorization code by hash
        var codeHash = TokenHashing.Hash(code);
        var authCode = await _dbContext.OAuth2AuthorizationCodes
            .FirstOrDefaultAsync(ac => ac.Code == codeHash, ct);

        if (authCode is null)
        {
            return null;
        }

        // 3. Validate code not expired, not used, redirect_uri matches, client matches
        var nowUtc = DateTime.UtcNow;
        if (authCode.UsedAtUtc.HasValue || authCode.ExpiresAtUtc <= nowUtc ||
            !string.Equals(authCode.ClientId, clientId, StringComparison.Ordinal) ||
            !string.Equals(authCode.RedirectUri, redirectUri, StringComparison.Ordinal))
        {
            return null;
        }

        // 4. Validate PKCE: SHA256(code_verifier) == code_challenge (base64url)
        if (client.RequirePkce || !string.IsNullOrEmpty(authCode.CodeChallenge))
        {
            if (string.IsNullOrWhiteSpace(codeVerifier))
            {
                return null;
            }

            var verifierHash = SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier));
            var computedChallenge = Convert.ToBase64String(verifierHash)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            if (!string.Equals(computedChallenge, authCode.CodeChallenge, StringComparison.Ordinal))
            {
                return null;
            }
        }

        // 5. Generate access token (JWT RS256)
        var accessToken = await _tokenService.GenerateAccessTokenAsync(
            authCode.UserId, clientId, authCode.Scope, ct);

        // 6. Generate refresh token (opaque, hash, new family_id)
        var (refreshToken, refreshHash) = await _tokenService.GenerateRefreshTokenAsync(ct);
        var familyId = Guid.NewGuid();

        // 7. Save refresh token
        var refreshTokenEntity = new OAuth2RefreshToken
        {
            TokenHash = refreshHash,
            ClientId = clientId,
            UserId = authCode.UserId,
            Scope = authCode.Scope,
            FamilyId = familyId,
            PreviousTokenHash = null, // First token in the family
            ExpiresAtUtc = nowUtc.AddDays(_options.RefreshTokenTtlDays),
            RevokedAtUtc = null,
            CreatedAtUtc = nowUtc
        };

        _dbContext.OAuth2RefreshTokens.Add(refreshTokenEntity);

        // 8. Mark authorization code as used
        authCode.UsedAtUtc = nowUtc;

        await _dbContext.SaveChangesAsync(ct);

        // 9. Return response
        return new OAuth2TokenResponse(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresIn: _options.AccessTokenTtlMinutes * 60,
            RefreshToken: refreshToken,
            Scope: authCode.Scope);
    }

    public async Task<OAuth2TokenResponse?> RefreshTokenAsync(string refreshToken, string clientId, CancellationToken ct = default)
    {
        // 1. Validate client exists
        var client = await _clientService.GetClientAsync(clientId, ct);
        if (client is null)
        {
            return null;
        }

        // 2. Search refresh token by hash
        var refreshHash = TokenHashing.Hash(refreshToken);
        var existingToken = await _dbContext.OAuth2RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == refreshHash, ct);

        if (existingToken is null)
        {
            return null;
        }

        var nowUtc = DateTime.UtcNow;

        // 3. Validate not expired, not revoked
        if (existingToken.ExpiresAtUtc <= nowUtc || existingToken.RevokedAtUtc.HasValue)
        {
            return null;
        }

        // 4. TOKEN THEFT DETECTION:
        // Check if this token has already been used (another token in the same family has this as previous_token_hash)
        var nextToken = await _dbContext.OAuth2RefreshTokens
            .FirstOrDefaultAsync(rt =>
                rt.FamilyId == existingToken.FamilyId &&
                rt.PreviousTokenHash == refreshHash &&
                rt.TokenHash != refreshHash,
                ct);

        if (nextToken is not null)
        {
            // TOKEN THEFT: This refresh token was already rotated.
            // Someone is trying to reuse a consumed token.
            // Revoke the entire family.
            await RevokeFamilyAsync(existingToken.FamilyId, ct);
            return null;
        }

        // 5. Generate new access token
        var accessToken = await _tokenService.GenerateAccessTokenAsync(
            existingToken.UserId, clientId, existingToken.Scope, ct);

        // 6. Generate new refresh token (same family_id, previous_token_hash = hash of current)
        var (newRefreshToken, newRefreshHash) = await _tokenService.GenerateRefreshTokenAsync(ct);

        var newRefreshTokenEntity = new OAuth2RefreshToken
        {
            TokenHash = newRefreshHash,
            ClientId = clientId,
            UserId = existingToken.UserId,
            Scope = existingToken.Scope,
            FamilyId = existingToken.FamilyId,
            PreviousTokenHash = refreshHash, // Chain to the current token
            ExpiresAtUtc = nowUtc.AddDays(_options.RefreshTokenTtlDays),
            RevokedAtUtc = null,
            CreatedAtUtc = nowUtc
        };

        _dbContext.OAuth2RefreshTokens.Add(newRefreshTokenEntity);

        // 7. Mark current refresh token as used (not revoked, but consumed)
        existingToken.RevokedAtUtc = nowUtc;

        await _dbContext.SaveChangesAsync(ct);

        // 8. Return new token pair
        return new OAuth2TokenResponse(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresIn: _options.AccessTokenTtlMinutes * 60,
            RefreshToken: newRefreshToken,
            Scope: existingToken.Scope);
    }

    public async Task<OAuth2IntrospectResponse> IntrospectTokenAsync(string token, CancellationToken ct = default)
    {
        // 1. Try as access token (JWT)
        var isValidJwt = await _tokenService.ValidateAccessTokenAsync(token, ct);
        if (isValidJwt)
        {
            // Parse JWT claims for introspection response
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            var sub = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            var clientId = jwt.Claims.FirstOrDefault(c => c.Type == "client_id")?.Value;
            var scope = jwt.Claims.FirstOrDefault(c => c.Type == "scope")?.Value;
            var expStr = jwt.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
            var iatStr = jwt.Claims.FirstOrDefault(c => c.Type == "iat")?.Value;
            var username = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

            long? exp = long.TryParse(expStr, out var expVal) ? expVal : null;
            long? iat = long.TryParse(iatStr, out var iatVal) ? iatVal : null;

            return new OAuth2IntrospectResponse(
                Active: true,
                ClientId: clientId,
                Username: username,
                Scope: scope,
                Exp: exp,
                Iat: iat,
                Sub: sub);
        }

        // 2. Try as refresh token (DB lookup by hash)
        var hash = TokenHashing.Hash(token);
        var refreshToken = await _dbContext.OAuth2RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);

        if (refreshToken is not null)
        {
            var nowUtc = DateTime.UtcNow;
            var isActive = refreshToken.ExpiresAtUtc > nowUtc && !refreshToken.RevokedAtUtc.HasValue;

            return new OAuth2IntrospectResponse(
                Active: isActive,
                ClientId: refreshToken.ClientId,
                Username: null, // Refresh tokens don't carry username directly
                Scope: refreshToken.Scope,
                Exp: new DateTimeOffset(refreshToken.ExpiresAtUtc).ToUnixTimeSeconds(),
                Iat: new DateTimeOffset(refreshToken.CreatedAtUtc).ToUnixTimeSeconds(),
                Sub: refreshToken.UserId.ToString());
        }

        // 3. Unknown token
        return new OAuth2IntrospectResponse(
            Active: false,
            ClientId: null,
            Username: null,
            Scope: null,
            Exp: null,
            Iat: null,
            Sub: null);
    }

    public async Task<OAuth2RevokeResponse> RevokeTokenAsync(string token, CancellationToken ct = default)
    {
        var hash = TokenHashing.Hash(token);
        var refreshToken = await _dbContext.OAuth2RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);

        if (refreshToken is not null)
        {
            await RevokeFamilyAsync(refreshToken.FamilyId, ct);
            return new OAuth2RevokeResponse(Success: true);
        }

        // Token not found - RFC 7009 says return success anyway (prevent probing)
        return new OAuth2RevokeResponse(Success: true);
    }

    private async Task RevokeFamilyAsync(Guid familyId, CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        await _dbContext.OAuth2RefreshTokens
            .Where(rt => rt.FamilyId == familyId && !rt.RevokedAtUtc.HasValue)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(rt => rt.RevokedAtUtc, nowUtc),
                ct);
    }

    private static OAuth2AuthorizeResult BuildErrorRedirect(string redirectUri, string? state, string errorCode, string errorDescription)
    {
        var separator = redirectUri.Contains('?') ? '&' : '?';
        var url = $"{redirectUri}{separator}error={Uri.EscapeDataString(errorCode)}&error_description={Uri.EscapeDataString(errorDescription)}";
        if (!string.IsNullOrEmpty(state))
        {
            url += $"&state={Uri.EscapeDataString(state)}";
        }

        return new OAuth2AuthorizeResult(
            Success: false,
            RedirectUrl: url,
            ErrorCode: errorCode,
            ErrorDescription: errorDescription);
    }
}
