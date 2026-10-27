using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Entities;
using ORO_POS.Core.Enums;
using ORO_POS.Core.Interfaces;
using ORO_POS.Infrastructure.Data;

namespace ORO_POS.Infrastructure.Services;

/// <summary>
/// Staff account management (BRD HR-001): create users, assign roles and
/// outlet scope, edit and deactivate accounts. All mutations are audit-logged
/// (BRD RPT-005) and restricted to holders of <see cref="Permission.ManageUsers"/>.
/// </summary>
public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly IAuthService _auth;

    public UserService(AppDbContext db, IAuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<List<UserDto>> GetUsersAsync(int actorId)
    {
        await RequireManageUsersAsync(actorId);

        var actor = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == actorId)
            ?? throw new UnauthorizedAccessException("Unknown user.");

        var query = _db.Users
            .Include(u => u.Role)
            .Include(u => u.Outlet)
            .Where(u => !u.IsDeleted);

        // Non-owner managers see only their own outlet's staff.
        if (actor.Role?.Name != "Owner")
        {
            query = query.Where(u => u.OutletId == actor.OutletId);
        }

        var users = await query.OrderBy(u => u.Username).ToListAsync();
        return users.Select(Map).ToList();
    }

    public async Task<UserDto?> GetUserByIdAsync(int id, int actorId)
    {
        await RequireManageUsersAsync(actorId);

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Outlet)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

        return user == null ? null : Map(user);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, int actorId)
    {
        await RequireManageUsersAsync(actorId);

        var username = request.Username.Trim();
        var email = request.Email.Trim();

        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Username is required.");
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Email is required.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new InvalidOperationException("Password must be at least 6 characters.");

        var usernameTaken = await _db.Users.AnyAsync(u => u.Username == username && !u.IsDeleted);
        if (usernameTaken)
            throw new InvalidOperationException($"Username '{username}' is already in use.");

        var emailTaken = await _db.Users.AnyAsync(u => u.Email == email && !u.IsDeleted);
        if (emailTaken)
            throw new InvalidOperationException($"Email '{email}' is already registered.");

        var (role, outlet) = await ResolveRoleAndOutletAsync(request.RoleId, request.OutletId);

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            RoleId = role?.Id,
            OutletId = outlet?.Id,
            IsActive = true,
            CreatedBy = actorId.ToString(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await AuditAsync(actorId, "CreateUser", user.Id, null, new
        {
            user.Username,
            user.Email,
            Role = role?.Name,
            Outlet = outlet?.Name,
            user.IsActive
        });

        user.Role = role;
        user.Outlet = outlet;
        return Map(user);
    }

    public async Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request, int actorId)
    {
        await RequireManageUsersAsync(actorId);

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Outlet)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted)
            ?? throw new KeyNotFoundException($"User {id} not found.");

        var username = request.Username.Trim();
        var email = request.Email.Trim();

        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Username is required.");
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Email is required.");

        var usernameTaken = await _db.Users
            .AnyAsync(u => u.Id != id && u.Username == username && !u.IsDeleted);
        if (usernameTaken)
            throw new InvalidOperationException($"Username '{username}' is already in use.");

        var emailTaken = await _db.Users
            .AnyAsync(u => u.Id != id && u.Email == email && !u.IsDeleted);
        if (emailTaken)
            throw new InvalidOperationException($"Email '{email}' is already registered.");

        var (role, outlet) = await ResolveRoleAndOutletAsync(request.RoleId, request.OutletId);

        // Guard: never let the last active Owner lock everyone out.
        var wasOwner = user.Role?.Name == "Owner";
        var willBeOwner = role?.Name == "Owner";
        if (wasOwner && (!willBeOwner || !request.IsActive) && await IsLastActiveOwnerAsync(user.Id))
            throw new InvalidOperationException("Cannot demote or deactivate the last active Owner account.");

        var before = new
        {
            user.Username,
            user.Email,
            Role = user.Role?.Name,
            Outlet = user.Outlet?.Name,
            user.IsActive
        };

        user.Username = username;
        user.Email = email;
        user.RoleId = role?.Id;
        user.OutletId = outlet?.Id;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = actorId.ToString();

        // Password change is optional on update.
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password.Length < 6)
                throw new InvalidOperationException("Password must be at least 6 characters.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        await _db.SaveChangesAsync();

        await AuditAsync(actorId, "UpdateUser", user.Id, before, new
        {
            user.Username,
            user.Email,
            Role = role?.Name,
            Outlet = outlet?.Name,
            user.IsActive,
            PasswordChanged = !string.IsNullOrWhiteSpace(request.Password)
        });

        user.Role = role;
        user.Outlet = outlet;
        return Map(user);
    }

    /// <summary>
    /// Soft-deletes a staff account. The row is retained for FK integrity and
    /// audit history; the user can no longer sign in (login filters IsDeleted).
    /// </summary>
    public async Task<UserDto> DeleteUserAsync(int id, int actorId)
    {
        await RequireManageUsersAsync(actorId);

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Outlet)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted)
            ?? throw new KeyNotFoundException($"User {id} not found.");

        if (user.Id == actorId)
            throw new InvalidOperationException("You cannot delete your own account.");

        if (user.Role?.Name == "Owner" && await IsLastActiveOwnerAsync(user.Id))
            throw new InvalidOperationException("Cannot delete the last active Owner account.");

        // Close any open shift so the account does not strand a shift record.
        var openShifts = await _db.Shifts
            .Where(s => s.UserId == id && s.Status == ShiftStatus.Open.ToString())
            .ToListAsync();
        foreach (var shift in openShifts)
        {
            shift.Status = ShiftStatus.Closed.ToString();
            shift.CloseTime = DateTime.UtcNow;
            shift.UpdatedAt = DateTime.UtcNow;
        }

        user.IsDeleted = true;
        user.IsActive = false;
        user.DeletedAt = DateTime.UtcNow;
        user.DeletedBy = actorId.ToString();
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await AuditAsync(actorId, "DeleteUser", user.Id, new
        {
            user.Username,
            user.Email,
            Role = user.Role?.Name,
            Outlet = user.Outlet?.Name,
            user.IsActive
        }, null);

        return Map(user);
    }

    public async Task<List<RoleDto>> GetRolesAsync()
    {
        var roles = await _db.Roles.Where(r => !r.IsDeleted).OrderBy(r => r.Name).ToListAsync();
        return roles.Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Permissions = ParsePermissions(r.Permissions)
        }).ToList();
    }

    // --- helpers -----------------------------------------------------------

    private async Task RequireManageUsersAsync(int actorId)
    {
        var can = await _auth.ValidatePermissionAsync(actorId, Permission.ManageUsers);
        if (!can) throw new UnauthorizedAccessException("ManageUsers permission required.");
    }

    private async Task<(Role? role, Outlet? outlet)> ResolveRoleAndOutletAsync(int? roleId, int? outletId)
    {
        Role? role = null;
        if (roleId.HasValue)
        {
            role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId.Value && !r.IsDeleted)
                ?? throw new InvalidOperationException($"Role {roleId} does not exist.");
        }

        Outlet? outlet = null;
        if (outletId.HasValue)
        {
            outlet = await _db.Outlets.FirstOrDefaultAsync(o => o.Id == outletId.Value && !o.IsDeleted)
                ?? throw new InvalidOperationException($"Outlet {outletId} does not exist.");
        }

        return (role, outlet);
    }

    private async Task<bool> IsLastActiveOwnerAsync(int exceptUserId)
    {
        return !await _db.Users.AnyAsync(u =>
            u.Id != exceptUserId &&
            !u.IsDeleted &&
            u.IsActive &&
            u.Role != null &&
            u.Role.Name == "Owner");
    }

    private async Task AuditAsync(int actorId, string action, int entityId, object? before, object? after)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            Action = action,
            Entity = "User",
            EntityId = entityId,
            BeforeValues = before == null ? null : JsonSerializer.Serialize(before),
            AfterValues = after == null ? null : JsonSerializer.Serialize(after),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private static string[] ParsePermissions(string? json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json ?? "[]") ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static UserDto Map(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        RoleName = u.Role?.Name,
        Permissions = ParsePermissions(u.Role?.Permissions),
        OutletId = u.OutletId,
        OutletName = u.Outlet?.Name,
        IsActive = u.IsActive
    };
}
