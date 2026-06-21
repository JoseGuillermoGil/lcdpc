using LCDPC.Application.Users.Auth;
using LCDPC.Application.OAuth2;
using LCDPC.Domain.Entities.Users;
using LCDPC.Infrastructure.OAuth2;
using LCDPC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LCDPC.Infrastructure.Users.Auth;

public sealed class RegistrationFlowService(
    AppDbContext dbContext,
    AuthSecurityOptions authOptions,
    GoogleOAuthOptions googleOAuthOptions,
    IEmailService emailService) : IRegistrationFlowService
{
    private const string PendingEmailVerificationStatus = "pending_email_verification";
    private const string PendingProfileStatus = "pending_profile";
    private const string OtpAttemptsExceededStatus = "otp_attempts_exceeded";
    private const string ActiveStatus = "active";

    public async Task<StartRegistrationResponse> StartAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var nowUtc = DateTime.UtcNow;

        var existingUser = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (existingUser)
        {
            throw new InvalidOperationException("EMAIL_ALREADY_REGISTERED");
        }

        var flow = await dbContext.RegistrationFlows
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (flow is null)
        {
            flow = new RegistrationFlow
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                CreatedAtUtc = nowUtc
            };

            ResetEmailVerificationFlow(flow, nowUtc);
            dbContext.RegistrationFlows.Add(flow);
        }
        else if (flow.Status == PendingProfileStatus)
        {
            ResetEmailVerificationFlow(flow, nowUtc);
        }
        else if (flow.Status == OtpAttemptsExceededStatus && flow.OtpBlockedUntilUtc > nowUtc)
        {
            throw new InvalidOperationException("OTP_COOLDOWN_ACTIVE");
        }
        else if (flow.Status == ActiveStatus)
        {
            throw new InvalidOperationException("EMAIL_ALREADY_REGISTERED");
        }
        else
        {
            ResetEmailVerificationFlow(flow, nowUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await emailService.SendOtpAsync(normalizedEmail, flow.OtpCode, cancellationToken);

        return new StartRegistrationResponse(
            flow.Id,
            flow.Status,
            new OtpPolicyResponse(
                RegistrationFlowDefaults.OtpTtlMinutes,
                RegistrationFlowDefaults.OtpMaxAttempts,
                RegistrationFlowDefaults.OtpCooldownMinutes));
    }

    public async Task<VerifyEmailRegistrationResponse> VerifyEmailAsync(Guid flowId, string otp, CancellationToken cancellationToken = default)
    {
        var flow = await dbContext.RegistrationFlows.FirstOrDefaultAsync(x => x.Id == flowId, cancellationToken);
        var nowUtc = DateTime.UtcNow;

        if (flow is null)
        {
            throw new InvalidOperationException("FLOW_NOT_FOUND");
        }

        if (flow.Status == PendingProfileStatus)
        {
            ResetEmailVerificationFlow(flow, nowUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("OTP_EXPIRED");
        }

        if (flow.Status != PendingEmailVerificationStatus)
        {
            throw new InvalidOperationException("FLOW_INVALID_STATUS");
        }

        if (flow.OtpExpiresAtUtc < nowUtc)
        {
            ResetEmailVerificationFlow(flow, nowUtc);
            await dbContext.SaveChangesAsync(cancellationToken);

            throw new InvalidOperationException("OTP_EXPIRED");
        }

        flow.OtpAttempts += 1;
        if (!string.Equals(flow.OtpCode, otp.Trim(), StringComparison.Ordinal))
        {
            if (flow.OtpAttempts >= RegistrationFlowDefaults.OtpMaxAttempts)
            {
                flow.OtpBlockedUntilUtc = DateTime.UtcNow.AddMinutes(RegistrationFlowDefaults.OtpCooldownMinutes);
                flow.Status = OtpAttemptsExceededStatus;
                await dbContext.SaveChangesAsync(cancellationToken);
                throw new InvalidOperationException("OTP_ATTEMPTS_EXCEEDED");
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("OTP_INVALID");
        }

        flow.Status = PendingProfileStatus;
        flow.VerifiedAtUtc = DateTime.UtcNow;
        flow.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        // TODO: replace this emulator path with the real email sender integration.
        return new VerifyEmailRegistrationResponse(flow.Id, flow.Status);
    }

    public async Task<CompleteProfileRegistrationResponse> CompleteProfileAsync(CompleteProfileRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var flow = await dbContext.RegistrationFlows.FirstOrDefaultAsync(x => x.Id == request.FlowId, cancellationToken);

        if (flow is null)
        {
            throw new InvalidOperationException("FLOW_NOT_FOUND");
        }

        if (flow.Status != PendingProfileStatus)
        {
            throw new InvalidOperationException("FLOW_INVALID_STATUS");
        }

        var normalizedEmail = flow.Email.Trim().ToLowerInvariant();
        var existingUser = await dbContext.Users.AsNoTracking().AnyAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (existingUser)
        {
            throw new InvalidOperationException("EMAIL_ALREADY_REGISTERED");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = HashPassword(request.Password),
            EmailVerifiedAtUtc = flow.VerifiedAtUtc,
            OnboardingStatus = "active",
            Status = UserStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };

        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FirstName = request.Nombres.Trim(),
            LastName = request.Apellidos.Trim(),
            IdentityDocument = request.DocumentoIdentidad.Trim(),
            Rif = string.IsNullOrWhiteSpace(request.Rif) ? null : request.Rif.Trim(),
            WhatsAppPhone = request.TelefonoWhatsApp.Trim(),
            FullAddress = request.DireccionCompleta.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var clientRole = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == "cliente", cancellationToken);

        if (clientRole is not null)
        {
            var assignment = new UserRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = clientRole.Id,
                Active = true,
                SedeIds = Array.Empty<Guid>(),
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.UserRoleAssignments.Add(assignment);
        }

        flow.Status = ActiveStatus;
        flow.UpdatedAtUtc = DateTime.UtcNow;

        dbContext.Users.Add(user);
        dbContext.Profiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CompleteProfileRegistrationResponse(user.Id, "activo", "cliente");
    }

    public async Task<MeResponse> MeAsync(string? accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return new MeResponse(false, null, Array.Empty<ResourcePermissionResponse>(), null);
        }

        var hashedAccessToken = TokenHashing.Hash(accessToken.Trim());

        var session = await dbContext.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.AccessTokenHash == hashedAccessToken
                     && x.RevokedAtUtc == null
                     && x.AccessTokenExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);

        if (session is null)
        {
            return new MeResponse(false, null, Array.Empty<ResourcePermissionResponse>(), null);
        }

        var userSummary = await BuildUserSummaryAsync(session.UserId, cancellationToken);
        var permissions = await BuildPermissionsAsync(session.UserId, cancellationToken);
        var expiresInSeconds = Math.Max(0, (int)(session.AccessTokenExpiresAtUtc - DateTime.UtcNow).TotalSeconds);

        return new MeResponse(true, userSummary, permissions, expiresInSeconds);
    }

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var policy = await GetOrCreateSecurityPolicyAsync(cancellationToken);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        string? resetToken = null;

        if (user is not null)
        {
            var nowUtc = DateTime.UtcNow;

            var activeTokens = await dbContext.PasswordResetTokens
                .Where(x => x.UserId == user.Id && x.UsedAtUtc == null && x.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.RevokedAtUtc = nowUtc;
            }

            resetToken = GenerateOpaqueToken();

            dbContext.PasswordResetTokens.Add(new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = TokenHashing.Hash(resetToken),
                ExpiresAtUtc = nowUtc.AddMinutes(policy.PasswordResetTtlMinutes),
                CreatedAtUtc = nowUtc
            });

            dbContext.AuditLogs.Add(CreateAuditLog(
                user.Id,
                "auth.password.forgot_requested",
                ipAddress,
                new { normalizedEmail }));

            await dbContext.SaveChangesAsync(cancellationToken);

            await emailService.SendPasswordResetAsync(normalizedEmail, resetToken, cancellationToken);
        }
        else
        {
            dbContext.AuditLogs.Add(CreateAuditLog(
                null,
                "auth.password.forgot_requested_unknown_email",
                ipAddress,
                new { normalizedEmail }));

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ForgotPasswordResponse(
            "accepted",
            "if the account exists, a reset instruction has been generated",
            null);
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new InvalidOperationException("INVALID_OR_EXPIRED_RESET_TOKEN");
        }

        var tokenHash = TokenHashing.Hash(request.Token.Trim());
        var policy = await GetOrCreateSecurityPolicyAsync(cancellationToken);

        var resetToken = await dbContext.PasswordResetTokens
            .FirstOrDefaultAsync(
                x => x.TokenHash == tokenHash
                     && x.UsedAtUtc == null
                     && x.RevokedAtUtc == null
                     && x.ExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);

        if (resetToken is null)
        {
            dbContext.AuditLogs.Add(CreateAuditLog(
                null,
                "auth.password.reset_invalid_token",
                ipAddress,
                null));
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("INVALID_OR_EXPIRED_RESET_TOKEN");
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == resetToken.UserId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("INVALID_OR_EXPIRED_RESET_TOKEN");
        }

        var nowUtc = DateTime.UtcNow;
        user.PasswordHash = HashPassword(request.NewPassword);
        resetToken.UsedAtUtc = nowUtc;

        var sessionsRevoked = false;
        if (policy.RevokeSessionsOnPasswordReset)
        {
            var activeSessions = await dbContext.UserSessions
                .Where(x => x.UserId == user.Id && x.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var session in activeSessions)
            {
                session.RevokedAtUtc = nowUtc;
            }

            sessionsRevoked = activeSessions.Count > 0;
        }

        dbContext.AuditLogs.Add(CreateAuditLog(
            user.Id,
            "auth.password.reset_completed",
            ipAddress,
            new { sessionsRevoked, policy.PasswordResetTtlMinutes, policy.RevokeSessionsOnPasswordReset }));

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResetPasswordResponse("completed", sessionsRevoked);
    }

    public async Task<AuthSecurityPolicyResponse> GetSecurityPolicyAsync(CancellationToken cancellationToken = default)
    {
        var policy = await GetOrCreateSecurityPolicyAsync(cancellationToken);
        return new AuthSecurityPolicyResponse(policy.PasswordResetTtlMinutes, policy.RevokeSessionsOnPasswordReset);
    }

    public async Task<AuthSecurityPolicyResponse> UpdateSecurityPolicyAsync(UpdateAuthSecurityPolicyRequest request, CancellationToken cancellationToken = default)
    {
        if (request.PasswordResetTtlMinutes < 5 || request.PasswordResetTtlMinutes > 1440)
        {
            throw new InvalidOperationException("INVALID_RESET_TTL");
        }

        var policy = await GetOrCreateSecurityPolicyAsync(cancellationToken);
        policy.PasswordResetTtlMinutes = request.PasswordResetTtlMinutes;
        policy.RevokeSessionsOnPasswordReset = request.RevokeSessionsOnPasswordReset;
        policy.UpdatedAtUtc = DateTime.UtcNow;

        dbContext.AuditLogs.Add(CreateAuditLog(
            null,
            "auth.security.policy_updated",
            null,
            new { request.PasswordResetTtlMinutes, request.RevokeSessionsOnPasswordReset }));

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthSecurityPolicyResponse(policy.PasswordResetTtlMinutes, policy.RevokeSessionsOnPasswordReset);
    }

    public Task<GoogleRegisterStartResponse> StartGoogleRegistrationAsync(string state, CancellationToken cancellationToken = default)
    {
        EnsureGoogleOAuthConfigured();

        var encodedClientId = UrlEncoder.Default.Encode(googleOAuthOptions.ClientId!);
        var encodedRedirectUri = UrlEncoder.Default.Encode(googleOAuthOptions.RedirectUri!);
        var encodedScope = UrlEncoder.Default.Encode(googleOAuthOptions.Scope);
        var encodedState = UrlEncoder.Default.Encode(state);

        var authorizationUrl =
            $"https://accounts.google.com/o/oauth2/v2/auth?response_type=code&client_id={encodedClientId}&redirect_uri={encodedRedirectUri}&scope={encodedScope}&state={encodedState}&prompt=select_account&access_type=offline";

        return Task.FromResult(new GoogleRegisterStartResponse(authorizationUrl, state, 600));
    }

    public async Task<GoogleRegisterCallbackResponse> CompleteGoogleRegistrationAsync(string code, CancellationToken cancellationToken = default)
    {
        EnsureGoogleOAuthConfigured();

        var tokenResponse = await ExchangeGoogleCodeAsync(code, cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("GOOGLE_OAUTH_CODE_INVALID");
        }

        var userInfo = await GetGoogleUserInfoAsync(tokenResponse.AccessToken, cancellationToken);
        if (string.IsNullOrWhiteSpace(userInfo.Email))
        {
            throw new InvalidOperationException("GOOGLE_OAUTH_CODE_INVALID");
        }

        if (!userInfo.EmailVerified)
        {
            throw new InvalidOperationException("GOOGLE_EMAIL_NOT_VERIFIED");
        }

        var normalizedEmail = userInfo.Email.Trim().ToLowerInvariant();
        var existingUser = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (existingUser)
        {
            throw new InvalidOperationException("EMAIL_ALREADY_REGISTERED");
        }

        var nowUtc = DateTime.UtcNow;
        var existingFlow = await dbContext.RegistrationFlows
            .FirstOrDefaultAsync(
                x => x.Email == normalizedEmail,
                cancellationToken);

        if (existingFlow is null)
        {
            existingFlow = new RegistrationFlow
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                Status = PendingProfileStatus,
                OtpCode = "000000",
                OtpHash = HashOtp("000000"),
                OtpExpiresAtUtc = nowUtc,
                OtpAttempts = 0,
                VerifiedAtUtc = nowUtc,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };

            dbContext.RegistrationFlows.Add(existingFlow);
        }
        else if (existingFlow.Status == ActiveStatus)
        {
            throw new InvalidOperationException("EMAIL_ALREADY_REGISTERED");
        }
        else
        {
            existingFlow.Status = PendingProfileStatus;
            existingFlow.OtpCode = "000000";
            existingFlow.OtpHash = HashOtp("000000");
            existingFlow.OtpExpiresAtUtc = nowUtc;
            existingFlow.OtpAttempts = 0;
            existingFlow.OtpBlockedUntilUtc = null;
            existingFlow.VerifiedAtUtc = nowUtc;
            existingFlow.UpdatedAtUtc = nowUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GoogleRegisterCallbackResponse(
            existingFlow.Id,
            existingFlow.Status,
            new GooglePrefillResponse(
                normalizedEmail,
                userInfo.EmailVerified,
                userInfo.Name,
                userInfo.GivenName,
                userInfo.FamilyName,
                userInfo.Picture,
                userInfo.Locale));
    }

    private static void ResetEmailVerificationFlow(RegistrationFlow flow, DateTime nowUtc)
    {
        var otpCode = CreateOtp();
        flow.Status = PendingEmailVerificationStatus;
        flow.OtpCode = otpCode;
        flow.OtpHash = HashOtp(otpCode);
        flow.OtpExpiresAtUtc = nowUtc.AddMinutes(RegistrationFlowDefaults.OtpTtlMinutes);
        flow.OtpAttempts = 0;
        flow.OtpBlockedUntilUtc = null;
        flow.VerifiedAtUtc = null;
        flow.UpdatedAtUtc = nowUtc;
    }

    private async Task<AuthSecurityPolicy> GetOrCreateSecurityPolicyAsync(CancellationToken cancellationToken)
    {
        var policy = await dbContext.AuthSecurityPolicies.FirstOrDefaultAsync(cancellationToken);
        if (policy is not null)
        {
            return policy;
        }

        policy = new AuthSecurityPolicy
        {
            Id = Guid.NewGuid(),
            PasswordResetTtlMinutes = authOptions.PasswordResetTtlMinutes,
            RevokeSessionsOnPasswordReset = authOptions.RevokeSessionsOnPasswordReset,
            UpdatedAtUtc = DateTime.UtcNow
        };

        dbContext.AuthSecurityPolicies.Add(policy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return policy;
    }

    private static AuditLog CreateAuditLog(Guid? userId, string actionCode, string? ipAddress, object? metadata)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Area = "auth",
            ActionCode = actionCode,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata),
            IpAddress = ipAddress,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static string CreateOtp()
    {
        return Random.Shared.Next(0, 999999).ToString("D6");
    }

    private static string HashOtp(string otp)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otp)));
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2$100000$SHA256${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

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

    private static string GenerateOpaqueToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private async Task<UserSummaryResponse> BuildUserSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.Profile)
            .FirstAsync(x => x.Id == userId, cancellationToken);

        var roles = await dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Active)
            .Join(dbContext.Roles, ura => ura.RoleId, role => role.Id, (ura, role) => role.Code)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var displayName = user.Profile is null
            ? user.Email
            : $"{user.Profile.FirstName} {user.Profile.LastName}".Trim();

        var tipoCuenta = roles.Any(x => x.StartsWith("admin", StringComparison.OrdinalIgnoreCase))
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

    private async Task<IReadOnlyList<ResourcePermissionResponse>> BuildPermissionsAsync(Guid userId, CancellationToken cancellationToken)
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
            .ToListAsync(cancellationToken);

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

    private void EnsureGoogleOAuthConfigured()
    {
        if (string.IsNullOrWhiteSpace(googleOAuthOptions.ClientId)
            || string.IsNullOrWhiteSpace(googleOAuthOptions.ClientSecret)
            || string.IsNullOrWhiteSpace(googleOAuthOptions.RedirectUri))
        {
            throw new InvalidOperationException("GOOGLE_OAUTH_NOT_CONFIGURED");
        }
    }

    private async Task<GoogleTokenResponse> ExchangeGoogleCodeAsync(string code, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();

        var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("client_id", googleOAuthOptions.ClientId!),
            new KeyValuePair<string, string>("client_secret", googleOAuthOptions.ClientSecret!),
            new KeyValuePair<string, string>("redirect_uri", googleOAuthOptions.RedirectUri!),
            new KeyValuePair<string, string>("grant_type", "authorization_code")
        ]);

        using var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", form, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("GOOGLE_OAUTH_CODE_INVALID");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken);
        return tokenResponse ?? new GoogleTokenResponse(null);
    }

    private async Task<GoogleUserInfoResponse> GetGoogleUserInfoAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("GOOGLE_OAUTH_CODE_INVALID");
        }

        var userInfo = await response.Content.ReadFromJsonAsync<GoogleUserInfoResponse>(cancellationToken: cancellationToken);
        return userInfo ?? new GoogleUserInfoResponse(null, false, null, null, null, null, null);
    }

    private sealed record GoogleTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record GoogleUserInfoResponse(
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("email_verified")] bool EmailVerified,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("given_name")] string? GivenName,
        [property: JsonPropertyName("family_name")] string? FamilyName,
        [property: JsonPropertyName("picture")] string? Picture,
        [property: JsonPropertyName("locale")] string? Locale);
}