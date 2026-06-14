using LCDPC.Application.OAuth2;
using LCDPC.Domain.Entities.OAuth2;
using LCDPC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.Infrastructure.OAuth2;

/// <summary>
/// Service for OAuth2 client operations: retrieval, redirect_uri validation, scope validation.
/// </summary>
public sealed class OAuth2ClientService(AppDbContext dbContext) : IOAuth2ClientService
{
    public async Task<OAuth2Client?> GetClientAsync(string clientId, CancellationToken ct = default)
    {
        return await dbContext.OAuth2Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClientId == clientId, ct);
    }

    public bool ValidateRedirectUri(OAuth2Client client, string redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return false;
        }

        return client.RedirectUris.Contains(redirectUri, StringComparer.Ordinal);
    }

    public bool ValidateScopes(OAuth2Client client, string requestedScopes)
    {
        if (string.IsNullOrWhiteSpace(requestedScopes))
        {
            return true; // No scopes requested is valid
        }

        var allowed = client.AllowedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var requested = requestedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return requested.All(r => allowed.Contains(r, StringComparer.Ordinal));
    }
}
