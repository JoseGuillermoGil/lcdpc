using LCDPC.Application.Users.Auth;
using LCDPC.Infrastructure.OAuth2;
using LCDPC.Domain.Entities.Users;
using LCDPC.Infrastructure.Persistence;
using LCDPC.Infrastructure.Users.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LCDPC.Architecture.Tests;

public class RegistrationFlowServiceSecurityPolicyTests
{
    [Fact]
    public async Task UpdatePolicy_AppliesForgotPasswordTtlEffectively()
    {
        await using var dbContext = BuildDbContext();
        var service = BuildService(dbContext);

        var userId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        const string email = "policy-ttl@example.com";
        await SeedUserAsync(dbContext, userId, email);

        var updatedPolicy = await service.UpdateSecurityPolicyAsync(
            new UpdateAuthSecurityPolicyRequest(45, false),
            CancellationToken.None);

        Assert.Equal(45, updatedPolicy.PasswordResetTtlMinutes);
        Assert.False(updatedPolicy.RevokeSessionsOnPasswordReset);

        var forgotResponse = await service.ForgotPasswordAsync(
            new ForgotPasswordRequest(email),
            ipAddress: null,
            cancellationToken: CancellationToken.None);

        Assert.Equal("accepted", forgotResponse.Status);
        Assert.False(string.IsNullOrWhiteSpace(forgotResponse.ResetToken));

        var token = await dbContext.PasswordResetTokens
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.UsedAtUtc == null)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstAsync();

        var ttlMinutes = (token.ExpiresAtUtc - token.CreatedAtUtc).TotalMinutes;
        Assert.InRange(ttlMinutes, 44.5, 45.5);

        var policyAudit = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.ActionCode == "auth.security.policy_updated")
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

        Assert.NotNull(policyAudit);
    }

    [Fact]
    public async Task ResetPassword_RevokesSessions_WhenPolicyRequiresRevocation()
    {
        await using var dbContext = BuildDbContext();
        var service = BuildService(dbContext);

        var userId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        const string email = "policy-revoke-on@example.com";
        await SeedUserAsync(dbContext, userId, email);
        await SeedActiveSessionAsync(dbContext, userId);

        await service.UpdateSecurityPolicyAsync(new UpdateAuthSecurityPolicyRequest(30, true), CancellationToken.None);
        var forgotResponse = await service.ForgotPasswordAsync(new ForgotPasswordRequest(email), null, CancellationToken.None);

        var resetResponse = await service.ResetPasswordAsync(
            new ResetPasswordRequest(forgotResponse.ResetToken!, "new-password"),
            ipAddress: null,
            cancellationToken: CancellationToken.None);

        Assert.True(resetResponse.SessionsRevoked);

        var activeSession = await dbContext.UserSessions
            .AsNoTracking()
            .SingleAsync(x => x.UserId == userId);

        Assert.NotNull(activeSession.RevokedAtUtc);
    }

    [Fact]
    public async Task ResetPassword_DoesNotRevokeSessions_WhenPolicyDisablesRevocation()
    {
        await using var dbContext = BuildDbContext();
        var service = BuildService(dbContext);

        var userId = Guid.Parse("20000000-0000-0000-0000-000000000003");
        const string email = "policy-revoke-off@example.com";
        await SeedUserAsync(dbContext, userId, email);
        await SeedActiveSessionAsync(dbContext, userId);

        await service.UpdateSecurityPolicyAsync(new UpdateAuthSecurityPolicyRequest(30, false), CancellationToken.None);
        var forgotResponse = await service.ForgotPasswordAsync(new ForgotPasswordRequest(email), null, CancellationToken.None);

        var resetResponse = await service.ResetPasswordAsync(
            new ResetPasswordRequest(forgotResponse.ResetToken!, "new-password"),
            ipAddress: null,
            cancellationToken: CancellationToken.None);

        Assert.False(resetResponse.SessionsRevoked);

        var activeSession = await dbContext.UserSessions
            .AsNoTracking()
            .SingleAsync(x => x.UserId == userId);

        Assert.Null(activeSession.RevokedAtUtc);
    }

    private static RegistrationFlowService BuildService(AppDbContext dbContext)
    {
        return new RegistrationFlowService(
            dbContext,
            new AuthSecurityOptions
            {
                PasswordResetTtlMinutes = 30,
                RevokeSessionsOnPasswordReset = true
            },
            new GoogleOAuthOptions());
    }

    private static AppDbContext BuildDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static async Task SeedUserAsync(AppDbContext dbContext, Guid userId, string email)
    {
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = email,
            PasswordHash = "PBKDF2$100000$SHA256$U0FMVA==$SEFTSA==",
            OnboardingStatus = "active",
            Status = UserStatus.Active,
            EmailVerifiedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedActiveSessionAsync(AppDbContext dbContext, Guid userId)
    {
        dbContext.UserSessions.Add(new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccessTokenHash = "access-hash",
            RefreshTokenHash = "refresh-hash",
            AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            CreatedAtUtc = DateTime.UtcNow,
            RevokedAtUtc = null
        });

        await dbContext.SaveChangesAsync();
    }
}
