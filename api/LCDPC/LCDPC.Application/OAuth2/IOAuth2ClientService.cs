using LCDPC.Domain.Entities.OAuth2;

namespace LCDPC.Application.OAuth2;

public interface IOAuth2ClientService
{
    /// <summary>
    /// Retrieves an OAuth2 client by its client_id.
    /// </summary>
    Task<OAuth2Client?> GetClientAsync(string clientId, CancellationToken ct = default);

    /// <summary>
    /// Validates that the given redirect_uri is registered for the client.
    /// </summary>
    bool ValidateRedirectUri(OAuth2Client client, string redirectUri);

    /// <summary>
    /// Validates that all requested scopes are within the client's allowed scopes.
    /// Scopes are space-separated.
    /// </summary>
    bool ValidateScopes(OAuth2Client client, string requestedScopes);
}
