using LCDPC.API.Controllers;
using LCDPC.Domain.Entities.Users;
using LCDPC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using LCDPC.API.Security;
using System.Reflection;

namespace LCDPC.Architecture.Tests;

public class AdminUsersControllerTests
{
    [Fact]
    public async Task List_ReturnsPaginatedUsersOrderedByNewestFirst()
    {
        await using var dbContext = BuildDbContext();
        await SeedUserAsync(
            dbContext,
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            "older@example.com",
            "Older User",
            UserStatus.Active,
            "cliente",
            new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc));
        await SeedUserAsync(
            dbContext,
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            "newer@example.com",
            "Newer Admin",
            UserStatus.Suspended,
            "admin_global",
            new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc));

        var controller = new AdminUsersController(dbContext);

        var result = await controller.List(
            q: null,
            estado: null,
            tipoCuenta: null,
            rol: null,
            page: 1,
            pageSize: 1,
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminUsersListResponse>(ok.Value);

        Assert.Equal(1, payload.Page);
        Assert.Equal(1, payload.PageSize);
        Assert.Equal(2, payload.Total);
        Assert.Single(payload.Items);
        Assert.Equal("newer@example.com", payload.Items[0].Email);
        Assert.Equal("administrador", payload.Items[0].TipoCuenta);
        Assert.Equal("suspendido", payload.Items[0].Estado);
    }

    [Fact]
    public async Task List_AppliesRoleAndSearchFilters()
    {
        await using var dbContext = BuildDbContext();
        await SeedUserAsync(
            dbContext,
            Guid.Parse("10000000-0000-0000-0000-000000000003"),
            "cliente@example.com",
            "Cliente Final",
            UserStatus.Active,
            "cliente",
            new DateTime(2026, 5, 3, 12, 0, 0, DateTimeKind.Utc));
        await SeedUserAsync(
            dbContext,
            Guid.Parse("10000000-0000-0000-0000-000000000004"),
            "admin@example.com",
            "Admin Global",
            UserStatus.Active,
            "admin_global",
            new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc));

        var controller = new AdminUsersController(dbContext);

        var result = await controller.List(
            q: "Admin",
            estado: null,
            tipoCuenta: null,
            rol: "admin_global",
            page: 1,
            pageSize: 20,
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminUsersListResponse>(ok.Value);

        Assert.Single(payload.Items);
        Assert.Equal("admin@example.com", payload.Items[0].Email);
        Assert.Equal(new[] { "admin_global" }, payload.Items[0].Roles);
    }

    [Fact]
    public async Task UpdateStatus_UpdatesUserStatus()
    {
        await using var dbContext = BuildDbContext();
        var userId = Guid.Parse("10000000-0000-0000-0000-000000000005");

        await SeedUserAsync(
            dbContext,
            userId,
            "status@example.com",
            "Status User",
            UserStatus.Active,
            "cliente",
            new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc));

        var controller = new AdminUsersController(dbContext);

        var result = await controller.UpdateStatus(
            userId,
            new UpdateAdminUserStatusRequest("desactivado"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminUserListItemResponse>(ok.Value);
        Assert.Equal("desactivado", payload.Estado);

        var updated = await dbContext.Users.AsNoTracking().FirstAsync(x => x.Id == userId);
        Assert.Equal(UserStatus.Deactivated, updated.Status);

        var auditLog = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.ActionCode == "admin.users.status_changed")
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstAsync();

        using var statusMetadata = JsonDocument.Parse(auditLog.MetadataJson!);
        var newStatus = statusMetadata.RootElement.GetProperty("newStatus").GetString();
        Assert.Equal("desactivado", newStatus);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsBadRequest_WhenStatusIsInvalid()
    {
        await using var dbContext = BuildDbContext();
        var userId = Guid.Parse("10000000-0000-0000-0000-000000000006");

        await SeedUserAsync(
            dbContext,
            userId,
            "invalid-status@example.com",
            "Invalid Status",
            UserStatus.Active,
            "cliente",
            new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc));

        var controller = new AdminUsersController(dbContext);

        var result = await controller.UpdateStatus(
            userId,
            new UpdateAdminUserStatusRequest("bloqueado"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsNotFound_WhenUserDoesNotExist()
    {
        await using var dbContext = BuildDbContext();
        var controller = new AdminUsersController(dbContext);

        var result = await controller.UpdateStatus(
            Guid.Parse("10000000-0000-0000-0000-000000000099"),
            new UpdateAdminUserStatusRequest("activo"),
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task AssignRole_ChangesActiveRoleAndReturnsUpdatedUser()
    {
        await using var dbContext = BuildDbContext();
        var userId = Guid.Parse("10000000-0000-0000-0000-000000000007");

        await SeedUserAsync(
            dbContext,
            userId,
            "role-switch@example.com",
            "Role Switch",
            UserStatus.Active,
            "cliente",
            new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc));

        var controller = new AdminUsersController(dbContext);

        var result = await controller.AssignRole(
            userId,
            new AssignAdminUserRoleRequest("admin_global", null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminUserListItemResponse>(ok.Value);

        Assert.Equal("administrador", payload.TipoCuenta);
        Assert.Equal(new[] { "admin_global" }, payload.Roles);

        var assignments = await dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();

        var activeAssignments = assignments.Where(x => x.Active).ToList();
        Assert.Single(activeAssignments);
        var activeRoleId = activeAssignments[0].RoleId;
        var activeRoleCode = await dbContext.Roles
            .AsNoTracking()
            .Where(x => x.Id == activeRoleId)
            .Select(x => x.Code)
            .FirstAsync();
        Assert.Equal("admin_global", activeRoleCode);

        var auditLog = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.ActionCode == "admin.users.role_changed")
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstAsync();

        using var roleMetadata = JsonDocument.Parse(auditLog.MetadataJson!);
        var newRole = roleMetadata.RootElement.GetProperty("newRole").GetString();
        Assert.Equal("admin_global", newRole);
    }

    [Fact]
    public async Task AssignRole_ReturnsConflict_WhenRoleScopeIsDeferred()
    {
        await using var dbContext = BuildDbContext();
        var userId = Guid.Parse("10000000-0000-0000-0000-000000000008");

        await SeedUserAsync(
            dbContext,
            userId,
            "role-deferred@example.com",
            "Role Deferred",
            UserStatus.Active,
            "cliente",
            new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc));

        var controller = new AdminUsersController(dbContext);

        var result = await controller.AssignRole(
            userId,
            new AssignAdminUserRoleRequest("admin_sede", Array.Empty<Guid>()),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task AssignRole_ReturnsBadRequest_WhenRoleDoesNotExist()
    {
        await using var dbContext = BuildDbContext();
        var userId = Guid.Parse("10000000-0000-0000-0000-000000000009");

        await SeedUserAsync(
            dbContext,
            userId,
            "role-invalid@example.com",
            "Role Invalid",
            UserStatus.Active,
            "cliente",
            new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc));

        var controller = new AdminUsersController(dbContext);

        var result = await controller.AssignRole(
            userId,
            new AssignAdminUserRoleRequest("not_a_role", null),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task AssignRole_ReturnsNotFound_WhenUserDoesNotExist()
    {
        await using var dbContext = BuildDbContext();
        var controller = new AdminUsersController(dbContext);

        var result = await controller.AssignRole(
            Guid.Parse("10000000-0000-0000-0000-000000000010"),
            new AssignAdminUserRoleRequest("admin_global", null),
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task ForceResetAdmin_ReturnsAccepted_AndCreatesResetTokenAndAudit()
    {
        await using var dbContext = BuildDbContext();
        var userId = Guid.Parse("10000000-0000-0000-0000-000000000011");

        await SeedUserAsync(
            dbContext,
            userId,
            "force-reset@example.com",
            "Force Reset",
            UserStatus.Active,
            "admin_global",
            new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        dbContext.UserSessions.Add(new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccessTokenHash = "access-hash",
            RefreshTokenHash = "refresh-hash",
            AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = new AdminUsersController(dbContext);

        var result = await controller.ForceResetAdmin(
            userId,
            new ForceResetAdminUserRequest("security-policy", true),
            CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var payload = Assert.IsType<ForceResetAdminUserResponse>(accepted.Value);
        Assert.Equal("admin_reset_triggered", payload.Status);

        var activeResetToken = await dbContext.PasswordResetTokens
            .AsNoTracking()
            .SingleAsync(x => x.UserId == userId && x.RevokedAtUtc == null && x.UsedAtUtc == null);
        Assert.True(activeResetToken.ExpiresAtUtc > DateTime.UtcNow);

        var session = await dbContext.UserSessions.AsNoTracking().SingleAsync(x => x.UserId == userId);
        Assert.NotNull(session.RevokedAtUtc);

        var auditLog = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.ActionCode == "admin.users.force_reset_triggered")
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstAsync();
        Assert.Equal("admin.users", auditLog.Area);

        using var metadata = JsonDocument.Parse(auditLog.MetadataJson!);
        var reason = metadata.RootElement.GetProperty("reason").GetString();
        Assert.Equal("security-policy", reason);
    }

    [Fact]
    public async Task ForceResetAdmin_ReturnsConflict_WhenTargetIsNotAdmin()
    {
        await using var dbContext = BuildDbContext();
        var userId = Guid.Parse("10000000-0000-0000-0000-000000000012");

        await SeedUserAsync(
            dbContext,
            userId,
            "cliente-force@example.com",
            "Cliente Force",
            UserStatus.Active,
            "cliente",
            new DateTime(2026, 5, 11, 12, 0, 0, DateTimeKind.Utc));

        var controller = new AdminUsersController(dbContext);

        var result = await controller.ForceResetAdmin(
            userId,
            new ForceResetAdminUserRequest("security-policy", false),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task ForceResetAdmin_ReturnsNotFound_WhenUserDoesNotExist()
    {
        await using var dbContext = BuildDbContext();
        var controller = new AdminUsersController(dbContext);

        var result = await controller.ForceResetAdmin(
            Guid.Parse("10000000-0000-0000-0000-000000000013"),
            new ForceResetAdminUserRequest("security-policy", false),
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public void ForceResetAdmin_Endpoint_RequiresAdminGlobalRole()
    {
        var classRequireRoles = typeof(AdminUsersController)
            .GetCustomAttributes(typeof(RequireRolesAttribute), true)
            .OfType<RequireRolesAttribute>()
            .ToList();

        Assert.NotEmpty(classRequireRoles);
        Assert.Contains(classRequireRoles.SelectMany(x => x.Roles), role =>
            string.Equals(role, "admin_global", StringComparison.OrdinalIgnoreCase));

        var method = typeof(AdminUsersController).GetMethod(
            nameof(AdminUsersController.ForceResetAdmin),
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
    }

    [Fact]
    public async Task AdminMutations_WriteMandatoryAuditEntries()
    {
        await using var dbContext = BuildDbContext();
        var userId = Guid.Parse("10000000-0000-0000-0000-000000000014");

        await SeedUserAsync(
            dbContext,
            userId,
            "audit-admin@example.com",
            "Audit Admin",
            UserStatus.Active,
            "cliente",
            new DateTime(2026, 5, 12, 12, 0, 0, DateTimeKind.Utc));

        dbContext.UserSessions.Add(new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccessTokenHash = "access-hash",
            RefreshTokenHash = "refresh-hash",
            AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = new AdminUsersController(dbContext);

        var updateStatusResult = await controller.UpdateStatus(
            userId,
            new UpdateAdminUserStatusRequest("suspendido"),
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(updateStatusResult);

        var assignRoleResult = await controller.AssignRole(
            userId,
            new AssignAdminUserRoleRequest("admin_global", null),
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(assignRoleResult);

        var forceResetResult = await controller.ForceResetAdmin(
            userId,
            new ForceResetAdminUserRequest("qa-mandatory-audit", false),
            CancellationToken.None);
        Assert.IsType<AcceptedResult>(forceResetResult);

        var adminAuditLogs = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.Area == "admin.users")
            .ToListAsync();

        Assert.Equal(3, adminAuditLogs.Count);

        var actionCodes = adminAuditLogs.Select(x => x.ActionCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("admin.users.status_changed", actionCodes);
        Assert.Contains("admin.users.role_changed", actionCodes);
        Assert.Contains("admin.users.force_reset_triggered", actionCodes);

        foreach (var auditLog in adminAuditLogs)
        {
            using var metadata = JsonDocument.Parse(auditLog.MetadataJson!);
            var targetUserId = metadata.RootElement.GetProperty("targetUserId").GetGuid();
            Assert.Equal(userId, targetUserId);
        }
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

    private static async Task SeedUserAsync(
        AppDbContext dbContext,
        Guid userId,
        string email,
        string fullName,
        UserStatus status,
        string roleCode,
        DateTime createdAtUtc)
    {
        var role = await dbContext.Roles.SingleAsync(x => x.Code == roleCode);
        var nameParts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.FirstOrDefault() ?? fullName;
        var lastName = nameParts.Skip(1).FirstOrDefault() ?? "User";

        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = email,
            PasswordHash = "PBKDF2$100000$SHA256$U0FMVA==$SEFTSA==",
            OnboardingStatus = "active",
            Status = status,
            CreatedAtUtc = createdAtUtc,
            Profile = new Profile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FirstName = firstName,
                LastName = lastName,
                IdentityDocument = $"V-{Random.Shared.Next(1000000, 9999999)}",
                WhatsAppPhone = "+584120000000",
                FullAddress = "Direccion de prueba",
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = createdAtUtc
            },
            RoleAssignments =
            [
                new UserRoleAssignment
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    Active = true,
                    CreatedAtUtc = createdAtUtc
                }
            ]
        });

        await dbContext.SaveChangesAsync();
    }
}