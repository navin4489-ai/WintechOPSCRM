using Microsoft.EntityFrameworkCore;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Entities;
using ORO_POS.Core.Enums;
using ORO_POS.Core.Interfaces;
using ORO_POS.Infrastructure.Data;
using System.Text.Json;

namespace ORO_POS.Infrastructure.Services;

public class KitchenService : IKitchenService
{
    private readonly AppDbContext _db;

    public KitchenService(AppDbContext db) => _db = db;

    public async Task<List<KitchenTicketDto>> GetKitchenTicketsAsync(int? outletId, int? stationId, int userId)
    {
        var query = _db.KitchenTickets
            .Include(kt => kt.Order).ThenInclude(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .Include(kt => kt.Order).ThenInclude(o => o.Table)
            .Where(kt => !kt.IsDeleted);

        if (outletId.HasValue)
            query = query.Where(kt => kt.Order.OutletId == outletId.Value);

        if (stationId.HasValue)
            query = query.Where(kt => kt.StationId == stationId.Value);

        var tickets = await query.OrderBy(kt => kt.CreatedAt).ToListAsync();

        return tickets.Select(MapToDto).ToList();
    }

    public async Task<KitchenTicketDto?> GetKitchenTicketByIdAsync(int ticketId, int userId)
    {
        var ticket = await _db.KitchenTickets
            .Include(kt => kt.Order).ThenInclude(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .Include(kt => kt.Order).ThenInclude(o => o.Table)
            .FirstOrDefaultAsync(kt => kt.Id == ticketId && !kt.IsDeleted);
        return ticket == null ? null : MapToDto(ticket);
    }

    public async Task<KitchenTicketDto> UpdateTicketStatusAsync(UpdateTicketStatusRequest request, int userId)
    {
        var ticket = await _db.KitchenTickets.FindAsync(request.TicketId);
        if (ticket == null || ticket.IsDeleted) throw new KeyNotFoundException("Ticket not found.");

        ticket.Status = request.Status;
        ticket.UpdatedAt = DateTime.UtcNow;

        if (request.Status == KitchenTicketStatus.Accepted.ToString())
            ticket.AcceptedAt = DateTime.UtcNow;
        else if (request.Status == KitchenTicketStatus.Ready.ToString())
            ticket.ReadyAt = DateTime.UtcNow;
        else if (request.Status == KitchenTicketStatus.Served.ToString())
            ticket.ServedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetKitchenTicketByIdAsync(request.TicketId, userId) ?? throw new InvalidOperationException("Ticket not found after update.");
    }

    public async Task<List<KitchenTicketDto>> GetTicketsByStationAsync(int stationId, int userId)
    {
        return await GetKitchenTicketsAsync(null, stationId, userId);
    }

    private static KitchenTicketDto MapToDto(KitchenTicket kt)
    {
        return new KitchenTicketDto
        {
            Id = kt.Id,
            OrderId = kt.OrderId,
            OrderNumber = kt.Order.OrderNumber,
            TableNumber = kt.Order.Table == null ? null : kt.Order.Table.TableNumber,
            StationId = kt.StationId,
            StationName = kt.StationName,
            Status = kt.Status,
            Priority = kt.Priority,
            CreatedAt = kt.CreatedAt,
            AcceptedAt = kt.AcceptedAt,
            ReadyAt = kt.ReadyAt,
            ServedAt = kt.ServedAt,
            Items = kt.Order.OrderItems.Select(oi => new KitchenTicketItemDto
            {
                OrderItemId = oi.Id,
                ProductName = oi.ProductName,
                Quantity = oi.Quantity,
                Modifiers = oi.Modifiers,
                Notes = oi.Notes,
                Status = oi.Status
            }).ToList()
        };
    }
}

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;

    public InventoryService(AppDbContext db) => _db = db;

    public async Task<List<ProductStockDto>> GetStockLevelsAsync(int? outletId, int userId)
    {
        var query = _db.Products.Include(p => p.InventoryLedgers).Where(p => !p.IsDeleted);
        if (outletId.HasValue) query = query.Where(p => p.OutletId == outletId.Value);

        var products = await query.ToListAsync();
        return products.Select(p => new ProductStockDto
        {
            ProductId = p.Id,
            ProductName = p.Name,
            SKU = p.SKU,
            CurrentStock = CalculateStock(p),
            IsLowStock = CalculateStock(p) <= 5 // default threshold
        }).ToList();
    }

    public async Task<List<InventoryLedgerDto>> GetInventoryMovementsAsync(int? outletId, PagedRequest request, int userId)
    {
        var query = _db.InventoryLedgers.Include(i => i.Product).Include(i => i.Actor).Where(i => !i.IsDeleted);
        if (outletId.HasValue) query = query.Where(i => i.Product != null && i.Product.OutletId == outletId.Value);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(i => i.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(i => new InventoryLedgerDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.Product == null ? "Unknown" : i.Product.Name,
                MovementType = i.MovementType,
                Quantity = i.Quantity,
                Location = i.Location,
                Batch = i.Batch,
                Source = i.Source,
                ActorId = i.ActorId,
                ActorName = i.Actor == null ? null : i.Actor.Username,
                CreatedAt = i.CreatedAt
            }).ToListAsync();

        return items;
    }

    public async Task<InventoryLedgerDto> RecordInventoryMovementAsync(RecordInventoryRequest request, int actorId)
    {
        var product = request.ProductId.HasValue ? await _db.Products.FindAsync(request.ProductId.Value) : null;
        var actor = await _db.Users.FindAsync(actorId);

        var ledger = new InventoryLedger
        {
            ProductId = request.ProductId,
            MovementType = request.MovementType,
            Quantity = request.Quantity,
            Location = request.Location,
            Batch = request.Batch,
            Source = request.Source,
            ActorId = actorId,
            CreatedAt = DateTime.UtcNow
        };

        _db.InventoryLedgers.Add(ledger);
        await _db.SaveChangesAsync();

        return new InventoryLedgerDto
        {
            Id = ledger.Id,
            ProductId = ledger.ProductId,
            ProductName = product?.Name ?? "Unknown",
            MovementType = ledger.MovementType,
            Quantity = ledger.Quantity,
            Location = ledger.Location,
            Batch = ledger.Batch,
            Source = ledger.Source,
            ActorId = ledger.ActorId,
            ActorName = actor?.Username,
            CreatedAt = ledger.CreatedAt
        };
    }

    public async Task<List<ProductStockDto>> GetLowStockItemsAsync(int? outletId, int userId)
    {
        var all = await GetStockLevelsAsync(outletId, userId);
        return all.Where(s => s.IsLowStock).ToList();
    }

    public async Task<RecipeDto> CreateRecipeAsync(CreateRecipeRequest request, int userId)
    {
        var product = await _db.Products.FindAsync(request.ProductId);
        var ingredient = await _db.Products.FindAsync(request.IngredientId);
        if (product == null || ingredient == null) throw new KeyNotFoundException("Product or ingredient not found.");

        var recipe = new Recipe
        {
            ProductId = request.ProductId,
            IngredientId = request.IngredientId,
            Quantity = request.Quantity,
            Unit = request.Unit
        };

        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();

        return MapToDto(recipe, product.Name, ingredient.Name);
    }

    public async Task<List<RecipeDto>> GetRecipesAsync(int? productId, int userId)
    {
        var query = _db.Recipes.Include(r => r.Product).Include(r => r.Ingredient).Where(r => !r.IsDeleted);
        if (productId.HasValue) query = query.Where(r => r.ProductId == productId.Value);

        var recipes = await query.ToListAsync();
        return recipes.Select(r => MapToDto(r, r.Product.Name, r.Ingredient.Name)).ToList();
    }

    public static decimal CalculateStock(Product p)
    {
        var received = p.InventoryLedgers.Where(i => i.MovementType == InventoryMovementType.Receipt.ToString()).Sum(i => i.Quantity);
        var sold = p.InventoryLedgers.Where(i => i.MovementType == InventoryMovementType.Sale.ToString()).Sum(i => i.Quantity);
        var adjusted = p.InventoryLedgers.Where(i => i.MovementType == InventoryMovementType.Adjustment.ToString()).Sum(i => i.Quantity);
        var wasted = p.InventoryLedgers.Where(i => i.MovementType == InventoryMovementType.Waste.ToString()).Sum(i => i.Quantity);
        return received - sold + adjusted - wasted;
    }

    private static RecipeDto MapToDto(Recipe r, string productName, string ingredientName)
    {
        return new RecipeDto
        {
            Id = r.Id,
            ProductId = r.ProductId,
            ProductName = productName,
            IngredientId = r.IngredientId,
            IngredientName = ingredientName,
            Quantity = r.Quantity,
            Unit = r.Unit
        };
    }
}

public class ShiftService : IShiftService
{
    private readonly AppDbContext _db;

    public ShiftService(AppDbContext db) => _db = db;

    public async Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, int userId)
    {
        var existing = await _db.Shifts.FirstOrDefaultAsync(s => s.UserId == userId && s.Status == ShiftStatus.Open.ToString());
        if (existing != null) throw new InvalidOperationException("User already has an open shift.");

        var shift = new Shift
        {
            UserId = request.UserId,
            OutletId = request.OutletId,
            OpenTime = DateTime.UtcNow,
            OpeningFloat = request.OpeningFloat,
            Status = ShiftStatus.Open.ToString()
        };

        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync();

        return MapToDto(shift);
    }

    public async Task<ShiftDto> CloseShiftAsync(CloseShiftRequest request, int userId)
    {
        var shift = await _db.Shifts.FindAsync(request.ShiftId);
        if (shift == null || shift.Status != ShiftStatus.Open.ToString()) throw new KeyNotFoundException("Open shift not found.");

        shift.CloseTime = DateTime.UtcNow;
        shift.CountedCash = request.CountedCash;
        shift.Status = ShiftStatus.Closed.ToString();
        shift.ClosedBy = (await _db.Users.FindAsync(userId))?.Username;
        shift.Variance = request.CountedCash - (shift.OpeningFloat + shift.PaidIn - shift.PaidOut);

        await _db.SaveChangesAsync();
        return MapToDto(shift);
    }

    public async Task<ShiftDto?> GetOpenShiftAsync(int userId, int outletId)
    {
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.UserId == userId && s.OutletId == outletId && s.Status == ShiftStatus.Open.ToString());
        return shift == null ? null : MapToDto(shift);
    }

    public async Task<List<ShiftDto>> GetShiftsAsync(int? outletId, PagedRequest request, int userId)
    {
        var query = _db.Shifts.Include(s => s.User).Include(s => s.Outlet).Where(s => !s.IsDeleted);
        if (outletId.HasValue) query = query.Where(s => s.OutletId == outletId.Value);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(s => s.OpenTime)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(s => new ShiftDto
            {
                Id = s.Id,
                UserId = s.UserId,
                UserName = s.User == null ? "" : s.User.Username,
                OutletId = s.OutletId,
                OpenTime = s.OpenTime,
                CloseTime = s.CloseTime,
                OpeningFloat = s.OpeningFloat,
                PaidIn = s.PaidIn,
                PaidOut = s.PaidOut,
                CountedCash = s.CountedCash,
                Variance = s.Variance,
                Status = s.Status
            }).ToListAsync();

        return items;
    }

    public async Task<ZReportDto> GenerateZReportAsync(int shiftId, int userId)
    {
        var shift = await _db.Shifts.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == shiftId);
        if (shift == null) throw new KeyNotFoundException("Shift not found.");

        var orders = await _db.Orders.Include(o => o.Payments).Where(o => o.ShiftId == shiftId && o.Status == "Closed").ToListAsync();

        var grossSales = orders.Sum(o => o.TotalAmount);
        var discounts = orders.Sum(o => o.DiscountAmount);
        var taxes = orders.Sum(o => o.TaxAmount);
        var netSales = grossSales - discounts;

        var cashExpected = shift.OpeningFloat + orders.Where(o => o.Payments.Any(p => p.TenderType == "Cash")).Sum(o => o.Payments.Where(p => p.TenderType == "Cash").Sum(p => p.Amount));
        var cashCounted = shift.CountedCash;
        var variance = cashCounted - cashExpected;

        return new ZReportDto
        {
            ShiftId = shift.Id,
            UserName = shift.User?.Username ?? string.Empty,
            OpenTime = shift.OpenTime,
            CloseTime = shift.CloseTime,
            OpeningFloat = shift.OpeningFloat,
            GrossSales = grossSales,
            Discounts = discounts,
            Taxes = taxes,
            NetSales = netSales,
            CashExpected = cashExpected,
            CashCounted = cashCounted,
            Variance = variance,
            OrderCount = orders.Count
        };
    }

    private static ShiftDto MapToDto(Shift s)
    {
        return new ShiftDto
        {
            Id = s.Id,
            UserId = s.UserId,
            UserName = s.User?.Username ?? string.Empty,
            OutletId = s.OutletId,
            OpenTime = s.OpenTime,
            CloseTime = s.CloseTime,
            OpeningFloat = s.OpeningFloat,
            PaidIn = s.PaidIn,
            PaidOut = s.PaidOut,
            CountedCash = s.CountedCash,
            Variance = s.Variance,
            Status = s.Status
        };
    }
}

public class ReportService : IReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db) => _db = db;

    public async Task<List<SalesReportDto>> GetSalesReportAsync(SalesReportRequest request, int userId)
    {
        var query = _db.Orders.Include(o => o.Payments).Where(o => o.Status == "Closed" && !o.IsDeleted);

        if (request.OutletId.HasValue) query = query.Where(o => o.OutletId == request.OutletId.Value);
        if (request.FromDate.HasValue) query = query.Where(o => o.CreatedAt >= request.FromDate.Value);
        if (request.ToDate.HasValue) query = query.Where(o => o.CreatedAt <= request.ToDate.Value);

        var orders = await query.ToListAsync();

        return orders.GroupBy(o =>
        {
            return request.GroupBy switch
            {
                "day" => o.CreatedAt.ToString("yyyy-MM-dd"),
                "employee" => o.User == null ? "Unknown" : o.User.Username,
                "tender" => "Mixed",
                _ => o.OutletId.ToString()
            };
        }).Select(g => new SalesReportDto
        {
            GroupKey = g.Key,
            OrderCount = g.Count(),
            GrossSales = g.Sum(o => o.TotalAmount),
            Discounts = g.Sum(o => o.DiscountAmount),
            Taxes = g.Sum(o => o.TaxAmount),
            NetSales = g.Sum(o => o.TotalAmount - o.DiscountAmount),
            Cash = g.Sum(o => o.Payments.Where(p => p.TenderType == "Cash").Sum(p => p.Amount)),
            Card = g.Sum(o => o.Payments.Where(p => p.TenderType == "Card").Sum(p => p.Amount)),
            DigitalWallet = g.Sum(o => o.Payments.Where(p => p.TenderType == "DigitalWallet").Sum(p => p.Amount))
        }).ToList();
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(int? outletId, int userId)
    {
        var today = DateTime.UtcNow.Date;
        var query = _db.Orders.Where(o => !o.IsDeleted);
        if (outletId.HasValue) query = query.Where(o => o.OutletId == outletId.Value);

        var todayOrders = await query.CountAsync(o => o.CreatedAt.Date == today);
        var todaySales = await query.Where(o => o.CreatedAt.Date == today && o.Status == "Closed").SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
        var todayCash = await _db.Payments.Include(p => p.Order).Where(p => p.TenderType == "Cash" && p.Order.CreatedAt.Date == today).SumAsync(p => (decimal?)p.Amount) ?? 0;

        var openTables = await _db.Tables.CountAsync(t => t.Status == "Occupied" && (!outletId.HasValue || t.OutletId == outletId.Value));
        var pendingTickets = await _db.KitchenTickets.CountAsync(kt => kt.Status == "New" || kt.Status == "InProgress");
        var lowStock = (await _db.Products.Include(p => p.InventoryLedgers).Where(p => !p.IsDeleted).ToListAsync())
            .Count(p => InventoryService.CalculateStock(p) <= 5);

        return new DashboardSummaryDto
        {
            TodaySales = todaySales,
            TodayOrders = todayOrders,
            TodayCash = todayCash,
            OpenTables = openTables,
            PendingKitchenTickets = pendingTickets,
            LowStockItems = lowStock,
            RecentSales = new List<SalesReportDto>()
        };
    }

    public async Task<List<AuditLogDto>> GetAuditLogsAsync(int? outletId, PagedRequest request, int userId)
    {
        var query = _db.AuditLogs.Include(a => a.Actor).Where(a => !a.IsDeleted).OrderByDescending(a => a.CreatedAt);
        var total = await query.CountAsync();
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                ActorName = a.Actor == null ? "System" : a.Actor.Username,
                Action = a.Action,
                Entity = a.Entity,
                EntityId = a.EntityId,
                Reason = a.Reason,
                CreatedAt = a.CreatedAt
            }).ToListAsync();
        return items;
    }

    public Task<byte[]> ExportReportAsync(string reportType, SalesReportRequest request, int userId)
    {
        // CSV export stub
        var csv = "ReportType,Data\n" + reportType + ",exported\n";
        return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(csv));
    }
}
