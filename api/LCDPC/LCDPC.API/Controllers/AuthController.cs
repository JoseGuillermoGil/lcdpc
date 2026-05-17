using LCDPC.Application.Users.Auth;
using LCDPC.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace LCDPC.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IRegistrationFlowService registrationFlowService, IConfiguration configuration) : ControllerBase
{
    private const string AccessTokenCookieName = "lcdpc_at";
    private const string RefreshTokenCookieName = "lcdpc_rt";
    private const string GoogleStateCookieName = "lcdpc_google_state";
    private int RefreshTokenTtlDays => int.TryParse(configuration["Auth:RefreshTokenTtlDays"], out var ttlDays) ? ttlDays : 30;

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

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { code = "INVALID_REQUEST", message = "email and password are required" });
        }

        if (!IsValidEmail(request.Email))
        {
            return BadRequest(new { code = "EMAIL_INVALID", message = "email format is invalid" });
        }

        try
        {
            var response = await registrationFlowService.LoginAsync(request, cancellationToken);
            SetSessionCookies(response.TokenPair);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_CREDENTIALS")
        {
            return Unauthorized(new { code = "INVALID_CREDENTIALS", message = "invalid email or password" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "USER_NOT_ALLOWED")
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "USER_NOT_ALLOWED", message = "user is not allowed to login" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "ONBOARDING_INCOMPLETE")
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "ONBOARDING_INCOMPLETE", message = "onboarding is not complete" });
        }
    }

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
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[AccessTokenCookieName];
        var response = await registrationFlowService.MeAsync(accessToken, cancellationToken);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        try
        {
            var refreshToken = Request.Cookies[RefreshTokenCookieName];
            var response = await registrationFlowService.RefreshAsync(refreshToken, cancellationToken);
            SetSessionCookies(response.TokenPair);
            return Ok(response);
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

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        await registrationFlowService.LogoutAsync(refreshToken, cancellationToken);
        ClearSessionCookies();
        return NoContent();
    }

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

    private void SetSessionCookies(TokenPairResponse tokens)
    {
        var accessCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresInSeconds),
            Path = "/"
        };

        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(RefreshTokenTtlDays),
            Path = "/"
        };

        Response.Cookies.Append(AccessTokenCookieName, tokens.AccessToken, accessCookieOptions);
        Response.Cookies.Append(RefreshTokenCookieName, tokens.RefreshToken, refreshCookieOptions);
    }

    private void ClearSessionCookies()
    {
        Response.Cookies.Delete(AccessTokenCookieName, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/" });
    }

    private static string GenerateOpaqueState()
    {
        return Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}