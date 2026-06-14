namespace LCDPC.Application.OAuth2;

// ── Authorize ──

public sealed record OAuth2AuthorizeRequest(
    string ClientId,
    string RedirectUri,
    string ResponseType,
    string Scope,
    string State,
    string? CodeChallenge,
    string? CodeChallengeMethod);

public sealed record OAuth2AuthorizeResult(
    bool Success,
    string? RedirectUrl,
    string? ErrorCode,
    string? ErrorDescription);

// ── Token ──

public sealed record OAuth2TokenRequest(
    string GrantType,
    string? Code,
    string? RedirectUri,
    string? CodeVerifier,
    string? RefreshToken,
    string ClientId);

public sealed record OAuth2TokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string RefreshToken,
    string Scope);

// ── Introspection ──

public sealed record OAuth2IntrospectRequest(
    string Token,
    string? TokenTypeHint);

public sealed record OAuth2IntrospectResponse(
    bool Active,
    string? ClientId,
    string? Username,
    string? Scope,
    long? Exp,
    long? Iat,
    string? Sub);

// ── Revocation ──

public sealed record OAuth2RevokeRequest(
    string Token,
    string? TokenTypeHint);

public sealed record OAuth2RevokeResponse(bool Success);

// ── Google OAuth ──

public sealed record GoogleUserInfo(
    string Email,
    bool EmailVerified,
    string? Name,
    string? GivenName,
    string? FamilyName,
    string? Picture,
    string? Locale);

public sealed record GooglePrefill(
    string Email,
    bool EmailVerified,
    string? Name,
    string? GivenName,
    string? FamilyName,
    string? Picture,
    string? Locale);

// ── Token theft ──

public sealed record OAuth2TokenTheftDetectedEvent(
    Guid UserId,
    string ClientId,
    Guid FamilyId,
    DateTime DetectedAtUtc);
