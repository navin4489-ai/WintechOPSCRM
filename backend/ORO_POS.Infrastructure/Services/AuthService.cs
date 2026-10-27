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
            new("SecurityStamp", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash)))),
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
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
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
    private readonly IConfiguration _configuration;

    public PermissionSeedService(AppDbContext db, IConfiguration configuration) { _db = db; _configuration = configuration; }

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
        var adminPassword = _configuration["INITIAL_ADMIN_PASSWORD"];
        if (string.IsNullOrEmpty(adminPassword))
        {
            if (_configuration["ASPNETCORE_ENVIRONMENT"] == "Production")
                throw new InvalidOperationException("INITIAL_ADMIN_PASSWORD is required when provisioning a new production database.");
            adminPassword = "Admin@123";
        }
        if (adminPassword != "Admin@123" && adminPassword.Length < 12)
            throw new InvalidOperationException("Initial admin password must contain at least 12 characters.");
        if (_configuration["ASPNETCORE_ENVIRONMENT"] == "Production" && adminPassword.Length < 12)
            throw new InvalidOperationException("Production initial admin password must contain at least 12 characters.");

        var outlet = new Outlet
        {
            Name = "Main Outlet",
            Address = "123 Main Street",
            Phone = "+1-555-0100",
            Timezone = "Asia/Kolkata",
            Currency = "INR",
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
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            RoleId = ownerRole.Id,
            OutletId = outlet.Id,
            IsActive = true
        };
        _db.Users.Add(adminUser);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Fills an outlet that has no menu with a working demo menu (categories +
    /// products priced in ₹), opening stock, zones and tables, so a fresh deploy
    /// is usable immediately. Every block is guarded, so re-running the seed only
    /// ever fills gaps and never duplicates data.
    /// </summary>
    public async Task SeedMenuAndFloorAsync()
    {
        var outlet = await _db.Outlets.OrderBy(o => o.Id).FirstOrDefaultAsync();
        if (outlet == null) return;

        // Platform runs in Indian Rupees; older rows were seeded as USD.
        if (!string.Equals(outlet.Currency, "INR", StringComparison.OrdinalIgnoreCase))
        {
            outlet.Currency = "INR";
            outlet.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        if (!string.Equals(_configuration["SEED_DEMO_DATA"], "true", StringComparison.OrdinalIgnoreCase)) return;
        await SeedCategoriesAndProductsAsync(outlet.Id);
        await SeedOpeningStockAsync(outlet.Id);
        await SeedZonesAndTablesAsync(outlet.Id);
    }

    private async Task SeedCategoriesAndProductsAsync(int outletId)
    {
        if (await _db.Categories.AnyAsync(c => c.OutletId == outletId)) return;

        var menu = new (string Name, int Sort, (string Item, string Sku, string Desc, decimal Price, int Ord)[] Items)[]
        {
            ("Starters", 1, new[]
            {
                ("Paneer Tikka", "STA-001", "Char-grilled cottage cheese skewers", 280m, 1),
                ("Chicken Tikka", "STA-002", "Tandoori marinated chicken", 320m, 2),
                ("Veg Spring Rolls", "STA-003", "Crispy rolled vegetables", 180m, 3),
                ("Tandoori Mushroom", "STA-004", "Stuffed button mushrooms", 240m, 4),
            }),
            ("Main Course", 2, new[]
            {
                ("Butter Chicken", "MAI-001", "Creamy tomato gravy", 380m, 1),
                ("Paneer Butter Masala", "MAI-002", "Cottage cheese in makhani", 320m, 2),
                ("Dal Tadka", "MAI-003", "Yellow lentils, tempered", 220m, 3),
                ("Kadai Vegetable", "MAI-004", "Wok-tossed seasonal vegetables", 280m, 4),
                ("Chicken Biryani", "MAI-005", "Basmati rice, slow cooked", 350m, 5),
            }),
            ("Breads & Rice", 3, new[]
            {
                ("Butter Naan", "BRD-001", "Tandoor bread finished with butter", 60m, 1),
                ("Tandoori Roti", "BRD-002", "Whole wheat tandoor bread", 40m, 2),
                ("Jeera Rice", "BRD-003", "Cumin basmati rice", 150m, 3),
                ("Veg Pulao", "BRD-004", "Rice with seasonal vegetables", 190m, 4),
            }),
            ("Beverages", 4, new[]
            {
                ("Masala Chai", "BEV-001", "Spiced Indian tea", 50m, 1),
                ("Fresh Lime Soda", "BEV-002", "Sweet or salted lime soda", 70m, 2),
                ("Mango Lassi", "BEV-003", "Sweet yogurt drink", 90m, 3),
                ("Mineral Water 1L", "BEV-004", "Packaged drinking water", 40m, 4),
            }),
            ("Desserts", 5, new[]
            {
                ("Gulab Jamun", "DES-001", "Warm milk dumplings in syrup", 90m, 1),
                ("Rasmalai", "DES-002", "Cottage cheese in saffron milk", 110m, 2),
                ("Vanilla Ice Cream", "DES-003", "Two scoops", 80m, 3),
            }),
        };

        foreach (var (catName, catSort, items) in menu)
        {
            var category = new Category { OutletId = outletId, Name = catName, SortOrder = catSort };
            _db.Categories.Add(category);
            await _db.SaveChangesAsync();

            foreach (var (item, sku, desc, price, ord) in items)
            {
                _db.Products.Add(new Product
                {
                    OutletId = outletId,
                    CategoryId = category.Id,
                    SKU = sku,
                    Name = item,
                    Description = desc,
                    Price = price,
                    TaxClass = "Standard",
                    IsAvailable = true,
                    SortOrder = ord
                });
            }
            await _db.SaveChangesAsync();
        }
    }

    private async Task SeedOpeningStockAsync(int outletId)
    {
        if (await _db.InventoryLedgers.AnyAsync()) return;

        var products = await _db.Products
            .Where(p => p.OutletId == outletId)
            .OrderBy(p => p.Id)
            .ToListAsync();
        if (products.Count == 0) return;

        foreach (var product in products)
        {
            _db.InventoryLedgers.Add(new InventoryLedger
            {
                ProductId = product.Id,
                MovementType = "Receipt",
                Quantity = 100,
                Location = "Main Store",
                Source = "OpeningStock"
            });
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedZonesAndTablesAsync(int outletId)
    {
        int? zoneId;
        if (await _db.Zones.AnyAsync(z => z.OutletId == outletId))
        {
            zoneId = await _db.Zones
                .Where(z => z.OutletId == outletId)
                .OrderBy(z => z.Id)
                .Select(z => (int?)z.Id)
                .FirstOrDefaultAsync();
        }
        else
        {
            var indoor = new Zone { OutletId = outletId, Name = "Indoor" };
            var outdoor = new Zone { OutletId = outletId, Name = "Outdoor" };
            _db.Zones.AddRange(indoor, outdoor);
            await _db.SaveChangesAsync();
            zoneId = indoor.Id;
        }

        var existingNumbers = await _db.Tables
            .Where(t => t.OutletId == outletId)
            .Select(t => t.TableNumber)
            .ToListAsync();

        for (var i = 1; i <= 8; i++)
        {
            var number = i.ToString();
            if (existingNumbers.Contains(number)) continue;

            _db.Tables.Add(new Table
            {
                OutletId = outletId,
                ZoneId = zoneId,
                TableNumber = number,
                Status = "Free",
                Capacity = i % 4 == 0 ? 6 : 4
            });
        }
        await _db.SaveChangesAsync();
    }
}
