using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using ORO_POS.Infrastructure.Data;
using System.Text.Json;

namespace ORO_POS.API.Security;

// Re-read permissions and outlet assignment for every request. Revoking a user or
// changing their role takes effect immediately, not only after their JWT expires.
public sealed class OutletAuthorizationFilter(AppDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any()) { await next(); return; }
        if (context.RouteData.Values["controller"]?.ToString() == "Auth" && context.RouteData.Values["action"]?.ToString() == "Login")
        { await next(); return; }
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) { await next(); return; }
        var id = int.TryParse(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var value) ? value : 0;
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
        if (user?.Role == null) { context.Result = new UnauthorizedResult(); return; }
        var stamp = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(user.PasswordHash)));
        if (context.HttpContext.User.FindFirstValue("SecurityStamp") != stamp)
        { context.Result = new UnauthorizedResult(); return; }
        var owner = user.Role.Name == "Owner";
        var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
        if (controller == "SuperAdmin" && !owner) { context.Result = new ForbidResult(); return; }
        var action = context.RouteData.Values["action"]?.ToString() ?? "";
        var write = !HttpMethods.IsGet(context.HttpContext.Request.Method);
        string? permission = controller switch
        {
            "Users" or "Roles" => "ManageUsers",
            "Outlets" when write => "ManageSettings",
            "Categories" or "Products" or "ModifierGroups" when write => "ManageSettings",
            "Tables" or "Zones" when write => "ManageSettings",
            "Inventory" or "Recipes" => "ManageInventory",
            "Reports" => action == "Export" ? "ExportReports" : "ViewReports",
            "Orders" when write => action switch {
                "Create" => "CreateOrder", "Void" => "VoidOrder", "Refund" => "RefundOrder",
                "ApplyDiscount" => "ApplyDiscount", "TransferTable" => "TransferTable",
                "Merge" => "MergeTable", "Split" => "SplitTable", "Receipt" => null, _ => "EditOrder"
            },
            _ => null
        };
        var permissions = JsonSerializer.Deserialize<string[]>(user.Role.Permissions ?? "[]") ?? [];
        var outletCatalogueAdmin = user.Role.Name == "StoreManager" && new[] {"Products","Categories","ModifierGroups"}.Contains(controller);
        var outletLocalAdmin = user.Role.Name == "StoreManager" && (new[]{"Tables","Zones"}.Contains(controller)||(controller=="Outlets"&&action=="Update"));
        if (!owner && !outletCatalogueAdmin && !outletLocalAdmin && permission != null && !permissions.Contains(permission))
        { context.Result = new ForbidResult(); return; }
        if (controller == "Orders" && action == "Receipt" && !owner && !permissions.Contains("EditOrder") && !permissions.Contains("ViewReports"))
        { context.Result = new ForbidResult(); return; }
        if (controller == "KitchenTickets" && write && !owner && !new[] { "KitchenStaff", "StoreManager", "Waiter" }.Contains(user.Role.Name))
        { context.Result = new ForbidResult(); return; }
        if (owner) { await next(); return; }
        if (!user.OutletId.HasValue) { context.Result = new ForbidResult(); return; }
        var outlet = user.OutletId.Value;

        // Optional list scope is forced to the assigned outlet rather than allowing
        // an omitted query parameter to return every outlet's records.
        if (context.ActionArguments.ContainsKey("outletId") && context.ActionArguments["outletId"] == null)
            context.ActionArguments["outletId"] = outlet;
        foreach (var argument in context.ActionArguments.ToArray())
        {
            if (argument.Value is AppDbContext) continue;
            var fields = argument.Value == null ? [] : argument.Value.GetType().IsPrimitive
                ? new[] { (argument.Key, (object?)argument.Value) }
                : argument.Value.GetType().GetProperties().Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                    .Select(p => (p.Name, p.GetValue(argument.Value))).ToArray();
            foreach (var (name, raw) in fields)
            {
                if (raw is not int resourceId || resourceId <= 0) continue;
                if (controller == "Users" && name.Equals("roleId", StringComparison.OrdinalIgnoreCase))
                {
                    var targetRole = await db.Roles.FindAsync(resourceId);
                    var targetPermissions = JsonSerializer.Deserialize<string[]>(targetRole?.Permissions ?? "[]") ?? [];
                    if (targetRole?.Name == "Owner" || targetPermissions.Except(permissions).Any())
                    { context.Result = new ForbidResult(); return; }
                }
                if (controller == "Users" && name.Equals("id", StringComparison.OrdinalIgnoreCase)
                    && await db.Users.AnyAsync(u => u.Id == resourceId && u.Role != null && u.Role.Name == "Owner"))
                { context.Result = new ForbidResult(); return; }
                int? resourceOutlet = name.ToLowerInvariant() switch
                {
                    "outletid" => resourceId,
                    "orderid" or "sourceorderid" or "targetorderid" => await db.Orders.Where(x => x.Id == resourceId).Select(x => (int?)x.OutletId).FirstOrDefaultAsync(),
                    "tableid" or "newtableid" => await db.Tables.Where(x => x.Id == resourceId).Select(x => (int?)x.OutletId).FirstOrDefaultAsync(),
                    "productid" or "ingredientid" => await db.Products.Where(x => x.Id == resourceId).Select(x => (int?)x.OutletId).FirstOrDefaultAsync(),
                    "categoryid" => await db.Categories.Where(x => x.Id == resourceId).Select(x => (int?)x.OutletId).FirstOrDefaultAsync(),
                    "zoneid" => await db.Zones.Where(x => x.Id == resourceId).Select(x => (int?)x.OutletId).FirstOrDefaultAsync(),
                    "shiftid" => await db.Shifts.Where(x => x.Id == resourceId).Select(x => (int?)x.OutletId).FirstOrDefaultAsync(),
                    "ticketid" => await db.KitchenTickets.Where(x => x.Id == resourceId).Select(x => (int?)x.Order.OutletId).FirstOrDefaultAsync(),
                    "userid" when controller == "Users" => await db.Users.Where(x => x.Id == resourceId).Select(x => x.OutletId).FirstOrDefaultAsync(),
                    "id" when controller == "Users" => await db.Users.Where(x => x.Id == resourceId).Select(x => x.OutletId).FirstOrDefaultAsync(),
                    "id" when controller == "Outlets" => resourceId,
                    _ => null
                };
                if (resourceOutlet.HasValue && resourceOutlet != outlet)
                { context.Result = new ForbidResult(); return; }
            }
        }
        // Recipes without a product filter and global role/outlet administration
        // expose multi-outlet configuration and are restricted to the owner.
        if (controller == "Outlets" && write && action != "Update")
        { context.Result = new ForbidResult(); return; }
        await next();
    }
}
