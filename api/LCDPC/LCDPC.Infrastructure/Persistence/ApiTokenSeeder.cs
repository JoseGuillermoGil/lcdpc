using LCDPC.Domain.Entities.Users;
using LCDPC.Infrastructure.Users.Auth;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.Infrastructure.Persistence;

public static class ApiTokenSeeder
{
    public static async Task<string?> SeedAsync(AppDbContext dbContext)
    {
        var hasTokens = await dbContext.ApiTokens.AnyAsync();
        if (hasTokens)
        {
            return null;
        }

        var rawToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var tokenHash = TokenHashing.Hash(rawToken);

        var apiToken = ApiToken.Create(
            Guid.NewGuid(),
            "sync-initial",
            tokenHash);

        dbContext.ApiTokens.Add(apiToken);
        await dbContext.SaveChangesAsync();

        return rawToken;
    }
}
