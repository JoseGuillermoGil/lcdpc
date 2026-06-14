using LCDPC.Domain.Entities.OAuth2;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.Infrastructure.Persistence;

public static class OAuth2ClientSeeder
{
    private const string DefaultClientId = "lcdpc-web";

    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.OAuth2Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClientId == DefaultClientId, cancellationToken);

        if (existing is not null)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;

        var client = new OAuth2Client
        {
            ClientId = DefaultClientId,
            ClientName = "LCDPC Web SPA",
            RedirectUris = ["http://localhost:4200", "http://localhost:4200/auth/callback"],
            GrantTypes = ["authorization_code", "refresh_token"],
            RequirePkce = true,
            AllowedScopes = "openid email profile admin admin:sedes admin:users",
            CreatedAtUtc = nowUtc
        };

        dbContext.OAuth2Clients.Add(client);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
