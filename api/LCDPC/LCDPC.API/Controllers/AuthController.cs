using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LCDPC.Application.OAuth2;
using LCDPC.Application.Users.Auth;
using LCDPC.API.Security;
using LCDPC.Domain.Entities.OAuth2;
using LCDPC.Domain.Entities.Users;
using LCDPC.Infrastructure.OAuth2;
using LCDPC.Infrastructure.Persistence;
using LCDPC.Infrastructure.Users.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.API.Controllers;

/// <summary>
/// Legacy authentication endpoints — maintained as a compatibility layer during OAuth 2.0 migration.
/// All endpoints return deprecation headers pointing clients toward the new /oauth2/* endpoints.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    IRegistrationFlowService registrationFlowService,
    IOAuth2AuthorizationService oauth2AuthorizationService,
    IOAuth2TokenService oauth2TokenService,
    IOAuth2ClientService oauth2ClientService,
    AppDbContext dbContext,
    OAuth2Options oauth2Options) : ControllerBase
{
    private const string AccessTokenCookieName = "lcdpc_at";
    private const string RefreshTokenCookieName = "lcdpc_rt";
    private const string GoogleStateCookieName = "lcdpc_google_state";
    private const string LegacyClientId = "lcdpc-web";
    private const string LegacyScope = "openid email profile";
    private int RefreshTokenTtlDays => oauth2Options.RefreshTokenTtlDays;

    // ──────────────────────────────────────────────
    // Deprecation helper
    // ──────────────────────────────────────────────

    /// <summary>
    /// Adds standard deprecation headers signaling the migration to OAuth 2.0.
    /// Sunset date: 3 months from now (clients should migrate before this date).
    /// </summary>
    private void AddDeprecationHeaders()
    {
        Response.Headers["Deprecation"] = "true";
        // Sunset: 2026-09-13 (3 months from spec date 2026-06-13)
        Response.Headers["Sunset"] = "Sat, 13 Sep 2026 00:00:00 GMT";
        Response.Headers["Link"] = "</oauth2/authorize>; rel=\"successor-version\"";
    }

    // ──────────────────────────────────────────────
    // Registration endpoints (UNCHANGED — pre-auth, no OAuth 2.0 mapping needed)
    // ──────────────────────────────────────────────

    [HttpPost("register/start")]
    [ProducesResponseType(typeof(StartRegistrationResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartRegistration([FromBody] StartRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                code = "EMAIL_REQUIRED",
                message = "email is required"
            });
        }

        if (!IsValidEmail(request.Email))
        {
            return BadRequest(new
            {
                code = "EMAIL_INVALID",
                message = "email format is invalid"
            });
        }

        try
        {
            var response = await registrationFlowService.StartAsync(request.Email, cancellationToken);
            return Accepted(response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "EMAIL_ALREADY_REGISTERED")
        {
            return Conflict(new
            {
                code = "EMAIL_ALREADY_REGISTERED",
                message = "email already exists"
            });
        }
        catch (InvalidOperationException ex) when (ex.Message == "OTP_COOLDOWN_ACTIVE")
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                code = "OTP_COOLDOWN_ACTIVE",
                message = "otp cooldown is active"
            });
        }
    }

    [HttpPost("register/verify-email")]
    [ProducesResponseType(typeof(VerifyEmailRegistrationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRegistrationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await registrationFlowService.VerifyEmailAsync(request.FlowId, request.Otp, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "FLOW_NOT_FOUND")
        {
            return NotFound(new { code = "FLOW_NOT_FOUND", message = "registration flow not found" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "FLOW_INVALID_STATUS")
        {
            return Conflict(new { code = "FLOW_INVALID_STATUS", message = "registration flow is not in a verifiable state" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "OTP_EXPIRED")
        {
            return StatusCode(StatusCodes.Status410Gone, new { code = "OTP_EXPIRED", message = "otp expired" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "OTP_ATTEMPTS_EXCEEDED")
        {
            return Conflict(new { code = "OTP_ATTEMPTS_EXCEEDED", message = "otp attempts exceeded" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "OTP_INVALID")
        {
            return BadRequest(new { code = "OTP_INVALID", message = "otp is invalid" });
        }
    }

    [HttpPost("register/profile")]
    [ProducesResponseType(typeof(CompleteProfileRegistrationResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CompleteProfile([FromBody] CompleteProfileRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { code = "PASSWORD_REQUIRED", message = "password is required" });
        }

        try
        {
            var response = await registrationFlowService.CompleteProfileAsync(request, cancellationToken);
            return Created(string.Empty, response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "FLOW_NOT_FOUND")
        {
            return NotFound(new { code = "FLOW_NOT_FOUND", message = "registration flow not found" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "FLOW_INVALID_STATUS")
        {
            return Conflict(new { code = "FLOW_INVALID_STATUS", message = "registration flow is not in a profile state" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "EMAIL_ALREADY_REGISTERED")
        {
            return Conflict(new { code = "EMAIL_ALREADY_REGISTERED", message = "email already exists" });
        }
    }

    // ──────────────────────────────────────────────
    // POST /api/v1/auth/login — OAuth 2.0 adapter
    // Authenticates user, generates OAuth2 tokens internally, returns legacy format.
    // ──────────────────────────────────────────────

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        AddDeprecationHeaders();

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { code = "INVALID_REQUEST", message = "email and password are required" });
        }

        if (!IsValidEmail(request.Email))
        {
            return BadRequest(new { code = "EMAIL_INVALID", message = "email format is invalid" });
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .Include(u => u.Profile)
            .Include(u => u.RoleAssignments)
                .ThenInclude(ra => ra.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return Unauthorized(new { code = "INVALID_CREDENTIALS", message = "invalid email or password" });
        }

        if (user.Status is UserStatus.Suspended or UserStatus.Deactivated)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "USER_NOT_ALLOWED", message = "user is not allowed to login" });
        }

        if (!string.Equals(user.OnboardingStatus, "active", StringComparison.OrdinalIgnoreCase) || user.EmailVerifiedAtUtc is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "ONBOARDING_INCOMPLETE", message = "onboarding is not complete" });
        }

        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { code = "INVALID_CREDENTIALS", message = "invalid email or password" });
        }

        // Validate the OAuth2 client exists (ensures the internal token generation target is valid)
        var client = await oauth2ClientService.GetClientAsync(LegacyClientId, cancellationToken);
        if (client is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { code = "OAUTH_CLIENT_NOT_FOUND", message = "OAuth2 client is not configured" });
        }

        // Generate OAuth2 tokens internally (bypassing authorization code flow for legacy adapter)
        var accessToken = await oauth2TokenService.GenerateAccessTokenAsync(user.Id, LegacyClientId, LegacyScope, cancellationToken);
        var (refreshToken, refreshHash) = await oauth2TokenService.GenerateRefreshTokenAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var familyId = Guid.NewGuid();

        // Save OAuth2 refresh token
        dbContext.OAuth2RefreshTokens.Add(new OAuth2RefreshToken
        {
            TokenHash = refreshHash,
            ClientId = LegacyClientId,
            UserId = user.Id,
            Scope = LegacyScope,
            FamilyId = familyId,
            PreviousTokenHash = null,
            ExpiresAtUtc = nowUtc.AddDays(oauth2Options.RefreshTokenTtlDays),
            RevokedAtUtc = null,
            CreatedAtUtc = nowUtc
        });

        // Save legacy UserSession for backward compatibility (legacy /me endpoint checks this table)
        var sessionId = Guid.NewGuid();
        dbContext.UserSessions.Add(new UserSession
        {
            Id = sessionId,
            UserId = user.Id,
            AccessTokenHash = TokenHashing.Hash(accessToken),
            RefreshTokenHash = TokenHashing.Hash(refreshToken),
            AccessTokenExpiresAtUtc = nowUtc.AddMinutes(oauth2Options.AccessTokenTtlMinutes),
            RefreshTokenExpiresAtUtc = nowUtc.AddDays(RefreshTokenTtlDays),
            CreatedAtUtc = nowUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        // Build legacy response format
        var userSummary = BuildUserSummaryResponse(user);
        var permissions = await BuildPermissionsAsync(user.Id, cancellationToken);

        SetOAuth2SessionCookies(accessToken, refreshToken);

        return Ok(new LoginResponse(
            new TokenPairResponse(accessToken, refreshToken, oauth2Options.AccessTokenTtlMinutes * 60),
            userSummary,
            permissions));
    }

    // ──────────────────────────────────────────────
    // Google OAuth registration (UNCHANGED — pre-auth flow)
    // ──────────────────────────────────────────────

    [HttpGet("register/google")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> StartGoogleRegistration(CancellationToken cancellationToken)
    {
        var state = GenerateOpaqueState();

        Response.Cookies.Append(GoogleStateCookieName, state, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMinutes(10),
            Path = "/"
        });

        try
        {
            var response = await registrationFlowService.StartGoogleRegistrationAsync(state, cancellationToken);
            return Redirect(response.AuthorizationUrl);
        }
        catch (InvalidOperationException ex) when (ex.Message == "GOOGLE_OAUTH_NOT_CONFIGURED")
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new { code = "GOOGLE_OAUTH_NOT_CONFIGURED", message = "google oauth is not configured" });
        }
    }

    [HttpGet("register/google/callback")]
    [ProducesResponseType(typeof(GoogleRegisterCallbackResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteGoogleRegistration([FromQuery] string? code, [FromQuery] string? state, CancellationToken cancellationToken)
    {
        var expectedState = Request.Cookies[GoogleStateCookieName];

        if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(expectedState) || !string.Equals(state, expectedState, StringComparison.Ordinal))
        {
            return BadRequest(new { code = "INVALID_OAUTH_STATE", message = "oauth state is invalid or expired" });
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new { code = "MISSING_OAUTH_CODE", message = "oauth code is required" });
        }

        try
        {
            var response = await registrationFlowService.CompleteGoogleRegistrationAsync(code, cancellationToken);
            Response.Cookies.Delete(GoogleStateCookieName, new CookieOptions { Path = "/" });
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "GOOGLE_OAUTH_NOT_CONFIGURED")
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new { code = "GOOGLE_OAUTH_NOT_CONFIGURED", message = "google oauth is not configured" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "GOOGLE_EMAIL_NOT_VERIFIED")
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "GOOGLE_EMAIL_NOT_VERIFIED", message = "google account email must be verified" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "GOOGLE_OAUTH_CODE_INVALID")
        {
            return Unauthorized(new { code = "GOOGLE_OAUTH_CODE_INVALID", message = "oauth code is invalid" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "EMAIL_ALREADY_REGISTERED")
        {
            return Conflict(new { code = "EMAIL_ALREADY_REGISTERED", message = "email already exists" });
        }
    }

    // ──────────────────────────────────────────────
    // GET /api/v1/auth/me — Already adapted in Fase 4 (verify deprecation headers present)
    // ──────────────────────────────────────────────

    [HttpGet("me")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        AddDeprecationHeaders();

        var accessToken = AccessTokenResolver.Resolve(Request);
        var response = await registrationFlowService.MeAsync(accessToken, cancellationToken);

        // If authenticated via OAuth2, extract scopes from JWT claims
        if (response.Authenticated && User.Identity?.IsAuthenticated == true)
        {
            var scopes = ExtractScopesFromJwt();
            if (scopes.Count > 0)
            {
                response = response with { Scopes = scopes };
            }
        }

        return Ok(response);
    }

    /// <summary>
    /// Extracts OAuth 2.0 scopes from the JWT token claims.
    /// The scope claim is a space-separated string (OAuth 2.0 standard).
    /// </summary>
    private IReadOnlyList<string> ExtractScopesFromJwt()
    {
        var scopeClaim = User.FindFirst("scope");
        if (scopeClaim is null || string.IsNullOrWhiteSpace(scopeClaim.Value))
        {
            return Array.Empty<string>();
        }

        return scopeClaim.Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    // ──────────────────────────────────────────────
    // POST /api/v1/auth/refresh — OAuth 2.0 adapter
    // Maps internally to POST /oauth2/token with grant_type=refresh_token
    // ──────────────────────────────────────────────

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        AddDeprecationHeaders();

        try
        {
            var refreshToken = Request.Cookies[RefreshTokenCookieName];

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                ClearSessionCookies();
                return Unauthorized(new { code = "INVALID_SESSION", message = "invalid or expired session" });
            }

            // Map to OAuth2 refresh_token grant internally
            var oauth2Response = await oauth2AuthorizationService.RefreshTokenAsync(refreshToken, LegacyClientId, cancellationToken);

            if (oauth2Response is null)
            {
                ClearSessionCookies();
                return Unauthorized(new { code = "INVALID_SESSION", message = "invalid or expired session" });
            }

            // Look up user to build legacy response
            var userId = await ResolveUserIdFromTokenAsync(oauth2Response.AccessToken, cancellationToken);
            if (!userId.HasValue)
            {
                ClearSessionCookies();
                return Unauthorized(new { code = "INVALID_SESSION", message = "invalid or expired session" });
            }

            var user = await dbContext.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);

            if (user is null || user.Status is UserStatus.Suspended or UserStatus.Deactivated)
            {
                ClearSessionCookies();
                return StatusCode(StatusCodes.Status403Forbidden, new { code = "USER_NOT_ALLOWED", message = "user is not allowed" });
            }

            // Also update legacy UserSession for backward compatibility
            var legacySession = await dbContext.UserSessions
                .FirstOrDefaultAsync(s => s.UserId == userId.Value && s.RevokedAtUtc == null, cancellationToken);

            if (legacySession is not null)
            {
                legacySession.AccessTokenHash = TokenHashing.Hash(oauth2Response.AccessToken);
                legacySession.RefreshTokenHash = TokenHashing.Hash(oauth2Response.RefreshToken);
                legacySession.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(oauth2Options.AccessTokenTtlMinutes);
                legacySession.RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenTtlDays);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var userSummary = user.Profile is null
                ? new UserSummaryResponse(
                    user.Id,
                    user.Email,
                    user.Email,
                    user.Status == UserStatus.Active ? "activo" : "suspendido",
                    "cliente",
                    user.OnboardingStatus,
                    user.EmailVerifiedAtUtc,
                    Array.Empty<string>())
                : new UserSummaryResponse(
                    user.Id,
                    user.Email,
                    $"{user.Profile.FirstName} {user.Profile.LastName}".Trim(),
                    user.Status == UserStatus.Active ? "activo" : "suspendido",
                    "cliente",
                    user.OnboardingStatus,
                    user.EmailVerifiedAtUtc,
                    Array.Empty<string>());

            // Resolve roles
            var roles = await dbContext.UserRoleAssignments
                .AsNoTracking()
                .Where(ra => ra.UserId == userId.Value && ra.Active)
                .Join(dbContext.Roles, ra => ra.RoleId, r => r.Id, (ra, r) => r.Code)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync(cancellationToken);

            userSummary = userSummary with { Roles = roles };

            // Update tipoCuenta
            var tipoCuenta = roles.Any(r => r.StartsWith("admin", StringComparison.OrdinalIgnoreCase))
                ? "administrador"
                : "cliente";
            userSummary = userSummary with { TipoCuenta = tipoCuenta };

            var permissions = await BuildPermissionsAsync(userId.Value, cancellationToken);

            SetOAuth2SessionCookies(oauth2Response.AccessToken, oauth2Response.RefreshToken);

            return Ok(new RefreshSessionResponse(
                new TokenPairResponse(oauth2Response.AccessToken, oauth2Response.RefreshToken, oauth2Response.ExpiresIn),
                userSummary,
                permissions));
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_SESSION")
        {
            ClearSessionCookies();
            return Unauthorized(new { code = "INVALID_SESSION", message = "invalid or expired session" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "USER_NOT_ALLOWED")
        {
            ClearSessionCookies();
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "USER_NOT_ALLOWED", message = "user is not allowed" });
        }
    }

    // ──────────────────────────────────────────────
    // POST /api/v1/auth/logout — OAuth 2.0 adapter
    // Maps internally to POST /oauth2/revoke
    // ──────────────────────────────────────────────

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        AddDeprecationHeaders();

        var refreshToken = Request.Cookies[RefreshTokenCookieName];

        // Revoke via OAuth2 revoke endpoint (RFC 7009: always succeeds even for unknown tokens)
        await oauth2AuthorizationService.RevokeTokenAsync(refreshToken ?? string.Empty, cancellationToken);

        // Clear legacy cookies
        ClearSessionCookies();

        return NoContent();
    }

    // ──────────────────────────────────────────────
    // Password management endpoints (UNCHANGED)
    // ──────────────────────────────────────────────

    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
        {
            return BadRequest(new { code = "EMAIL_INVALID", message = "email format is invalid" });
        }

        var response = await registrationFlowService.ForgotPasswordAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Accepted(response);
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ResetPasswordResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { code = "INVALID_REQUEST", message = "token and newPassword are required" });
        }

        try
        {
            var response = await registrationFlowService.ResetPasswordAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            ClearSessionCookies();
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_OR_EXPIRED_RESET_TOKEN")
        {
            return Unauthorized(new { code = "INVALID_OR_EXPIRED_RESET_TOKEN", message = "invalid or expired reset token" });
        }
    }

    [HttpGet("security-policy")]
    [ProducesResponseType(typeof(AuthSecurityPolicyResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSecurityPolicy(CancellationToken cancellationToken)
    {
        var response = await registrationFlowService.GetSecurityPolicyAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPut("security-policy")]
    [RequireRoles("admin_global")]
    [ProducesResponseType(typeof(AuthSecurityPolicyResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSecurityPolicy([FromBody] UpdateAuthSecurityPolicyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await registrationFlowService.UpdateSecurityPolicyAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_RESET_TTL")
        {
            return BadRequest(new { code = "INVALID_RESET_TTL", message = "passwordResetTtlMinutes must be between 5 and 1440" });
        }
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new System.Net.Mail.MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sets cookies with OAuth2 tokens (JWT RS256 access token + opaque refresh token).
    /// Uses the same cookie names as legacy system for seamless migration.
    /// </summary>
    private void SetOAuth2SessionCookies(string accessToken, string refreshToken)
    {
        var accessCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddSeconds(oauth2Options.AccessTokenTtlMinutes * 60),
            Path = "/"
        };

        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(RefreshTokenTtlDays),
            Path = "/api/v1/auth"
        };

        Response.Cookies.Append(AccessTokenCookieName, accessToken, accessCookieOptions);
        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, refreshCookieOptions);
    }

    private void ClearSessionCookies()
    {
        Response.Cookies.Delete(AccessTokenCookieName, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/api/v1/auth" });
    }

    private static string GenerateOpaqueState()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// PBKDF2-SHA256 password verification (matches RegistrationFlowService implementation).
    /// </summary>
    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('$', 5, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5 || parts[0] != "PBKDF2")
        {
            return false;
        }

        var iterations = int.Parse(parts[1]);
        var salt = Convert.FromBase64String(parts[3]);
        var expectedHash = Convert.FromBase64String(parts[4]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    /// <summary>
    /// Builds a UserSummaryResponse from a loaded User entity.
    /// </summary>
    private static UserSummaryResponse BuildUserSummaryResponse(User user)
    {
        var roles = user.RoleAssignments
            .Where(ra => ra.Active)
            .Select(ra => ra.Role.Code)
            .Distinct()
            .OrderBy(r => r)
            .ToList();

        var displayName = user.Profile is null
            ? user.Email
            : $"{user.Profile.FirstName} {user.Profile.LastName}".Trim();

        var tipoCuenta = roles.Any(r => r.StartsWith("admin", StringComparison.OrdinalIgnoreCase))
            ? "administrador"
            : "cliente";

        var estado = user.Status switch
        {
            UserStatus.Active => "activo",
            UserStatus.Suspended => "suspendido",
            UserStatus.Deactivated => "desactivado",
            _ => "activo"
        };

        return new UserSummaryResponse(
            user.Id,
            user.Email,
            displayName,
            estado,
            tipoCuenta,
            user.OnboardingStatus,
            user.EmailVerifiedAtUtc,
            roles);
    }

    /// <summary>
    /// Builds permissions list for a user by joining role assignments with resource permissions.
    /// </summary>
    private async Task<IReadOnlyList<ResourcePermissionResponse>> BuildPermissionsAsync(Guid userId, CancellationToken ct)
    {
        var rawPermissions = await dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Active)
            .Join(dbContext.RoleResourcePermissions,
                ura => ura.RoleId,
                permission => permission.RoleId,
                (ura, permission) => permission)
            .Join(dbContext.ApiResources,
                permission => permission.ResourceId,
                resource => resource.Id,
                (permission, resource) => new
                {
                    resource.Code,
                    permission.CanView,
                    permission.CanWrite,
                    permission.CanUpdate,
                    permission.CanDelete,
                    permission.CanAll
                })
            .ToListAsync(ct);

        return rawPermissions
            .GroupBy(x => x.Code)
            .Select(group => new ResourcePermissionResponse(
                group.Key,
                group.Any(x => x.CanView),
                group.Any(x => x.CanWrite),
                group.Any(x => x.CanUpdate),
                group.Any(x => x.CanDelete),
                group.Any(x => x.CanAll)))
            .OrderBy(x => x.ResourceCode)
            .ToList();
    }

    /// <summary>
    /// Resolves a user ID from an OAuth2 access token (JWT RS256) by reading the 'sub' claim.
    /// </summary>
    private static async Task<Guid?> ResolveUserIdFromTokenAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(accessToken);
            var sub = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            if (!string.IsNullOrEmpty(sub) && Guid.TryParse(sub, out var userId))
            {
                return userId;
            }
        }
        catch
        {
            // Invalid token — return null
        }

        return null;
    }
}
