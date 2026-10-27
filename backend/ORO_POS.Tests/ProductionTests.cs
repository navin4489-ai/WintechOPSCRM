using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ORO_POS.API.Security;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Entities;
using ORO_POS.Infrastructure.Data;
using ORO_POS.Infrastructure.Services;

namespace ORO_POS.Tests;

public class ProductionTests
{
    private static AppDbContext Database()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Outlets.AddRange(new Outlet { Id = 1, Name = "A" }, new Outlet { Id = 2, Name = "B" });
        db.Roles.AddRange(new Role { Id = 1, Name = "Owner", Permissions = "[]" }, new Role { Id = 2, Name = "Waiter", Permissions = "[\"CreateOrder\",\"EditOrder\",\"ApplyDiscount\"]" });
        db.Users.AddRange(new User { Id = 1, Username = "owner", RoleId = 1, OutletId = 1, IsActive = true }, new User { Id = 2, Username = "waiter", RoleId = 2, OutletId = 1, IsActive = true });
        db.Orders.Add(new Order { Id = 1, OutletId = 2, Status = "Open" });
        db.SaveChanges();
        return db;
    }
    private static async Task<(IActionResult? Result, bool Executed, Dictionary<string, object?> Args)> Invoke(AppDbContext db, string controller, string action, string method, Dictionary<string, object?> args)
    {
        var http = new DefaultHttpContext();
        var stamp = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes((await db.Users.FindAsync(2))!.PasswordHash)));
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "2"), new Claim("SecurityStamp", stamp) }, "test"));
        http.Request.Method = method;
        var route = new RouteData(); route.Values["controller"] = controller; route.Values["action"] = action;
        var ctx = new ActionContext(http, route, new ActionDescriptor(), new ModelStateDictionary());
        var filterCtx = new ActionExecutingContext(ctx, new List<IFilterMetadata>(), args, new object());
        bool executed = false;
        await new OutletAuthorizationFilter(db).OnActionExecutionAsync(filterCtx, () => { executed = true; return Task.FromResult(new ActionExecutedContext(ctx, new List<IFilterMetadata>(), new object())); });
        return (filterCtx.Result, executed, args);
    }
    [Fact]
    public async Task CrossOutletOrderIsForbidden()
    {
        using var db = Database();
        var result = await Invoke(db, "Orders", "GetById", "GET", new() { ["orderId"] = 1 });
        Assert.IsType<ForbidResult>(result.Result); Assert.False(result.Executed);
    }
    [Fact]
    public async Task MissingListOutletIsForcedToAssignment()
    {
        using var db = Database();
        var result = await Invoke(db, "Orders", "GetAll", "GET", new() { ["outletId"] = null });
        Assert.True(result.Executed); Assert.Equal(1, result.Args["outletId"]);
    }
    [Fact]
    public async Task WaiterCannotRefundOrReadReports()
    {
        using var db = Database();
        Assert.IsType<ForbidResult>((await Invoke(db, "Orders", "Refund", "POST", new())).Result);
        Assert.IsType<ForbidResult>((await Invoke(db, "Reports", "GetSalesReport", "GET", new())).Result);
        Assert.IsType<ForbidResult>((await Invoke(db, "SuperAdmin", "List", "GET", new())).Result);
    }
    [Fact]
    public async Task DeactivatedSessionStopsImmediately()
    {
        using var db = Database(); (await db.Users.FindAsync(2))!.IsActive = false; await db.SaveChangesAsync();
        Assert.IsType<UnauthorizedResult>((await Invoke(db, "Orders", "GetAll", "GET", new())).Result);
    }
    [Fact]
    public async Task RefundIsAuditedOnceAndCashDrawerTracksPayout()
    {
        using var db = Database();
        db.Shifts.Add(new Shift { Id = 1, UserId = 1, OutletId = 1, Status = "Open" });
        db.Orders.Add(new Order { Id = 2, OutletId = 1, Status = "Closed", TotalAmount = 105, Subtotal = 100, TaxAmount = 5 });
        db.Payments.Add(new Payment { OrderId = 2, TenderType = "Cash", Amount = 105, Status = "Captured" });
        await db.SaveChangesAsync();
        var service = new OrderService(db, new InventoryService(db), new KitchenService(db));
        var result = await service.RefundAsync(2, new RefundRequest { Reason = "QA return", Reference = "Manual return 123" }, 1);
        Assert.Equal("Refunded", result.Status);
        Assert.Equal(105, (await db.Shifts.FindAsync(1))!.PaidOut);
        Assert.Single(await db.AuditLogs.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefundAsync(2, new RefundRequest { Reason = "QA repeat", Reference = "Manual return 123" }, 1));
        Assert.Single(await db.AuditLogs.ToListAsync());
    }
    [Fact]
    public async Task StatusEndpointCannotBypassCheckout()
    {
        using var db = Database();
        var service = new OrderService(db, new InventoryService(db), new KitchenService(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateOrderStatusAsync(1, "Closed", 1));
    }
    [Fact]
    public async Task ExportContainsActualReportRows()
    {
        using var db = Database();
        db.Orders.Add(new Order { Id = 2, OutletId = 1, Status = "Closed", Subtotal = 100, TaxAmount = 5, TotalAmount = 105 }); await db.SaveChangesAsync();
        var csv = System.Text.Encoding.UTF8.GetString(await new ReportService(db).ExportReportAsync("sales", new SalesReportRequest { OutletId = 1 }, 1));
        Assert.Contains("Gross INR", csv); Assert.Contains("100", csv); Assert.DoesNotContain("exported", csv);
    }

    [Fact]
    public async Task AddingItemsDoesNotDuplicateKitchenTickets()
    {
        using var db = Database();
        db.Products.Add(new Product { Id = 1, OutletId = 1, Name = "Meal", Price = 100 });
        db.Shifts.Add(new Shift { Id = 1, UserId = 1, OutletId = 1, Status = "Open" });
        await db.SaveChangesAsync();
        var service = new OrderService(db, new InventoryService(db), new KitchenService(db));
        var order = await service.CreateOrderAsync(new CreateOrderRequest { OutletId = 1, ServiceType = "Takeaway", Items = new() }, 1);
        await service.AddOrderItemAsync(order.Id, new OrderItemRequest { ProductId = 1, Quantity = 1, ModifierOptionIds = new() }, 1);
        await service.AddOrderItemAsync(order.Id, new OrderItemRequest { ProductId = 1, Quantity = 1, ModifierOptionIds = new() }, 1);
        Assert.Single(await db.KitchenTickets.Where(t => t.OrderId == order.Id).ToListAsync());
    }

    [Fact]
    public async Task PaidOrderCannotBeVoidedOrRefundedWithoutReference()
    {
        using var db = Database();
        db.Orders.Add(new Order { Id = 2, OutletId = 1, Status = "Closed" }); await db.SaveChangesAsync();
        var service = new OrderService(db, new InventoryService(db), new KitchenService(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VoidOrderAsync(2, new VoidOrderRequest { Reason = "QA" }, 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefundAsync(2, new RefundRequest { Reason = "QA" }, 1));
    }

    [Fact]
    public async Task PhysicalCountPostsDifferenceNotAbsoluteQuantity()
    {
        using var db = Database();
        db.Products.Add(new Product { Id = 1, OutletId = 1, Name = "Stock" });
        db.InventoryLedgers.Add(new InventoryLedger { ProductId = 1, MovementType = "Receipt", Quantity = 100 }); await db.SaveChangesAsync();
        var service = new InventoryService(db);
        var result = await service.RecordInventoryMovementAsync(new RecordInventoryRequest { ProductId = 1, MovementType = "PhysicalCount", Quantity = 80, Source = "QA count" }, 1);
        Assert.Equal("Adjustment", result.MovementType); Assert.Equal(-20m, result.Quantity);
        var stock = await service.GetStockLevelsAsync(1, 1);
        Assert.Equal(80m, Assert.Single(stock).CurrentStock);
    }

    [Fact]
    public async Task NonManagerDiscountRequiresVerifiedManagerApproval()
    {
        using var db = Database();
        (await db.Users.FindAsync(1))!.PasswordHash = BCrypt.Net.BCrypt.HashPassword("ManagerSecure123");
        db.Products.Add(new Product { Id = 1, OutletId = 1, Name = "Meal" });
        db.Orders.Add(new Order { Id = 2, OutletId = 1, Status = "Open", Subtotal = 100, TotalAmount = 105 });
        db.OrderItems.Add(new OrderItem { OrderId = 2, ProductId = 1, Quantity = 1, LineTotal = 100 }); await db.SaveChangesAsync();
        var service = new OrderService(db, new InventoryService(db), new KitchenService(db));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApplyDiscountAsync(2, new ApplyDiscountRequest { Percent = 10, Reason = "QA approval" }, 2));
        var result = await service.ApplyDiscountAsync(2, new ApplyDiscountRequest { Percent = 10, Reason = "QA approval", ApproverUsername = "owner", ApproverPassword = "ManagerSecure123" }, 2);
        Assert.Equal(10m, result.DiscountAmount);
        var audit = Assert.Single(await db.AuditLogs.ToListAsync());
        Assert.Contains("approved by owner", audit.Reason);
        Assert.DoesNotContain("ManagerSecure123", audit.AfterValues ?? "");
    }
}
