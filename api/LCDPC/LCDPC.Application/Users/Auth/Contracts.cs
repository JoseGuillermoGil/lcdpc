namespace LCDPC.Application.Users.Auth;

public sealed record StartRegistrationRequest(string Email);

public sealed record VerifyEmailRegistrationRequest(Guid FlowId, string Otp);

public sealed record CompleteProfileRegistrationRequest(
    Guid FlowId,
    string Nombres,
    string Apellidos,
    string DocumentoIdentidad,
    string? Rif,
    string TelefonoWhatsApp,
    string DireccionCompleta,
    string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record GoogleRegisterStartResponse(string AuthorizationUrl, string State, int StateExpiresInSeconds);

public sealed record GooglePrefillResponse(
    string Email,
    bool EmailVerified,
    string? Name,
    string? GivenName,
    string? FamilyName,
    string? Picture,
    string? Locale);

public sealed record GoogleRegisterCallbackResponse(Guid FlowId, string Status, GooglePrefillResponse Prefill);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ForgotPasswordResponse(string Status, string Message, string? ResetToken);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record ResetPasswordResponse(string Status, bool SessionsRevoked);

public sealed record AuthSecurityPolicyResponse(int PasswordResetTtlMinutes, bool RevokeSessionsOnPasswordReset);

public sealed record UpdateAuthSecurityPolicyRequest(int PasswordResetTtlMinutes, bool RevokeSessionsOnPasswordReset);

public sealed record OtpPolicyResponse(int TtlMinutes, int MaxAttempts, int CooldownMinutes);

public sealed record StartRegistrationResponse(Guid FlowId, string Status, OtpPolicyResponse OtpPolicy);

public sealed record VerifyEmailRegistrationResponse(Guid FlowId, string Status);

public sealed record CompleteProfileRegistrationResponse(Guid UsuarioId, string Estado, string TipoCuenta);

public sealed record TokenPairResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds);

public sealed record UserSummaryResponse(
    Guid UsuarioId,
    string Email,
    string DisplayName,
    string Estado,
    string TipoCuenta,
    string OnboardingStatus,
    DateTime? EmailVerifiedAtUtc,
    IReadOnlyList<string> Roles);

public sealed record ResourcePermissionResponse(
    string ResourceCode,
    bool CanView,
    bool CanWrite,
    bool CanUpdate,
    bool CanDelete,
    bool CanAll);

public sealed record LoginResponse(
    TokenPairResponse TokenPair,
    UserSummaryResponse UserSummary,
    IReadOnlyList<ResourcePermissionResponse> Permissions);

public sealed record MeResponse(
    bool Authenticated,
    UserSummaryResponse? UserSummary,
    IReadOnlyList<ResourcePermissionResponse> Permissions,
    int? ExpiresInSeconds,
    IReadOnlyList<string>? Scopes = null);

public sealed record RefreshSessionResponse(
    TokenPairResponse TokenPair,
    UserSummaryResponse UserSummary,
    IReadOnlyList<ResourcePermissionResponse> Permissions);

public interface IRegistrationFlowService
{
    Task<StartRegistrationResponse> StartAsync(string email, CancellationToken cancellationToken = default);
    Task<VerifyEmailRegistrationResponse> VerifyEmailAsync(Guid flowId, string otp, CancellationToken cancellationToken = default);
    Task<CompleteProfileRegistrationResponse> CompleteProfileAsync(CompleteProfileRegistrationRequest request, CancellationToken cancellationToken = default);
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<MeResponse> MeAsync(string? accessToken, CancellationToken cancellationToken = default);
    Task<RefreshSessionResponse> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default);
    Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default);
    Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default);
    Task<AuthSecurityPolicyResponse> GetSecurityPolicyAsync(CancellationToken cancellationToken = default);
    Task<AuthSecurityPolicyResponse> UpdateSecurityPolicyAsync(UpdateAuthSecurityPolicyRequest request, CancellationToken cancellationToken = default);
    Task<GoogleRegisterStartResponse> StartGoogleRegistrationAsync(string state, CancellationToken cancellationToken = default);
    Task<GoogleRegisterCallbackResponse> CompleteGoogleRegistrationAsync(string code, CancellationToken cancellationToken = default);
}