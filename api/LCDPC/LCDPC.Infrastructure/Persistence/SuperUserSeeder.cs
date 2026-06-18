using LCDPC.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace LCDPC.Infrastructure.Persistence;

public static class SuperUserSeeder
{
    public sealed record SuperUserSeedOptions(
        string Alias,
        string Email,
        string FirstName,
        string LastName,
        string IdentityDocument,
        string WhatsAppPhone,
        string FullAddress)
    {
        public static SuperUserSeedOptions Default { get; } = new(
            Alias: "superperro",
            Email: "nemoxgil@gmail.com",
            FirstName: "Jose Guillermo",
            LastName: "Gil Valderrama",
            IdentityDocument: "V24276018",
            WhatsAppPhone: "0000000000",
            FullAddress: "Usuario administrador inicial");
    }

    public static async Task SeedAsync(
        AppDbContext dbContext,
        string superUserPassword,
        SuperUserSeedOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var seed = options ?? SuperUserSeedOptions.Default;
        var normalizedEmail = seed.Email.Trim().ToLowerInvariant();

        var existingUser = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            return;
        }

        var adminGlobalRole = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(role => role.Code == "admin_global", cancellationToken);

        if (adminGlobalRole is null)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = normalizedEmail,
            PasswordHash = HashPassword(superUserPassword),
            EmailVerifiedAtUtc = nowUtc,
            OnboardingStatus = "active",
            Status = UserStatus.Active,
            CreatedAtUtc = nowUtc
        };

        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FirstName = seed.FirstName,
            LastName = seed.LastName,
            IdentityDocument = seed.IdentityDocument,
            Rif = null,
            WhatsAppPhone = seed.WhatsAppPhone,
            FullAddress = seed.FullAddress,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        var roleAssignment = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = adminGlobalRole.Id,
            Active = true,
            SedeIds = Array.Empty<Guid>(),
            CreatedAtUtc = nowUtc
        };

        dbContext.Users.Add(user);
        dbContext.Profiles.Add(profile);
        dbContext.UserRoleAssignments.Add(roleAssignment);

        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Area = "auth",
            ActionCode = "auth.superuser.seeded",
            MetadataJson = $"{{\"alias\":\"{seed.Alias}\",\"email\":\"{normalizedEmail}\"}}",
            CreatedAtUtc = nowUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2$100000$SHA256${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
}
