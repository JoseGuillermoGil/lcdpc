using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using LCDPC.Application.OAuth2;
using LCDPC.Infrastructure.OAuth2;
using LCDPC.Infrastructure.Persistence;
using LCDPC.Infrastructure.Users.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace LCDPC.API.Security;

/// <summary>
/// JWT Bearer events for OAuth 2.0 token validation using RSA public key from JWKS.
/// Validates RS256-signed access tokens and checks session validity in the database.
/// </summary>
public sealed class OAuth2JwtBearerEvents : JwtBearerEvents
{
    private readonly ILogger<OAuth2JwtBearerEvents> _logger;

    public OAuth2JwtBearerEvents(ILogger<OAuth2JwtBearerEvents> logger)
    {
        _logger = logger;
    }

    public override Task MessageReceived(MessageReceivedContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Token))
        {
            context.Token = AccessTokenResolver.Resolve(context.HttpContext.Request);
        }

        return Task.CompletedTask;
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var accessToken = AccessTokenResolver.Resolve(context.HttpContext.Request);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            context.Fail("MISSING_ACCESS_TOKEN");
            return;
        }

        var serviceProvider = context.HttpContext.RequestServices;

        // Validate token against the OAuth2 token service (RSA/JWKS validation)
        var tokenService = serviceProvider.GetService<IOAuth2TokenService>();
        if (tokenService is not null)
        {
            var isValid = await tokenService.ValidateAccessTokenAsync(accessToken, context.HttpContext.RequestAborted);
            if (!isValid)
            {
                _logger.LogWarning("OAuth2 access token failed RSA/JWKS validation");
                context.Fail("INVALID_OAUTH2_TOKEN");
                return;
            }
        }

        // For backward compatibility: also validate against the legacy session store
        // OAuth2 tokens won't have a matching session, so this is a soft check
        var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
        var tokenHash = TokenHashing.Hash(accessToken);
        var nowUtc = DateTime.UtcNow;

        var session = await dbContext.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.AccessTokenHash == tokenHash
                && x.RevokedAtUtc == null
                && x.AccessTokenExpiresAtUtc > nowUtc,
            context.HttpContext.RequestAborted);

        // If no legacy session found, check if this is a valid OAuth2 token
        // by verifying the user exists and is active
        if (session is null)
        {
            var subject = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (Guid.TryParse(subject, out var userId))
            {
                var userStatus = await dbContext.Users
                    .AsNoTracking()
                    .Where(x => x.Id == userId)
                    .Select(x => x.Status)
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                if (userStatus == 0) // UserStatus default/unknown
                {
                    context.Fail("USER_NOT_FOUND");
                    return;
                }

                if (userStatus is LCDPC.Domain.Entities.Users.UserStatus.Suspended
                    or LCDPC.Domain.Entities.Users.UserStatus.Deactivated)
                {
                    context.Fail("USER_NOT_ALLOWED");
                    return;
                }

                // Valid OAuth2 token with active user — mark as OAuth2 session
                context.HttpContext.Items["IsOAuth2Token"] = true;
                context.HttpContext.Items["OAuth2UserId"] = userId;
                return;
            }

            context.Fail("INVALID_SESSION");
            return;
        }

        // Legacy session validation
        var sessionSubject = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                             ?? context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sessionSubject, out var sessionUserId) || sessionUserId != session.UserId)
        {
            context.Fail("TOKEN_SUBJECT_MISMATCH");
            return;
        }

        var sessionClaim = context.Principal?.FindFirstValue("sid");
        if (!Guid.TryParse(sessionClaim, out var tokenSessionId) || tokenSessionId != session.Id)
        {
            context.Fail("TOKEN_SESSION_MISMATCH");
            return;
        }

        var userStatusForSession = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == session.UserId)
            .Select(x => x.Status)
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (userStatusForSession is LCDPC.Domain.Entities.Users.UserStatus.Suspended
            or LCDPC.Domain.Entities.Users.UserStatus.Deactivated)
        {
            context.Fail("USER_NOT_ALLOWED");
        }
    }

    public override Task AuthenticationFailed(AuthenticationFailedContext context)
    {
        _logger.LogError(context.Exception, "OAuth2 JWT authentication failed: {FailureReason}",
            context.Exception.Message);

        return Task.CompletedTask;
    }
}
