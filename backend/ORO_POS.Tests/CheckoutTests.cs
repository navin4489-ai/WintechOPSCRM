using Microsoft.EntityFrameworkCore;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Entities;
using ORO_POS.Infrastructure.Data;
using ORO_POS.Infrastructure.Services;

namespace ORO_POS.Tests;

public class CheckoutTests : IDisposable
{
    private readonly AppDbContext db;
    private readonly OrderService orders;

    public CheckoutTests()
    {
        db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Outlets.Add(new Outlet { Id = 1, Name = "Test", Currency = "INR" });
        db.Roles.Add(new Role { Id = 1, Name = "Owner", Permissions = "[]" });
        db.Users.Add(new User { Id = 1, Username = "test", OutletId = 1, RoleId = 1, IsActive = true });
        db.Products.Add(new Product { Id = 1, OutletId = 1, Name = "Test meal", Price = 600 });
        db.Shifts.Add(new Shift { Id = 1, UserId = 1, OutletId = 1, OpeningFloat = 500, Status = "Open" });
        db.Orders.Add(new Order { Id = 1, OutletId = 1, UserId = 1, ShiftId = 1, Status = "Open", Subtotal = 600, TotalAmount = 600 });
        db.OrderItems.Add(new OrderItem { Id = 1, OrderId = 1, ProductId = 1, ProductName = "Test meal", Quantity = 1, LineTotal = 600, Status = "New" });
        db.SaveChanges();
        orders = new OrderService(db, new InventoryService(db), new KitchenService(db));
    }

    [Fact]
    public async Task DiscountPersistsGstAndTotal()
    {
        var result = await orders.ApplyDiscountAsync(1, new ApplyDiscountRequest { Percent = 10 }, 1);
        Assert.Equal(60m, result.DiscountAmount);
        Assert.Equal(27m, result.TaxAmount);
        Assert.Equal(567m, result.TotalAmount);
        Assert.Single(await db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task InvalidPercentageIsRejected(int percent)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => orders.ApplyDiscountAsync(1, new ApplyDiscountRequest { Percent = percent }, 1));
    }

    [Fact]
    public async Task CashChangeIsNotRevenueAndInventoryPostsOnce()
    {
        var result = await orders.ProcessPaymentAsync(1, new ProcessPaymentRequest { TenderType = "Cash", Amount = 1000 }, 1);
        Assert.Equal("Closed", result.Status);
        Assert.Equal(30m, result.TaxAmount);
        Assert.Equal(630m, result.TotalAmount);
        Assert.Equal(630m, Assert.Single(result.Payments).Amount);
        Assert.Equal(1m, Assert.Single(await db.InventoryLedgers.ToListAsync()).Quantity);
        await Assert.ThrowsAsync<InvalidOperationException>(() => orders.ProcessPaymentAsync(1, new ProcessPaymentRequest { TenderType = "Cash", Amount = 630 }, 1));
        Assert.Single(await db.Payments.ToListAsync());
        Assert.Single(await db.InventoryLedgers.ToListAsync());
    }

    [Fact]
    public async Task PartialPaymentsDeductStockOnlyOnSettlement()
    {
        var first = await orders.ProcessPaymentAsync(1, new ProcessPaymentRequest { TenderType = "Cash", Amount = 100 }, 1);
        Assert.Equal("Open", first.Status);
        Assert.Empty(await db.InventoryLedgers.ToListAsync());
        var second = await orders.ProcessPaymentAsync(1, new ProcessPaymentRequest { TenderType = "UPI", Amount = 530 }, 1);
        Assert.Equal("Closed", second.Status);
        Assert.Equal(630m, second.Payments.Sum(p => p.Amount));
        Assert.Single(await db.InventoryLedgers.ToListAsync());
    }

    [Theory]
    [InlineData(0, "Cash")]
    [InlineData(-10, "Cash")]
    [InlineData(1000, "Card")]
    [InlineData(630, "Invalid")]
    public async Task InvalidPaymentsAreRejected(decimal amount, string tender)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => orders.ProcessPaymentAsync(1, new ProcessPaymentRequest { Amount = amount, TenderType = tender }, 1));
        Assert.Empty(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task ShiftCloseAndZReportAgree()
    {
        await orders.ProcessPaymentAsync(1, new ProcessPaymentRequest { TenderType = "Cash", Amount = 630 }, 1);
        var shifts = new ShiftService(db);
        var closed = await shifts.CloseShiftAsync(new CloseShiftRequest { ShiftId = 1, CountedCash = 1130 }, 1);
        var report = await shifts.GenerateZReportAsync(1, 1);
        Assert.Equal(0m, closed.Variance);
        Assert.Equal(closed.Variance, report.Variance);
        Assert.Equal(1130m, report.CashExpected);
        Assert.Equal(600m, report.GrossSales);
        Assert.Equal(30m, report.Taxes);
    }

    [Fact]
    public async Task RemovingItemPersistsTotals()
    {
        await orders.RemoveOrderItemAsync(1, 1, 1);
        db.ChangeTracker.Clear();
        var saved = await db.Orders.FindAsync(1);
        Assert.Equal(0m, saved!.Subtotal);
        Assert.Equal(0m, saved.TotalAmount);
    }

    [Fact]
    public async Task SalesReportDoesNotSubtractDiscountTwiceAndIncludesUpi()
    {
        await orders.ApplyDiscountAsync(1, new ApplyDiscountRequest { Percent = 10 }, 1);
        await orders.ProcessPaymentAsync(1, new ProcessPaymentRequest { TenderType = "UPI", Amount = 567 }, 1);
        var reports = new ReportService(db);
        var report = Assert.Single(await reports.GetSalesReportAsync(new SalesReportRequest { OutletId = 1 }, 1));
        Assert.Equal(600m, report.GrossSales);
        Assert.Equal(60m, report.Discounts);
        Assert.Equal(540m, report.NetSales);
        Assert.Equal(27m, report.Taxes);
        Assert.Equal(567m, report.DigitalWallet);
    }

    [Fact]
    public async Task RecipeConsumptionUsesIngredientQuantities()
    {
        db.Products.Add(new Product { Id = 2, OutletId = 1, Name = "Ingredient" });
        db.Recipes.Add(new Recipe { ProductId = 1, IngredientId = 2, Quantity = 0.25m });
        await db.SaveChangesAsync();
        await orders.ProcessPaymentAsync(1, new ProcessPaymentRequest { TenderType = "Cash", Amount = 630 }, 1);
        var ledger = Assert.Single(await db.InventoryLedgers.ToListAsync());
        Assert.Equal(2, ledger.ProductId);
        Assert.Equal(0.25m, ledger.Quantity);
    }

    [Fact]
    public async Task TableLookupFindsOrderRegardlessOfHistorySize()
    {
        db.Tables.Add(new Table { Id = 1, OutletId = 1, TableNumber = "T1" });
        var open = await db.Orders.FindAsync(1);
        open!.TableId = 1;
        for (var i = 2; i < 65; i++)
            db.Orders.Add(new Order { Id = i, OutletId = 1, UserId = 1, Status = "Closed" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var result = await orders.GetOpenOrderForTableAsync(1, 1);
        Assert.Equal(1, result!.Id);
        Assert.Single(result.OrderItems);
    }

    public void Dispose() => db.Dispose();
}
