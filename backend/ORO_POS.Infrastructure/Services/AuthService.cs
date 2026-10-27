using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Entities;
using ORO_POS.Core.Enums;
using ORO_POS.Core.Interfaces;
using ORO_POS.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;

namespace ORO_POS.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Outlet)
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive && !u.IsDeleted);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid username or password.");

        var permissions = GetPermissions(user.Role);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role?.Name ?? string.Empty),
            new("OutletId", user.OutletId?.ToString() ?? string.Empty),
            new("Permissions", string.Join(",", permissions)),
        };

        var token = GenerateToken(claims);
        var refreshToken = GenerateRefreshToken();

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new LoginResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddHours(8),
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                RoleName = user.Role?.Name ?? string.Empty,
                Permissions = permissions,
                OutletId = user.OutletId,
                OutletName = user.Outlet?.Name,
                IsActive = user.IsActive
            }
        };
    }

    public async Task<bool> ValidatePermissionAsync(int userId, Permission permission)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user == null || user.Role == null) return false;
        return GetPermissions(user.Role).Contains(permission.ToString());
    }

    public async Task<bool> ValidateOutletAccessAsync(int userId, int outletId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user == null) return false;
        // Owner/admin can access all outlets; others restricted to their outlet
        if (user.Role != null && user.Role.Name == "Owner") return true;
        return user.OutletId == outletId;
    }

    private static string[] GetPermissions(Role? role)
    {
        if (role == null) return Array.Empty<string>();
        try
        {
            var perms = System.Text.Json.JsonSerializer.Deserialize<string[]>(role.Permissions ?? "[]");
            return perms ?? Array.Empty<string>();
        }
        catch { return Array.Empty<string>(); }
    }

    private string GenerateToken(IEnumerable<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? "ORO_POS_Super_Secret_Key_32Chars!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "ORO_POS",
            audience: _config["Jwt:Audience"] ?? "ORO_POS_Client",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken() => Guid.NewGuid().ToString("N");
}

public class PermissionSeedService
{
    private readonly AppDbContext _db;

    public PermissionSeedService(AppDbContext db) => _db = db;

    public async Task SeedRolesAndPermissionsAsync()
    {
        if (await _db.Roles.AnyAsync()) return;

        var roles = new[]
        {
            new Role { Name = "Owner", Description = "Full system access", Permissions = "[]", IsDeleted = false },
            new Role { Name = "StoreManager", Description = "Outlet management and approvals", Permissions = "[]", IsDeleted = false },
            new Role { Name = "Waiter", Description = "Floor order taking and table management", Permissions = "[]", IsDeleted = false },
            new Role { Name = "Cashier", Description = "Payment and receipt handling", Permissions = "[]", IsDeleted = false },
            new Role { Name = "KitchenStaff", Description = "Kitchen ticket management", Permissions = "[]", IsDeleted = false },
            new Role { Name = "InventoryController", Description = "Stock and recipe management", Permissions = "[]", IsDeleted = false },
            new Role { Name = "Finance", Description = "Financial reports and reconciliation", Permissions = "[]", IsDeleted = false },
        };

        var allPermissions = Enum.GetValues<Permission>().Select(p => p.ToString()).ToArray();

        foreach (var role in roles)
        {
            var rolePerms = role.Name switch
            {
                "Owner" => allPermissions,
                "StoreManager" => allPermissions.Where(p => p != nameof(Permission.ManageSettings) && p != nameof(Permission.ManagePeripherals)).ToArray(),
                "Waiter" => new[] { nameof(Permission.CreateOrder), nameof(Permission.EditOrder), nameof(Permission.TransferTable), nameof(Permission.MergeTable), nameof(Permission.SplitTable), nameof(Permission.ApplyDiscount) },
                "Cashier" => new[] { nameof(Permission.CreateOrder), nameof(Permission.EditOrder), nameof(Permission.VoidOrder), nameof(Permission.RefundOrder), nameof(Permission.ApplyDiscount) },
                "KitchenStaff" => Array.Empty<string>(),
                "InventoryController" => new[] { nameof(Permission.ManageInventory), nameof(Permission.RecordReceipt), nameof(Permission.RecordWaste), nameof(Permission.PhysicalCount) },
                "Finance" => new[] { nameof(Permission.ViewReports), nameof(Permission.ExportReports), nameof(Permission.CloseShift), nameof(Permission.ViewFinanceDashboard) },
                _ => Array.Empty<string>()
            };

            role.Permissions = System.Text.Json.JsonSerializer.Serialize(rolePerms);
            _db.Roles.Add(role);
        }

        await _db.SaveChangesAsync();
    }

    public async Task SeedDefaultOutletAndAdminAsync()
    {
        if (await _db.Outlets.AnyAsync()) return;

        var outlet = new Outlet
        {
            Name = "Main Outlet",
            Address = "123 Main Street",
            Phone = "+1-555-0100",
            Timezone = "UTC",
            Currency = "USD",
            IsOperating = true,
            IsDeleted = false
        };
        _db.Outlets.Add(outlet);
        await _db.SaveChangesAsync();

        var ownerRole = await _db.Roles.FirstAsync(r => r.Name == "Owner");
        var adminUser = new User
        {
            Username = "admin",
            Email = "admin@oropos.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            RoleId = ownerRole.Id,
            OutletId = outlet.Id,
            IsActive = true
        };
        _db.Users.Add(adminUser);
        await _db.SaveChangesAsync();
    }
}
