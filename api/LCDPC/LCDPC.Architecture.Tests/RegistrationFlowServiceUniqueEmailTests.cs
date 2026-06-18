using LCDPC.Application.Users.Auth;
using LCDPC.Domain.Entities.Users;
using LCDPC.Infrastructure.OAuth2;
using LCDPC.Infrastructure.Persistence;
using LCDPC.Infrastructure.Users.Auth;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.Architecture.Tests;

public class RegistrationFlowServiceUniqueEmailTests
{
    [Fact]
    public async Task StartAsync_ReusesExistingFlow_WhenEmailStartsTwice()
    {
        await using var dbContext = BuildDbContext();
        var service = BuildService(dbContext);

        var first = await service.StartAsync("cliente@example.com", CancellationToken.None);
        var second = await service.StartAsync("cliente@example.com", CancellationToken.None);

        Assert.Equal(first.FlowId, second.FlowId);
        Assert.Equal("pending_email_verification", second.Status);
        Assert.Equal(2, first.OtpPolicy.TtlMinutes);
        Assert.Equal(1, await dbContext.RegistrationFlows.CountAsync());
    }

    [Fact]
    public async Task StartAsync_NormalizesEmail_WhenLookingUpExistingFlow()
    {
        await using var dbContext = BuildDbContext();
        var service = BuildService(dbContext);

        var first = await service.StartAsync(" Cliente@Example.COM ", CancellationToken.None);
        var second = await service.StartAsync("cliente@example.com", CancellationToken.None);
        var flow = await dbContext.RegistrationFlows.SingleAsync();

        Assert.Equal(first.FlowId, second.FlowId);
        Assert.Equal("cliente@example.com", flow.Email);
        Assert.Equal(1, await dbContext.RegistrationFlows.CountAsync());
    }

    [Fact]
    public async Task StartAsync_RefreshesOtpState_WhenPendingEmailVerificationFlowExists()
    {
        await using var dbContext = BuildDbContext();
        var service = BuildService(dbContext);
        var flowId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddMinutes(-30);

        dbContext.RegistrationFlows.Add(new RegistrationFlow
        {
            Id = flowId,
            Email = "refresh@example.com",
            Status = "pending_email_verification",
            OtpCode = "111111",
            OtpHash = "old-hash",
            OtpExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
            OtpAttempts = 3,
            OtpBlockedUntilUtc = DateTime.UtcNow.AddMinutes(-5),
            VerifiedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-20)
        });
        await dbContext.SaveChangesAsync();

        var response = await service.StartAsync("refresh@example.com", CancellationToken.None);
        var flow = await dbContext.RegistrationFlows.SingleAsync();

        Assert.Equal(flowId, response.FlowId);
        Assert.Equal("pending_email_verification", flow.Status);
        Assert.Equal(0, flow.OtpAttempts);
        Assert.Null(flow.OtpBlockedUntilUtc);
        Assert.Null(flow.VerifiedAtUtc);
        Assert.Equal(createdAt, flow.CreatedAtUtc);
        Assert.True(flow.OtpExpiresAtUtc > DateTime.UtcNow);
        Assert.NotEqual("old-hash", flow.OtpHash);
    }

    [Fact]
    public async Task StartAsync_ResetsPendingProfileFlow_WhenRestartingRegistration()
    {
        await using var dbContext = BuildDbContext();
        var service = BuildService(dbContext);
        var flowId = Guid.NewGuid();
        var previousOtpExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        dbContext.RegistrationFlows.Add(new RegistrationFlow
        {
            Id = flowId,
            Email = "profile@example.com",
            Status = "pending_profile",
            OtpCode = "222222",
            OtpHash = "verified-hash",
            OtpExpiresAtUtc = previousOtpExpiresAt,
            OtpAttempts = 1,
            VerifiedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        });
        await dbContext.SaveChangesAsync();

        var response = await service.StartAsync("profile@example.com", CancellationToken.None);
        var flow = await dbContext.RegistrationFlows.SingleAsync();

        Assert.Equal(flowId, response.FlowId);
        Assert.Equal("pending_email_verification", response.Status);
        Assert.Equal("pending_email_verification", flow.Status);
        Assert.NotEqual("222222", flow.OtpCode);
        Assert.NotEqual("verified-hash", flow.OtpHash);
        Assert.True(flow.OtpExpiresAtUtc > DateTime.UtcNow);
        Assert.NotEqual(previousOtpExpiresAt, flow.OtpExpiresAtUtc);
        Assert.Equal(0, flow.OtpAttempts);
        Assert.Null(flow.VerifiedAtUtc);
    }

    [Fact]
    public async Task StartAsync_RejectsRestart_WhenOtpCooldownIsActive()
    {
        await using var dbContext = BuildDbContext();
        var service = BuildService(dbContext);

        dbContext.RegistrationFlows.Add(new RegistrationFlow
        {
            Id = Guid.NewGuid(),
            Email = "blocked@example.com",
            Status = "otp_attempts_exceeded",
            OtpCode = "333333",
            OtpHash = "blocked-hash",
            OtpExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            OtpAttempts = 5,
            OtpBlockedUntilUtc = DateTime.UtcNow.AddMinutes(10),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartAsync("blocked@example.com", CancellationToken.None));

        Assert.Equal("OTP_COOLDOWN_ACTIVE", exception.Message);
        Assert.Equal(1, await dbContext.RegistrationFlows.CountAsync());
    }

    [Fact]
    public async Task VerifyEmailAsync_RotatesOtp_WhenExpiredAndOlderThanTtl()
    {
        await using var dbContext = BuildDbContext();
        var service = BuildService(dbContext);
        var flowId = Guid.NewGuid();

        dbContext.RegistrationFlows.Add(new RegistrationFlow
        {
            Id = flowId,
            Email = "expired-verify@example.com",
            Status = "pending_email_verification",
            OtpCode = "123456",
            OtpHash = "old-hash",
            OtpExpiresAtUtc = DateTime.UtcNow.AddSeconds(-10),
            OtpAttempts = 2,
            OtpBlockedUntilUtc = DateTime.UtcNow.AddMinutes(-1),
            VerifiedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-3)
        });
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.VerifyEmailAsync(flowId, "123456", CancellationToken.None));

        Assert.Equal("OTP_EXPIRED", exception.Message);

        var flow = await dbContext.RegistrationFlows.SingleAsync();
        Assert.Equal("pending_email_verification", flow.Status);
        Assert.NotEqual("123456", flow.OtpCode);
        Assert.NotEqual("old-hash", flow.OtpHash);
        Assert.True(flow.OtpExpiresAtUtc > DateTime.UtcNow);
        Assert.Equal(0, flow.OtpAttempts);
        Assert.Null(flow.OtpBlockedUntilUtc);
    }

    [Fact]
    public async Task VerifyEmailAsync_RotatesOtp_WhenFlowIsPendingProfile()
    {
        await using var dbContext = BuildDbContext();
        var service = BuildService(dbContext);
        var flowId = Guid.NewGuid();

        dbContext.RegistrationFlows.Add(new RegistrationFlow
        {
            Id = flowId,
            Email = "pending-profile-verify@example.com",
            Status = "pending_profile",
            OtpCode = "654321",
            OtpHash = "old-pending-profile-hash",
            OtpExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            OtpAttempts = 1,
            OtpBlockedUntilUtc = null,
            VerifiedAtUtc = DateTime.UtcNow.AddMinutes(-2),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-2)
        });
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.VerifyEmailAsync(flowId, "654321", CancellationToken.None));

        Assert.Equal("OTP_EXPIRED", exception.Message);

        var flow = await dbContext.RegistrationFlows.SingleAsync();
        Assert.Equal("pending_email_verification", flow.Status);
        Assert.NotEqual("654321", flow.OtpCode);
        Assert.NotEqual("old-pending-profile-hash", flow.OtpHash);
        Assert.True(flow.OtpExpiresAtUtc > DateTime.UtcNow);
        Assert.Equal(0, flow.OtpAttempts);
        Assert.Null(flow.VerifiedAtUtc);
    }

    [Fact]
    public async Task VerifyEmailAsync_RotatesOtp_WhenExpiredEvenIfUpdatedRecently()
    {
        await using var dbContext = BuildDbContext();
        var service = BuildService(dbContext);
        var flowId = Guid.NewGuid();

        dbContext.RegistrationFlows.Add(new RegistrationFlow
        {
            Id = flowId,
            Email = "expired-recent-update@example.com",
            Status = "pending_email_verification",
            OtpCode = "777777",
            OtpHash = "recent-update-hash",
            OtpExpiresAtUtc = DateTime.UtcNow.AddSeconds(-5),
            OtpAttempts = 1,
            OtpBlockedUntilUtc = null,
            VerifiedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAtUtc = DateTime.UtcNow.AddSeconds(-15)
        });
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.VerifyEmailAsync(flowId, "777777", CancellationToken.None));

        Assert.Equal("OTP_EXPIRED", exception.Message);

        var flow = await dbContext.RegistrationFlows.SingleAsync();
        Assert.Equal("pending_email_verification", flow.Status);
        Assert.NotEqual("777777", flow.OtpCode);
        Assert.NotEqual("recent-update-hash", flow.OtpHash);
        Assert.True(flow.OtpExpiresAtUtc > DateTime.UtcNow);
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
}
