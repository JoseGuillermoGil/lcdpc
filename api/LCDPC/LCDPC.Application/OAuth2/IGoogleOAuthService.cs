namespace LCDPC.Application.OAuth2;

public interface IGoogleOAuthService
{
    /// <summary>
    /// Builds the Google OAuth 2.0 authorization URL for redirecting the user.
    /// </summary>
    string GetAuthorizationUrl(string state, string nonce);

    /// <summary>
    /// Exchanges the Google authorization code for user info via Google's token and userinfo endpoints.
    /// </summary>
    Task<GoogleUserInfo> ExchangeCodeForUserInfoAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Maps Google user info into a prefill response for registration flow.
    /// </summary>
    GooglePrefill GetPrefillFromClaims(GoogleUserInfo userInfo);
}
