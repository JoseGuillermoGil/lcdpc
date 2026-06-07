using LCDPC.API.Security;
using LCDPC.Domain.Entities.Users;
using LCDPC.Infrastructure.Users.Auth;
using LCDPC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

namespace LCDPC.API.Controllers;

public sealed record AdminUserListItemResponse(
    Guid UsuarioId,
    string Email,
    string DisplayName,
    string Estado,
    string TipoCuenta,
    string OnboardingStatus,
    DateTime CreatedAtUtc,
    IReadOnlyList<string> Roles);

public sealed record AdminUsersListResponse(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<AdminUserListItemResponse> Items);

public sealed record UpdateAdminUserStatusRequest(string Estado);
public sealed record AssignAdminUserRoleRequest(string Rol, Guid[]? SedeIds);
public sealed record ForceResetAdminUserRequest(string? Reason, bool NotifyEmail = false);
public sealed record ForceResetAdminUserResponse(string Status);

[ApiController]
[Route("api/v1/admin/users")]
[RequireRoles("admin_global")]
public class AdminUsersController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AdminUsersListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] string? estado,
        [FromQuery] string? tipoCuenta,
        [FromQuery] string? rol,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var normalizedQuery = q?.Trim();
        var normalizedRole = rol?.Trim().ToLowerInvariant();

        var projectedUsers = await dbContext.Users
            .AsNoTracking()
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.OnboardingStatus,
                user.Status,
                user.CreatedAtUtc,
                DisplayName = user.Profile != null
                    ? ((user.Profile.FirstName ?? string.Empty) + " " + (user.Profile.LastName ?? string.Empty)).Trim()
                    : user.Email,
                Roles = user.RoleAssignments
                    .Where(assignment => assignment.Active)
                    .Select(assignment => assignment.Role.Code)
                    .Distinct()
                    .OrderBy(code => code)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var filteredUsers = projectedUsers
            .Where(user => string.IsNullOrWhiteSpace(normalizedQuery)
                || user.Email.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || user.DisplayName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Where(user => string.IsNullOrWhiteSpace(estado)
                || string.Equals(MapStatus(user.Status), estado.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(user => string.IsNullOrWhiteSpace(tipoCuenta)
                || string.Equals(MapTipoCuenta(user.Roles), tipoCuenta.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(user => string.IsNullOrWhiteSpace(normalizedRole)
                || user.Roles.Any(code => string.Equals(code, normalizedRole, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(user => user.CreatedAtUtc)
            .ThenBy(user => user.Email)
            .ToList();

        var total = filteredUsers.Count;
        var items = filteredUsers
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(user => new AdminUserListItemResponse(
                user.Id,
                user.Email,
                user.DisplayName,
                MapStatus(user.Status),
                MapTipoCuenta(user.Roles),
                user.OnboardingStatus,
                user.CreatedAtUtc,
                user.Roles))
            .ToList();

        return Ok(new AdminUsersListResponse(normalizedPage, normalizedPageSize, total, items));
    }

    [HttpPatch("{usuarioId:guid}/status")]
    [ProducesResponseType(typeof(AdminUserListItemResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(
        [FromRoute] Guid usuarioId,
        [FromBody] UpdateAdminUserStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseStatus(request.Estado, out var targetStatus))
        {
            return BadRequest(new
            {
                code = "INVALID_STATUS",
                message = "estado must be one of: activo, suspendido, desactivado"
            });
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == usuarioId, cancellationToken);

        if (user is null)
        {
            return NotFound(new
            {
                code = "USER_NOT_FOUND",
                message = "user not found"
            });
        }

        user.Status = targetStatus;

        var actorUserId = TryResolveActorUserId(ControllerContext?.HttpContext?.User ?? User);
        var remoteIpAddress = ControllerContext?.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        dbContext.AuditLogs.Add(CreateAdminAuditLog(
            actorUserId,
            remoteIpAddress,
            "admin.users.status_changed",
            new
            {
                targetUserId = usuarioId,
                newStatus = MapStatus(targetStatus)
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        var payload = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == usuarioId)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.OnboardingStatus,
                x.Status,
                x.CreatedAtUtc,
                DisplayName = x.Profile != null
                    ? ((x.Profile.FirstName ?? string.Empty) + " " + (x.Profile.LastName ?? string.Empty)).Trim()
                    : x.Email,
                Roles = x.RoleAssignments
                    .Where(assignment => assignment.Active)
                    .Select(assignment => assignment.Role.Code)
                    .Distinct()
                    .OrderBy(code => code)
                    .ToList()
            })
            .FirstAsync(cancellationToken);

        return Ok(new AdminUserListItemResponse(
            payload.Id,
            payload.Email,
            payload.DisplayName,
            MapStatus(payload.Status),
            MapTipoCuenta(payload.Roles),
            payload.OnboardingStatus,
            payload.CreatedAtUtc,
            payload.Roles));
    }

    [HttpPost("{usuarioId:guid}/roles")]
    [ProducesResponseType(typeof(AdminUserListItemResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignRole(
        [FromRoute] Guid usuarioId,
        [FromBody] AssignAdminUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRole = request.Rol?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedRole))
        {
            return BadRequest(new
            {
                code = "INVALID_ROLE",
                message = "rol is required"
            });
        }

        if (string.Equals(normalizedRole, "admin_sede", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new
            {
                code = "ROLE_SCOPE_DEFERRED",
                message = "admin_sede assignment is deferred until sede scope flow is implemented"
            });
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == usuarioId, cancellationToken);

        if (user is null)
        {
            return NotFound(new
            {
                code = "USER_NOT_FOUND",
                message = "user not found"
            });
        }

        var role = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == normalizedRole, cancellationToken);

        if (role is null)
        {
            return BadRequest(new
            {
                code = "INVALID_ROLE",
                message = "rol must be a valid role code"
            });
        }

        var activeAssignments = await dbContext.UserRoleAssignments
            .Where(x => x.UserId == usuarioId && x.Active)
            .ToListAsync(cancellationToken);

        var previousRoleIds = activeAssignments.Select(x => x.RoleId).Distinct().ToList();
        var previousRoleCodes = await dbContext.Roles
            .AsNoTracking()
            .Where(x => previousRoleIds.Contains(x.Id))
            .Select(x => x.Code)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        foreach (var assignment in activeAssignments)
        {
            assignment.Active = false;
        }

        var newAssignment = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = usuarioId,
            RoleId = role.Id,
            SedeIds = request.SedeIds ?? Array.Empty<Guid>(),
            Active = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.UserRoleAssignments.Add(newAssignment);

        var actorUserId = TryResolveActorUserId(ControllerContext?.HttpContext?.User ?? User);
        var remoteIpAddress = ControllerContext?.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        dbContext.AuditLogs.Add(CreateAdminAuditLog(
            actorUserId,
            remoteIpAddress,
            "admin.users.role_changed",
            new
            {
                targetUserId = usuarioId,
                previousRoles = previousRoleCodes,
                newRole = role.Code,
                sedeIds = newAssignment.SedeIds
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        var payload = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == usuarioId)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.OnboardingStatus,
                x.Status,
                x.CreatedAtUtc,
                DisplayName = x.Profile != null
                    ? ((x.Profile.FirstName ?? string.Empty) + " " + (x.Profile.LastName ?? string.Empty)).Trim()
                    : x.Email,
                Roles = x.RoleAssignments
                    .Where(assignment => assignment.Active)
                    .Select(assignment => assignment.Role.Code)
                    .Distinct()
                    .OrderBy(code => code)
                    .ToList()
            })
            .FirstAsync(cancellationToken);

        return Ok(new AdminUserListItemResponse(
            payload.Id,
            payload.Email,
            payload.DisplayName,
            MapStatus(payload.Status),
            MapTipoCuenta(payload.Roles),
            payload.OnboardingStatus,
            payload.CreatedAtUtc,
            payload.Roles));
    }

    [HttpPost("{usuarioId:guid}/force-reset")]
    [ProducesResponseType(typeof(ForceResetAdminUserResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForceResetAdmin(
        [FromRoute] Guid usuarioId,
        [FromBody] ForceResetAdminUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == usuarioId, cancellationToken);

        if (user is null)
        {
            return NotFound(new
            {
                code = "USER_NOT_FOUND",
                message = "user not found"
            });
        }

        var activeRoleCodes = await dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(x => x.UserId == usuarioId && x.Active)
            .Join(dbContext.Roles, assignment => assignment.RoleId, role => role.Id, (assignment, role) => role.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (!activeRoleCodes.Any(code => code.StartsWith("admin", StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new
            {
                code = "TARGET_NOT_ADMIN",
                message = "force reset is only available for admin users"
            });
        }

        var nowUtc = DateTime.UtcNow;
        var policy = await dbContext.AuthSecurityPolicies.FirstOrDefaultAsync(cancellationToken)
                     ?? new AuthSecurityPolicy
                     {
                         PasswordResetTtlMinutes = 30,
                         RevokeSessionsOnPasswordReset = true,
                         UpdatedAtUtc = nowUtc
                     };

        var activeResetTokens = await dbContext.PasswordResetTokens
            .Where(x => x.UserId == usuarioId && x.UsedAtUtc == null && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var activeResetToken in activeResetTokens)
        {
            activeResetToken.RevokedAtUtc = nowUtc;
        }

        var plainResetToken = GenerateOpaqueToken();
        dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = usuarioId,
            TokenHash = TokenHashing.Hash(plainResetToken),
            ExpiresAtUtc = nowUtc.AddMinutes(policy.PasswordResetTtlMinutes),
            CreatedAtUtc = nowUtc
        });

        var sessionsRevoked = 0;
        if (policy.RevokeSessionsOnPasswordReset)
        {
            var activeSessions = await dbContext.UserSessions
                .Where(x => x.UserId == usuarioId && x.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var activeSession in activeSessions)
            {
                activeSession.RevokedAtUtc = nowUtc;
            }

            sessionsRevoked = activeSessions.Count;
        }

        var actorUserId = TryResolveActorUserId(ControllerContext?.HttpContext?.User ?? User);
        var remoteIpAddress = ControllerContext?.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        dbContext.AuditLogs.Add(CreateAdminAuditLog(
            actorUserId,
            remoteIpAddress,
            "admin.users.force_reset_triggered",
            new
            {
                targetUserId = usuarioId,
                reason = request.Reason,
                request.NotifyEmail,
                resetTtlMinutes = policy.PasswordResetTtlMinutes,
                sessionsRevoked
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        return Accepted(new ForceResetAdminUserResponse("admin_reset_triggered"));
    }

    private static string MapStatus(UserStatus status)
    {
        return status switch
        {
            UserStatus.Active => "activo",
            UserStatus.Suspended => "suspendido",
            UserStatus.Deactivated => "desactivado",
            _ => "activo"
        };
    }

    private static string MapTipoCuenta(IReadOnlyCollection<string> roles)
    {
        return roles.Any(roleCode => roleCode.StartsWith("admin", StringComparison.OrdinalIgnoreCase))
            ? "administrador"
            : "cliente";
    }

    private static bool TryParseStatus(string? estado, out UserStatus status)
    {
        if (string.Equals(estado?.Trim(), "activo", StringComparison.OrdinalIgnoreCase))
        {
            status = UserStatus.Active;
            return true;
        }

        if (string.Equals(estado?.Trim(), "suspendido", StringComparison.OrdinalIgnoreCase))
        {
            status = UserStatus.Suspended;
            return true;
        }

        if (string.Equals(estado?.Trim(), "desactivado", StringComparison.OrdinalIgnoreCase))
        {
            status = UserStatus.Deactivated;
            return true;
        }

        status = default;
        return false;
    }

    private static Guid? TryResolveActorUserId(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return null;
        }

        var subject = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out var actorUserId) ? actorUserId : null;
    }

    private static string GenerateOpaqueToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static AuditLog CreateAdminAuditLog(Guid? actorUserId, string? remoteIpAddress, string actionCode, object metadata)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actorUserId,
            Area = "admin.users",
            ActionCode = actionCode,
            MetadataJson = JsonSerializer.Serialize(metadata),
            IpAddress = remoteIpAddress,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}