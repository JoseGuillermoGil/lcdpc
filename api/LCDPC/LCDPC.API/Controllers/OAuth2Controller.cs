using LCDPC.API.Security;
using LCDPC.Application.OAuth2;
using LCDPC.Domain.Entities.Users;
using LCDPC.Infrastructure.OAuth2;
using LCDPC.Infrastructure.Persistence;
using LCDPC.Infrastructure.Users.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace LCDPC.API.Controllers;

/// <summary>
/// OAuth 2.0 Authorization Server endpoints (RFC 6749, RFC 7636 PKCE, RFC 7662 Introspection, RFC 7009 Revocation).
/// These endpoints are unauthenticated by design — they are part of the auth flow itself.
/// </summary>
[ApiController]
[Route("oauth2")]
public class OAuth2Controller(
    IOAuth2AuthorizationService authorizationService,
    IOAuth2ClientService clientService,
    IGoogleOAuthService googleOAuthService,
    AppDbContext dbContext,
    GoogleOAuthOptions googleOptions,
    OAuth2Options oauth2Options) : ControllerBase
{
    private const string GoogleStateCookieName = "lcdpc_google_oauth_state";
    private const string GoogleCallbackCookieName = "lcdpc_google_oauth_callback";

    // ──────────────────────────────────────────────
    // GET /oauth2/authorize — Authorization Endpoint (RFC 6749 §4.1.1)
    // ──────────────────────────────────────────────

    [HttpGet("authorize")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Authorize(
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string response_type = "code",
        [FromQuery] string scope = "openid email profile",
        [FromQuery] string? state = null,
        [FromQuery] string? code_challenge = null,
        [FromQuery] string code_challenge_method = "S256",
        [FromQuery] string? provider = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(client_id))
        {
            return BadRequest(new
            {
                error = "invalid_request",
                error_description = "client_id is required"
            });
        }

        if (string.IsNullOrWhiteSpace(redirect_uri))
        {
            return BadRequest(new
            {
                error = "invalid_request",
                error_description = "redirect_uri is required"
            });
        }

        // If provider=google, redirect to Google OAuth first
        if (string.Equals(provider, "google", StringComparison.OrdinalIgnoreCase))
        {
            return await RedirectToGoogleOAuthAsync(client_id, redirect_uri, response_type, scope, state, code_challenge, code_challenge_method, ct);
        }

        // Resolve user identity from authenticated session (cookie or JWT)
        var userId = ResolveUserId();

        var request = new OAuth2AuthorizeRequest(
            ClientId: client_id,
            RedirectUri: redirect_uri,
            ResponseType: response_type,
            Scope: scope,
            State: state ?? string.Empty,
            CodeChallenge: code_challenge,
            CodeChallengeMethod: code_challenge_method);

        var result = await authorizationService.AuthorizeAsync(request, userId, ct);

        if (result.Success && !string.IsNullOrEmpty(result.RedirectUrl))
        {
            return Redirect(result.RedirectUrl);
        }

        // Return error — either as redirect to redirect_uri (if valid) or as JSON
        if (!string.IsNullOrEmpty(result.RedirectUrl))
        {
            return Redirect(result.RedirectUrl);
        }

        return BadRequest(new
        {
            error = result.ErrorCode ?? "invalid_request",
            error_description = result.ErrorDescription ?? "Authorization request failed"
        });
    }

    /// <summary>
    /// Redirects the user to Google OAuth when provider=google is specified in the authorize request.
    /// Stores the original OAuth2 authorize params in a cookie for the callback to reconstruct the flow.
    /// </summary>
    private async Task<IActionResult> RedirectToGoogleOAuthAsync(
        string clientId, string redirectUri, string responseType, string scope,
        string? state, string? codeChallenge, string codeChallengeMethod, CancellationToken ct)
    {
        // Check if Google OAuth is configured
        if (string.IsNullOrWhiteSpace(googleOptions.ClientId) ||
            string.IsNullOrWhiteSpace(googleOptions.ClientSecret))
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new
            {
                error = "google_oauth_not_configured",
                error_description = "Google OAuth is not configured on the server."
            });
        }

        // Validate the client exists and redirect_uri is allowed
        var client = await clientService.GetClientAsync(clientId, ct);
        if (client is null)
        {
            return BadRequest(new
            {
                error = "invalid_client",
                error_description = "The client_id is not registered."
            });
        }

        if (!clientService.ValidateRedirectUri(client, redirectUri))
        {
            return BadRequest(new
            {
                error = "invalid_redirect_uri",
                error_description = "The redirect_uri is not registered for this client."
            });
        }

        // Generate a state for Google OAuth (separate from the OAuth2 state)
        var googleState = GenerateOpaqueToken();
        var nonce = GenerateOpaqueToken();

        // Build callback data to pass original OAuth2 params to the callback handler
        var callbackData = $"client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&response_type={Uri.EscapeDataString(responseType)}" +
            $"&scope={Uri.EscapeDataString(scope)}" +
            $"&state={Uri.EscapeDataString(state ?? string.Empty)}" +
            $"&code_challenge={Uri.EscapeDataString(codeChallenge ?? string.Empty)}" +
            $"&code_challenge_method={Uri.EscapeDataString(codeChallengeMethod)}";

        // Store callback data and google state in cookies
        Response.Cookies.Append(GoogleStateCookieName, googleState, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMinutes(10),
            Path = "/"
        });

        Response.Cookies.Append(GoogleCallbackCookieName, callbackData, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMinutes(10),
            Path = "/"
        });

        // Redirect to Google OAuth with the combined state (googleState contains the OAuth2 state for correlation)
        var googleUrl = googleOAuthService.GetAuthorizationUrl(googleState, nonce);
        return Redirect(googleUrl);
    }

    private static string GenerateOpaqueToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    // ──────────────────────────────────────────────
    // POST /oauth2/token — Token Endpoint (RFC 6749 §4.1.3)
    // Content-Type: application/x-www-form-urlencoded
    // ──────────────────────────────────────────────

    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(OAuth2TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [RateLimit(maxRequests: 10, windowSeconds: 60)]
    public async Task<IActionResult> Token(
        [FromForm] string grant_type,
        [FromForm] string? code = null,
        [FromForm] string? redirect_uri = null,
        [FromForm] string? code_verifier = null,
        [FromForm] string? refresh_token = null,
        [FromForm] string? client_id = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(grant_type))
        {
            return BadRequest(new
            {
                error = "invalid_request",
                error_description = "grant_type is required"
            });
        }

        if (string.IsNullOrWhiteSpace(client_id))
        {
            return BadRequest(new
            {
                error = "invalid_client",
                error_description = "client_id is required"
            });
        }

        OAuth2TokenResponse? response = grant_type switch
        {
            "authorization_code" => await HandleAuthorizationCodeGrant(code, redirect_uri, code_verifier, client_id, ct),
            "refresh_token" => await HandleRefreshTokenGrant(refresh_token, client_id, ct),
            _ => CreateTokenError("unsupported_grant_type", $"Grant type '{grant_type}' is not supported.")
        };

        if (response is null)
        {
            return BadRequest(new
            {
                error = "invalid_grant",
                error_description = "The authorization code or refresh token is invalid, expired, or revoked."
            });
        }

        return Ok(response);
    }

    // ──────────────────────────────────────────────
    // POST /oauth2/introspect — Token Introspection (RFC 7662)
    // Content-Type: application/x-www-form-urlencoded
    // ──────────────────────────────────────────────

    [HttpPost("introspect")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(OAuth2IntrospectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Introspect(
        [FromForm] string token,
        [FromForm] string? token_type_hint = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new
            {
                error = "invalid_request",
                error_description = "token is required"
            });
        }

        var result = await authorizationService.IntrospectTokenAsync(token, ct);
        return Ok(result);
    }

    // ──────────────────────────────────────────────
    // POST /oauth2/revoke — Token Revocation (RFC 7009)
    // Content-Type: application/x-www-form-urlencoded
    // ──────────────────────────────────────────────

    [HttpPost("revoke")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Revoke(
        [FromForm] string token,
        [FromForm] string? token_type_hint = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new
            {
                error = "invalid_request",
                error_description = "token is required"
            });
        }

        // RFC 7009: always return 200, even if the token doesn't exist (prevent probing)
        await authorizationService.RevokeTokenAsync(token, ct);
        return Ok(new { });
    }

    // ──────────────────────────────────────────────
    // GET /oauth2/callback/google — Google OAuth Callback
    // Handles the redirect from Google after user authorization.
    // ──────────────────────────────────────────────

    [HttpGet("callback/google")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        CancellationToken ct = default)
    {
        // Validate state from cookie (CSRF protection)
        var expectedState = Request.Cookies[GoogleStateCookieName];

        if (string.IsNullOrWhiteSpace(state) ||
            string.IsNullOrWhiteSpace(expectedState) ||
            !string.Equals(state, expectedState, StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                error = "invalid_state",
                error_description = "OAuth state is invalid or expired. Possible CSRF attack."
            });
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new
            {
                error = "missing_code",
                error_description = "Google authorization code is required."
            });
        }

        // Check if Google OAuth is configured
        if (string.IsNullOrWhiteSpace(googleOptions.ClientId) ||
            string.IsNullOrWhiteSpace(googleOptions.ClientSecret))
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new
            {
                error = "google_oauth_not_configured",
                error_description = "Google OAuth is not configured on the server."
            });
        }

        try
        {
            // 1. Exchange Google code for user info
            var userInfo = await googleOAuthService.ExchangeCodeForUserInfoAsync(code, ct);

            if (!userInfo.EmailVerified)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "email_not_verified",
                    error_description = "Google account email must be verified."
                });
            }

            // 2. Find or create the local user
            var user = await dbContext.Users
                .Include(u => u.Profile)
                .Include(u => u.RoleAssignments)
                    .ThenInclude(ra => ra.Role)
                .FirstOrDefaultAsync(u => u.Email == userInfo.Email, ct);

            if (user is null)
            {
                // Create new user from Google info
                user = await CreateGoogleUserAsync(userInfo, ct);
            }

            // 3. Update existing user profile if needed
            await UpdateUserProfileFromGoogleAsync(user, userInfo, ct);

            // 4. Check if user is allowed to login
            if (user.Status == UserStatus.Suspended || user.Status == UserStatus.Deactivated)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "user_not_allowed",
                    error_description = "User account is suspended or deactivated."
                });
            }

            // 5. Parse the callback data from state (contains the original OAuth2 authorize params)
            var callbackData = Request.Cookies[GoogleCallbackCookieName];
            var originalRequest = ParseCallbackData(callbackData);

            // 6. Generate our own authorization code and redirect
            var authRequest = new OAuth2AuthorizeRequest(
                ClientId: originalRequest.ClientId ?? "lcdpc-web",
                RedirectUri: originalRequest.RedirectUri ?? GetDefaultRedirectUri(),
                ResponseType: "code",
                Scope: originalRequest.Scope ?? "openid email profile",
                State: originalRequest.State ?? state,
                CodeChallenge: originalRequest.CodeChallenge,
                CodeChallengeMethod: originalRequest.CodeChallengeMethod ?? "S256");

            var authResult = await authorizationService.AuthorizeAsync(authRequest, user.Id, ct);

            // Clean up cookies
            Response.Cookies.Delete(GoogleStateCookieName, new CookieOptions { Path = "/" });
            Response.Cookies.Delete(GoogleCallbackCookieName, new CookieOptions { Path = "/" });

            if (authResult.Success && !string.IsNullOrEmpty(authResult.RedirectUrl))
            {
                return Redirect(authResult.RedirectUrl);
            }

            // Fallback: redirect with error
            var redirectUri = originalRequest.RedirectUri ?? GetDefaultRedirectUri();
            var errorUrl = $"{redirectUri}?error={Uri.EscapeDataString(authResult.ErrorCode ?? "authorization_failed")}&error_description={Uri.EscapeDataString(authResult.ErrorDescription ?? "Failed to complete Google OAuth flow.")}&state={Uri.EscapeDataString(originalRequest.State ?? state)}";
            return Redirect(errorUrl);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("GOOGLE_OAUTH_CODE_INVALID"))
        {
            return Unauthorized(new
            {
                error = "invalid_grant",
                error_description = "Google authorization code is invalid or expired."
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "google_api_error",
                error_description = "Failed to communicate with Google OAuth API."
            });
        }
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────

    private Guid? ResolveUserId()
    {
        var subject = User.FindFirst("sub")
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (subject != null && Guid.TryParse(subject.Value, out var userId))
        {
            return userId;
        }

        // Also check for cookie-based session (legacy compatibility)
        // The auth middleware will have set the ClaimsPrincipal if a valid session exists
        return null;
    }

    private async Task<OAuth2TokenResponse?> HandleAuthorizationCodeGrant(
        string? code, string? redirectUri, string? codeVerifier, string clientId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return CreateTokenError("invalid_request", "code is required for authorization_code grant.");
        }

        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return CreateTokenError("invalid_request", "redirect_uri is required for authorization_code grant.");
        }

        if (string.IsNullOrWhiteSpace(codeVerifier))
        {
            return CreateTokenError("invalid_request", "code_verifier is required for PKCE.");
        }

        return await authorizationService.ExchangeCodeAsync(code, codeVerifier, redirectUri, clientId, ct);
    }

    private async Task<OAuth2TokenResponse?> HandleRefreshTokenGrant(
        string? refreshToken, string clientId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return CreateTokenError("invalid_request", "refresh_token is required for refresh_token grant.");
        }

        return await authorizationService.RefreshTokenAsync(refreshToken, clientId, ct);
    }

    private static OAuth2TokenResponse? CreateTokenError(string error, string description)
    {
        // Return null to signal the controller to return an error response
        return null;
    }

    private async Task<User> CreateGoogleUserAsync(GoogleUserInfo userInfo, CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;

        // Assign default "cliente" role
        var clientRole = await dbContext.Roles
            .FirstOrDefaultAsync(r => r.Code == "cliente", ct);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = userInfo.Email,
            PasswordHash = $"google_oauth_{GenerateRandomString(64)}", // OAuth users don't need password
            OnboardingStatus = "complete", // Google users skip registration steps
            EmailVerifiedAtUtc = userInfo.EmailVerified ? nowUtc : null,
            Status = UserStatus.Active,
            CreatedAtUtc = nowUtc
        };

        // Create profile from Google data
        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FirstName = userInfo.GivenName ?? userInfo.Name?.Split(' ').FirstOrDefault() ?? string.Empty,
            LastName = userInfo.FamilyName ?? (userInfo.Name?.Split(' ').Skip(1).FirstOrDefault() ?? string.Empty),
            IdentityDocument = $"GOOGLE_{userInfo.Email.Replace("@", "_")}", // Placeholder
            WhatsAppPhone = string.Empty,
            FullAddress = string.Empty,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        user.Profile = profile;

        if (clientRole is not null)
        {
            user.RoleAssignments = new List<UserRoleAssignment>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = clientRole.Id,
                    SedeIds = Array.Empty<Guid>(),
                    Active = true,
                    CreatedAtUtc = nowUtc
                }
            };
        }

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(ct);

        return user;
    }

    private async Task UpdateUserProfileFromGoogleAsync(User user, GoogleUserInfo userInfo, CancellationToken ct)
    {
        var needsUpdate = false;

        if (user.Profile is null)
        {
            var nowUtc = DateTime.UtcNow;
            user.Profile = new Profile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FirstName = userInfo.GivenName ?? userInfo.Name?.Split(' ').FirstOrDefault() ?? string.Empty,
                LastName = userInfo.FamilyName ?? string.Empty,
                IdentityDocument = $"GOOGLE_{user.Email.Replace("@", "_")}",
                WhatsAppPhone = string.Empty,
                FullAddress = string.Empty,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };
            needsUpdate = true;
        }
        else if (string.IsNullOrEmpty(user.Profile.FirstName) || string.IsNullOrEmpty(user.Profile.LastName))
        {
            if (string.IsNullOrEmpty(user.Profile.FirstName) && !string.IsNullOrEmpty(userInfo.GivenName))
            {
                user.Profile.FirstName = userInfo.GivenName;
                needsUpdate = true;
            }
            if (string.IsNullOrEmpty(user.Profile.LastName) && !string.IsNullOrEmpty(userInfo.FamilyName))
            {
                user.Profile.LastName = userInfo.FamilyName;
                needsUpdate = true;
            }
        }

        if (user.EmailVerifiedAtUtc == null && userInfo.EmailVerified)
        {
            user.EmailVerifiedAtUtc = DateTime.UtcNow;
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            if (user.Profile is not null)
            {
                user.Profile.UpdatedAtUtc = DateTime.UtcNow;
            }
            await dbContext.SaveChangesAsync(ct);
        }
    }

    private static OAuth2AuthorizeRequest ParseCallbackData(string? callbackData)
    {
        if (string.IsNullOrWhiteSpace(callbackData))
        {
            return new OAuth2AuthorizeRequest(
                ClientId: "lcdpc-web",
                RedirectUri: string.Empty,
                ResponseType: "code",
                Scope: "openid email profile",
                State: string.Empty,
                CodeChallenge: null,
                CodeChallengeMethod: "S256");
        }

        try
        {
            var parts = callbackData.Split('&');
            var dict = parts
                .Select(p => p.Split('=', 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));

            return new OAuth2AuthorizeRequest(
                ClientId: dict.GetValueOrDefault("client_id", "lcdpc-web"),
                RedirectUri: dict.GetValueOrDefault("redirect_uri", string.Empty),
                ResponseType: dict.GetValueOrDefault("response_type", "code"),
                Scope: dict.GetValueOrDefault("scope", "openid email profile"),
                State: dict.GetValueOrDefault("state", string.Empty),
                CodeChallenge: dict.TryGetValue("code_challenge", out var cc) ? cc : null,
                CodeChallengeMethod: dict.GetValueOrDefault("code_challenge_method", "S256"));
        }
        catch
        {
            return new OAuth2AuthorizeRequest(
                ClientId: "lcdpc-web",
                RedirectUri: string.Empty,
                ResponseType: "code",
                Scope: "openid email profile",
                State: string.Empty,
                CodeChallenge: null,
                CodeChallengeMethod: "S256");
        }
    }

    private string GetDefaultRedirectUri()
    {
        // Get the first registered redirect URI for the default client
        var firstRedirectUri = oauth2Options.Clients
            .FirstOrDefault()?.RedirectUris
            .FirstOrDefault() ?? "http://localhost:4200/auth/callback";

        return firstRedirectUri;
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var result = new char[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }
        return new string(result);
    }
}
