using Microsoft.EntityFrameworkCore;
using ORO_POS.Core.Entities;
using ORO_POS.Infrastructure.Data;

namespace ORO_POS.Infrastructure.Services;

// Services enforce tenancy independently of controller/query-string checks.
// A missing outlet means "my outlet" for tenant users, never "all outlets".
public static class TenantAccess
{
    public static async Task<User> ActorAsync(AppDbContext db, int userId) =>
        await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId && u.IsActive && u.Role != null)
        ?? throw new UnauthorizedAccessException("Active account and role required.");

    public static async Task<int?> ScopeAsync(AppDbContext db, int userId, int? requestedOutlet)
    {
        var actor = await ActorAsync(db, userId);
        if (actor.Role!.Name == "Owner") return requestedOutlet;
        if (!actor.OutletId.HasValue || (requestedOutlet.HasValue && requestedOutlet.Value != actor.OutletId.Value))
            throw new UnauthorizedAccessException("Access is restricted to your assigned outlet.");
        return actor.OutletId.Value;
    }
    public static async Task RequireAsync(AppDbContext db, int userId, int outletId) { await ScopeAsync(db, userId, outletId); }
    public static async Task RequireOwnerAsync(AppDbContext db, int userId)
    {
        if ((await ActorAsync(db, userId)).Role!.Name != "Owner") throw new UnauthorizedAccessException("Only Super Admin can create outlets.");
    }
    public static async Task RequireOutletAdminAsync(AppDbContext db, int userId, int outletId)
    {
        await RequireAsync(db, userId, outletId);
        var actor = await ActorAsync(db, userId);
        if (actor.Role!.Name is not ("Owner" or "StoreManager")) throw new UnauthorizedAccessException("Outlet administrator access required.");
    }
}
