using System.Net.Http.Json;
using System.Text.Json;
using LCDPC.Application.OAuth2;
using LCDPC.Infrastructure.Users.Auth;

namespace LCDPC.Infrastructure.OAuth2;

/// <summary>
/// Google OAuth 2.0 service acting as an upstream Identity Provider.
/// Handles authorization URL generation, code exchange for userinfo, and claim mapping.
/// </summary>
public sealed class GoogleOAuthService : IGoogleOAuthService
{
    private readonly GoogleOAuthOptions _options;
    private readonly HttpClient _httpClient;

    private const string GoogleAuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string GoogleTokenUrl = "https://oauth2.googleapis.com/token";
    private const string GoogleUserInfoUrl = "https://openidconnect.googleapis.com/v1/userinfo";

    public GoogleOAuthService(
        GoogleOAuthOptions options,
        HttpClient httpClient)
    {
        _options = options;
        _httpClient = httpClient;
    }

    public string GetAuthorizationUrl(string state, string nonce)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId ?? string.Empty,
            ["redirect_uri"] = _options.RedirectUri ?? string.Empty,
            ["response_type"] = "code",
            ["scope"] = _options.Scope,
            ["state"] = state,
            ["nonce"] = nonce,
            ["access_type"] = "offline",
            ["prompt"] = "consent"
        };

        var queryString = string.Join("&", parameters.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{GoogleAuthUrl}?{queryString}";
    }

    public async Task<GoogleUserInfo> ExchangeCodeForUserInfoAsync(string code, CancellationToken ct = default)
    {
        // Exchange code for access token
        var tokenRequest = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId ?? string.Empty,
            ["client_secret"] = _options.ClientSecret ?? string.Empty,
            ["redirect_uri"] = _options.RedirectUri ?? string.Empty,
            ["grant_type"] = "authorization_code"
        };

        var tokenResponse = await _httpClient.PostAsync(GoogleTokenUrl, new FormUrlEncodedContent(tokenRequest), ct);
        tokenResponse.EnsureSuccessStatusCode();

        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: ct);
        if (tokenJson is null || string.IsNullOrEmpty(tokenJson.AccessToken))
        {
            throw new InvalidOperationException("Failed to obtain Google access token.");
        }

        // Use access token to get user info
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenJson.AccessToken);

        try
        {
            var userInfo = await _httpClient.GetFromJsonAsync<GoogleUserInfoResponse>(GoogleUserInfoUrl, cancellationToken: ct);
            if (userInfo is null)
            {
                throw new InvalidOperationException("Failed to parse Google userinfo response.");
            }

            return new GoogleUserInfo(
                Email: userInfo.Email ?? string.Empty,
                EmailVerified: userInfo.EmailVerified,
                Name: userInfo.Name,
                GivenName: userInfo.GivenName,
                FamilyName: userInfo.FamilyName,
                Picture: userInfo.Picture,
                Locale: userInfo.Locale);
        }
        finally
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public GooglePrefill GetPrefillFromClaims(GoogleUserInfo userInfo)
    {
        return new GooglePrefill(
            Email: userInfo.Email,
            EmailVerified: userInfo.EmailVerified,
            Name: userInfo.Name,
            GivenName: userInfo.GivenName,
            FamilyName: userInfo.FamilyName,
            Picture: userInfo.Picture,
            Locale: userInfo.Locale);
    }

    private sealed record GoogleTokenResponse(string? AccessToken, string? TokenType, int? ExpiresIn, string? IdToken);

    private sealed record GoogleUserInfoResponse(
        string? Sub,
        string? Name,
        string? GivenName,
        string? FamilyName,
        string? Picture,
        string? Email,
        bool EmailVerified,
        string? Locale);
}
