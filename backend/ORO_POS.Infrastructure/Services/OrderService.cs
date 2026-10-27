using Microsoft.EntityFrameworkCore;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Entities;
using ORO_POS.Core.Enums;
using ORO_POS.Core.Interfaces;
using ORO_POS.Infrastructure.Data;
using System.Text.Json;

namespace ORO_POS.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly IKitchenService _kitchen;

    public OrderService(AppDbContext db, IInventoryService inventory, IKitchenService kitchen)
    {
        _db = db;
        _inventory = inventory;
        _kitchen = kitchen;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, int userId)
    {
        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
        var user = await _db.Users.Include(u => u.Outlet).FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user == null) throw new UnauthorizedAccessException("User not found or inactive.");
        if (request.OutletId != user.OutletId && user.Role?.Name != "Owner")
            throw new UnauthorizedAccessException("Cannot create order for another outlet.");

        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.UserId == userId && s.OutletId == request.OutletId && s.Status == ShiftStatus.Open.ToString());
        if (shift == null) throw new InvalidOperationException("No open shift for this user. Open a shift first.");

        if (request.TableId.HasValue)
        {
            var table = await _db.Tables.FindAsync(request.TableId.Value);
            if (table == null || table.OutletId != request.OutletId) throw new InvalidOperationException("Table does not belong to this outlet.");
            if (await _db.Orders.AnyAsync(o => o.TableId == table.Id && o.Status == "Open"))
                throw new InvalidOperationException("This table already has an open order. Refresh and open the existing order.");
        }

        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

        var order = new Order
        {
            OutletId = request.OutletId,
            TableId = request.TableId,
            UserId = userId,
            ShiftId = shift.Id,
            ServiceType = request.ServiceType,
            OrderNumber = orderNumber,
            Status = OrderStatus.Open.ToString(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = user.Username,
            Notes = request.Notes,
            IsDeleted = false
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        if (request.TableId.HasValue)
        {
            var table = await _db.Tables.FindAsync(request.TableId.Value);
            if (table != null) table.Status = "Occupied";
        }

        foreach (var itemReq in request.Items)
        {
            await AddItemToOrderAsync(order, itemReq, userId, save: false);
        }

        await RecalculateOrderTotalsAsync(order.Id);
        await _db.SaveChangesAsync();

        // Route to kitchen
        await RouteOrderToKitchenAsync(order.Id, userId);
        if (tx != null) await tx.CommitAsync();

        return await GetOrderByIdAsync(order.Id, userId);
    }

    public async Task<OrderDto?> GetOrderByIdAsync(int orderId, int userId)
    {
        var order = await _db.Orders
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .Include(o => o.Payments)
            .Include(o => o.Table)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);

        if (order == null) return null;

        var user = await _db.Users.FindAsync(userId);
        if (user != null && user.Role?.Name != "Owner" && order.OutletId != user.OutletId)
            throw new UnauthorizedAccessException("Access denied to this order.");

        // Quote legacy open orders using today's application policy before the
        // cashier confirms payment. This read does not save or rewrite closed sales.
        if (order.Status == "Open") CalculateTotals(order);
        return MapToOrderDto(order);
    }

    public async Task<OrderDto?> GetOpenOrderForTableAsync(int tableId, int userId)
    {
        var table = await _db.Tables.FindAsync(tableId);
        if (table == null) throw new KeyNotFoundException("Table not found.");
        if (!await ValidateOutletAccess(userId, table.OutletId))
            throw new UnauthorizedAccessException("Access denied to outlet.");
        var id = await _db.Orders.Where(o => o.TableId == tableId && o.Status == "Open")
            .OrderByDescending(o => o.CreatedAt).Select(o => (int?)o.Id).FirstOrDefaultAsync();
        return id.HasValue ? await GetOrderByIdAsync(id.Value, userId) : null;
    }

    public async Task<PagedResult<OrderDto>> GetOrdersAsync(PagedRequest request, int? outletId, int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) throw new UnauthorizedAccessException("User not found.");

        var query = _db.Orders
            .Include(o => o.Table)
            .Include(o => o.User)
            .Include(o => o.Payments)
            .Where(o => !o.IsDeleted);

        if (outletId.HasValue)
        {
            if (!await ValidateOutletAccess(userId, outletId.Value))
                throw new UnauthorizedAccessException("Access denied to outlet.");
            query = query.Where(o => o.OutletId == outletId.Value);
        }
        else if (user.Role?.Name != "Owner" && user.OutletId.HasValue)
        {
            query = query.Where(o => o.OutletId == user.OutletId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.ToLower();
            query = query.Where(o => o.OrderNumber.ToLower().Contains(s) || (o.Table != null && o.Table.TableNumber.ToLower().Contains(s)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(o => MapToOrderDto(o))
            .ToListAsync();

        return new PagedResult<OrderDto> { Items = items, TotalCount = total, Page = request.Page, PageSize = request.PageSize };
    }

    private async Task<bool> ValidateOutletAccess(int userId, int outletId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return false;
        if (user.Role != null && user.Role.Name == "Owner") return true;
        return user.OutletId == outletId;
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(int orderId, string status, int userId, string? reason = null)
    {
        if (status != "Open") throw new InvalidOperationException("Use the dedicated payment, void or refund action to change financial status.");
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null || order.IsDeleted) throw new KeyNotFoundException("Order not found.");
        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = (await _db.Users.FindAsync(userId))?.Username;

        if (status == "Closed" || status == "Voided")
        {
            order.ClosedAt = DateTime.UtcNow;
            order.ClosedBy = (await _db.Users.FindAsync(userId))?.Username;
            if (order.TableId.HasValue)
            {
                var table = await _db.Tables.FindAsync(order.TableId.Value);
                if (table != null) table.Status = status == "Closed" ? "Free" : "Closed";
            }
        }

        await _db.SaveChangesAsync();
        return await GetOrderByIdAsync(orderId, userId) ?? throw new InvalidOperationException("Order not found after update.");
    }

    public async Task<OrderDto> VoidOrderAsync(int orderId, VoidOrderRequest request, int userId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null || order.IsDeleted) throw new KeyNotFoundException("Order not found.");
        if (order.Status != "Open") throw new InvalidOperationException("Only open orders can be voided. Use refund for a settled sale.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("A void reason is required.");
        if (await _db.Payments.AnyAsync(p => p.OrderId == orderId && p.Status == "Captured"))
            throw new InvalidOperationException("An order with captured payments cannot be voided.");

        // Check permission
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) throw new UnauthorizedAccessException("User not found.");

        bool canVoid = user.Role?.Name == "Owner" || (user.OutletId == order.OutletId &&
            (JsonSerializer.Deserialize<string[]>(user.Role?.Permissions ?? "[]") ?? []).Contains("VoidOrder"));

        if (!canVoid) throw new UnauthorizedAccessException("You do not have permission to void this order.");
        var approver = await RequireManagerApprovalAsync(userId, order.OutletId, request.ApproverUsername, request.ApproverPassword);

        order.Status = OrderStatus.Voided.ToString();
        order.ClosedAt = DateTime.UtcNow;
        order.ClosedBy = user.Username;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = user.Username;

        var tickets = await _db.KitchenTickets.Where(t => t.OrderId == orderId && t.Status != "Served").ToListAsync();
        foreach (var ticket in tickets) { ticket.Status = "Cancelled"; ticket.UpdatedAt = DateTime.UtcNow; }

        if (order.TableId.HasValue)
        {
            var table = await _db.Tables.FindAsync(order.TableId.Value);
            if (table != null) table.Status = "Free";
        }

        await _db.SaveChangesAsync();

        // Audit log
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = userId,
            Action = "VoidOrder",
            Entity = "Order",
            EntityId = orderId,
            Reason = $"{request.Reason} (approved by {approver})",
            AfterValues = JsonSerializer.Serialize(new { orderId, status = "Voided" }),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return await GetOrderByIdAsync(orderId, userId);
    }

    public async Task<OrderDto> ApplyDiscountAsync(int orderId, ApplyDiscountRequest request, int userId)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("A discount reason is required.");
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null || order.IsDeleted) throw new KeyNotFoundException("Order not found.");
        if (order.Status != "Open") throw new InvalidOperationException("Cannot discount a closed order.");

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) throw new UnauthorizedAccessException("User not found.");

        // ApplyDiscount permission: Owner/StoreManager always, otherwise role permission string.
        if (user.Role?.Name != "Owner" && user.OutletId != order.OutletId)
            throw new UnauthorizedAccessException("Access denied to this order.");
        bool canDiscount = user.Role?.Name == "Owner" || user.Role?.Name == "StoreManager" ||
                           (JsonSerializer.Deserialize<string[]>(user.Role?.Permissions ?? "[]")?.Contains(nameof(Permission.ApplyDiscount)) ?? false);
        if (!canDiscount) throw new UnauthorizedAccessException("You do not have permission to apply a discount.");
        var approver = await RequireManagerApprovalAsync(userId, order.OutletId, request.ApproverUsername, request.ApproverPassword);

        // Resolve discount amount: percentage (of subtotal) takes precedence when provided.
        decimal discount;
        if (request.Percent.HasValue)
        {
            var pct = request.Percent.Value;
            if (pct < 0 || pct > 100) throw new InvalidOperationException("Discount percentage must be between 0 and 100.");
            discount = Math.Round(order.Subtotal * pct / 100m, 2);
        }
        else
        {
            discount = Math.Round(request.Amount, 2);
        }
        if (discount < 0) throw new InvalidOperationException("Discount cannot be negative.");
        if (discount > order.Subtotal) throw new InvalidOperationException("Discount cannot exceed the order subtotal.");

        var before = new { order.DiscountAmount, order.TotalAmount };
        order.DiscountAmount = discount;
        await RecalculateOrderTotalsAsync(orderId);
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = user.Username;
        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = userId,
            Action = "ApplyDiscount",
            Entity = "Order",
            EntityId = orderId,
            Reason = $"{request.Reason} (approved by {approver})",
            BeforeValues = JsonSerializer.Serialize(before),
            AfterValues = JsonSerializer.Serialize(new { orderId, discount, order.TotalAmount }),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return await GetOrderByIdAsync(orderId, userId);
    }

    public async Task<OrderDto> AddOrderItemAsync(int orderId, OrderItemRequest request, int userId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null || order.IsDeleted) throw new KeyNotFoundException("Order not found.");
        if (order.Status != "Open") throw new InvalidOperationException("Cannot add items to a closed order.");

        await AddItemToOrderAsync(order, request, userId, save: false);
        await RecalculateOrderTotalsAsync(orderId);
        await _db.SaveChangesAsync();

        // Route to kitchen
        await RouteOrderToKitchenAsync(orderId, userId);

        return await GetOrderByIdAsync(orderId, userId);
    }

    public async Task<OrderDto> RemoveOrderItemAsync(int orderId, int orderItemId, int userId)
    {
        var item = await _db.OrderItems.FindAsync(orderItemId);
        if (item == null || item.OrderId != orderId) throw new KeyNotFoundException("Order item not found.");
        if (item.Status != "New" && item.Status != "Sent") throw new InvalidOperationException("Cannot remove an item that is already being prepared.");

        _db.OrderItems.Remove(item);
        await _db.SaveChangesAsync();
        await RecalculateOrderTotalsAsync(orderId);
        await _db.SaveChangesAsync();
        return await GetOrderByIdAsync(orderId, userId);
    }

    public async Task<OrderDto> ProcessPaymentAsync(int orderId, ProcessPaymentRequest request, int userId)
    {
        // A serializable transaction prevents concurrent requests from capturing the
        // same balance twice. A failed transaction must be retried after refreshing.
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
            : null;
        var order = await _db.Orders.Include(o => o.Payments).Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null || order.IsDeleted) throw new KeyNotFoundException("Order not found.");
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user == null || (user.Role?.Name != "Owner" && user.OutletId != order.OutletId))
            throw new UnauthorizedAccessException("Access denied to this order.");
        if (order.Status != "Open") throw new InvalidOperationException("Only open orders can receive payment.");
        if (order.OrderItems.Count == 0) throw new InvalidOperationException("Cannot pay an empty order.");
        if (request.TenderType != "Cash" && request.TenderType != "Card" && request.TenderType != "UPI")
            throw new InvalidOperationException("Unsupported tender type.");
        if (request.Amount < 0 || decimal.Round(request.Amount, 2) != request.Amount)
            throw new InvalidOperationException("Payment cannot be negative and must have at most two decimal places.");

        await RecalculateOrderTotalsAsync(orderId);
        var totalPaid = order.Payments.Where(p => p.Status == "Captured").Sum(p => p.Amount);
        var balance = order.TotalAmount - totalPaid;
        if (request.Amount == 0 && order.TotalAmount > 0) throw new InvalidOperationException("Payment must be positive for a non-zero bill.");
        if (balance <= 0 && !(order.TotalAmount == 0 && order.Payments.Count == 0)) throw new InvalidOperationException("Order has no remaining balance.");
        if (request.TenderType != "Cash" && request.Amount > balance)
            throw new InvalidOperationException("Non-cash payment cannot exceed the remaining balance.");
        // Cash tendered can exceed the balance; change returned is not revenue.
        var captured = Math.Min(request.Amount, balance);

        var payment = new Payment
        {
            OrderId = orderId,
            TenderType = request.TenderType,
            Amount = captured,
            Provider = request.Provider,
            Reference = request.Reference,
            Status = "Captured",
            ProcessedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = (await _db.Users.FindAsync(userId))?.Username
        };

        _db.Payments.Add(payment);

        if (totalPaid + captured >= order.TotalAmount)
        {
            order.Status = "Closed";
            order.ClosedAt = DateTime.UtcNow;
            order.ClosedBy = (await _db.Users.FindAsync(userId))?.Username;
            if (order.TableId.HasValue)
            {
                var table = await _db.Tables.FindAsync(order.TableId.Value);
                if (table != null) table.Status = "Free";
            }
            // Post actual product/recipe consumption exactly once, on settlement.
            foreach (var item in order.OrderItems)
            {
                var recipes = await _db.Recipes.Where(r => r.ProductId == item.ProductId).ToListAsync();
                if (recipes.Count == 0)
                    _db.InventoryLedgers.Add(new InventoryLedger { ProductId = item.ProductId,
                        MovementType = "Sale", Quantity = item.Quantity, ActorId = userId, Source = $"Order_{orderId}" });
                else
                    foreach (var recipe in recipes)
                        _db.InventoryLedgers.Add(new InventoryLedger { ProductId = recipe.IngredientId,
                            MovementType = "Sale", Quantity = recipe.Quantity * item.Quantity,
                            ActorId = userId, Source = $"Order_{orderId}" });
            }
        }

        await _db.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
        return await GetOrderByIdAsync(orderId, userId);
    }

    public async Task<OrderDto> RefundAsync(int orderId, RefundRequest request, int userId)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.Reference))
            throw new InvalidOperationException("Refund reason and manual/bank refund reference are required.");
        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
        var order = await _db.Orders.Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException("Order not found.");
        var actor = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId && u.IsActive)
            ?? throw new UnauthorizedAccessException("User not found.");
        var perms = JsonSerializer.Deserialize<string[]>(actor.Role?.Permissions ?? "[]") ?? [];
        if (actor.Role?.Name != "Owner" && (actor.OutletId != order.OutletId || !perms.Contains("RefundOrder")))
            throw new UnauthorizedAccessException("Refund permission required for this outlet.");
        var approver = await RequireManagerApprovalAsync(userId, order.OutletId, request.ApproverUsername, request.ApproverPassword);
        if (order.Status != "Closed") throw new InvalidOperationException("Only settled orders can be refunded, once.");
        var captured = order.Payments.Where(p => p.Status == "Captured").ToList();
        var cash = captured.Where(p => p.TenderType == "Cash").Sum(p => p.Amount);
        if (cash > 0)
        {
            var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.UserId == userId && s.OutletId == order.OutletId && s.Status == "Open")
                ?? throw new InvalidOperationException("Open your shift before recording a cash refund.");
            shift.PaidOut += cash;
        }
        foreach (var group in captured.GroupBy(p => p.TenderType))
            _db.Payments.Add(new Payment { OrderId = orderId, TenderType = group.Key, Amount = -group.Sum(p => p.Amount),
                Status = "Refunded", Reference = request.Reference.Trim(), ProcessedAt = DateTime.UtcNow, CreatedBy = actor.Username });
        if (request.Restock)
        {
            var saleEntries = await _db.InventoryLedgers.Where(l => l.Source == $"Order_{orderId}" && l.MovementType == "Sale").ToListAsync();
            foreach (var ledger in saleEntries)
                _db.InventoryLedgers.Add(new InventoryLedger { ProductId = ledger.ProductId, MovementType = "Receipt", Quantity = ledger.Quantity,
                    ActorId = userId, Source = $"Refund_{orderId}" });
        }
        order.Status = "Refunded";
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = actor.Username;
        _db.AuditLogs.Add(new AuditLog { ActorId = userId, Action = "RefundOrder", Entity = "Order", EntityId = orderId,
            Reason = $"{request.Reason.Trim()} (approved by {approver})", AfterValues = JsonSerializer.Serialize(new { request.Reference, request.Restock, Amount = captured.Sum(p => p.Amount) }) });
        await _db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return (await GetOrderByIdAsync(orderId, userId))!;
    }

    public async Task<OrderDto> TransferTableAsync(int orderId, int newTableId, int userId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null || order.IsDeleted) throw new KeyNotFoundException("Order not found.");
        if (order.Status != "Open") throw new InvalidOperationException("Only open orders can be transferred.");
        var destination = await _db.Tables.FindAsync(newTableId);
        if (destination == null || destination.OutletId != order.OutletId) throw new InvalidOperationException("Destination table must be in the same outlet.");
        if (await _db.Orders.AnyAsync(o => o.TableId == newTableId && o.Status == "Open" && o.Id != orderId))
            throw new InvalidOperationException("Destination table is occupied. Use merge instead.");

        var oldTableId = order.TableId;
        order.TableId = newTableId;
        order.UpdatedAt = DateTime.UtcNow;

        if (oldTableId.HasValue)
        {
            var oldTable = await _db.Tables.FindAsync(oldTableId.Value);
            if (oldTable != null && !oldTable.TableNumber.Equals(await GetTableNumberAsync(newTableId))) oldTable.Status = "Free";
        }

        var newTable = await _db.Tables.FindAsync(newTableId);
        if (newTable != null) newTable.Status = "Occupied";

        await _db.SaveChangesAsync();
        return await GetOrderByIdAsync(orderId, userId);
    }

    public async Task<OrderDto> MergeOrdersAsync(int sourceOrderId, int targetOrderId, int userId)
    {
        var source = await _db.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == sourceOrderId && !o.IsDeleted);
        var target = await _db.Orders.FindAsync(targetOrderId);
        if (source == null || target == null) throw new KeyNotFoundException("Order not found.");
        if (source.Id == target.Id || source.Status != "Open" || target.Status != "Open" || source.OutletId != target.OutletId)
            throw new InvalidOperationException("Merge requires two different open orders in the same outlet.");
        if (source.DiscountAmount != 0 || target.DiscountAmount != 0 || await _db.Payments.AnyAsync(p => p.OrderId == sourceOrderId || p.OrderId == targetOrderId))
            throw new InvalidOperationException("Remove discounts and settle existing payments before merging.");

        foreach (var item in source.OrderItems)
        {
            item.OrderId = targetOrderId;
        }

        source.Status = "Merged";
        source.Subtotal = source.TaxAmount = source.TotalAmount = 0;
        source.ClosedAt = DateTime.UtcNow;

        await RecalculateOrderTotalsAsync(targetOrderId);
        await _db.SaveChangesAsync();

        if (source.TableId.HasValue)
        {
            var table = await _db.Tables.FindAsync(source.TableId.Value);
            if (table != null) table.Status = "Free";
        }
        foreach (var ticket in await _db.KitchenTickets.Where(t => t.OrderId == sourceOrderId && t.Status != "Served").ToListAsync()) ticket.Status = "Cancelled";
        await _db.SaveChangesAsync();
        await RouteOrderToKitchenAsync(targetOrderId, userId);

        return await GetOrderByIdAsync(targetOrderId, userId);
    }

    public async Task<OrderDto> SplitOrderAsync(int orderId, List<int> orderItemIds, int newTableId, int userId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null || order.IsDeleted) throw new KeyNotFoundException("Order not found.");
        if (order.Status != "Open" || order.DiscountAmount != 0 || await _db.Payments.AnyAsync(p => p.OrderId == orderId))
            throw new InvalidOperationException("Split requires an open order without discounts or payments.");
        var destination = await _db.Tables.FindAsync(newTableId);
        if (destination == null || destination.OutletId != order.OutletId || await _db.Orders.AnyAsync(o => o.TableId == newTableId && o.Status == "Open"))
            throw new InvalidOperationException("Select a free table in the same outlet.");
        var validIds = await _db.OrderItems.Where(i => i.OrderId == orderId).Select(i => i.Id).ToListAsync();
        if (orderItemIds.Count == 0 || orderItemIds.Distinct().Count() != orderItemIds.Count || orderItemIds.Except(validIds).Any() || orderItemIds.Count >= validIds.Count)
            throw new InvalidOperationException("Choose a non-empty subset of this order's items. Leave at least one item on the original bill.");

        var newOrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

        var newOrder = new Order
        {
            OutletId = order.OutletId,
            TableId = newTableId,
            UserId = order.UserId,
            ShiftId = order.ShiftId,
            ServiceType = order.ServiceType,
            OrderNumber = newOrderNumber,
            Status = OrderStatus.Open.ToString(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = order.CreatedBy,
            Notes = order.Notes
        };

        _db.Orders.Add(newOrder);
        await _db.SaveChangesAsync();

        var itemsToMove = await _db.OrderItems.Where(oi => orderItemIds.Contains(oi.Id) && oi.OrderId == orderId).ToListAsync();
        foreach (var item in itemsToMove)
        {
            item.OrderId = newOrder.Id;
        }
        destination.Status = "Occupied";
        await _db.SaveChangesAsync();

        await RecalculateOrderTotalsAsync(orderId);
        await RecalculateOrderTotalsAsync(newOrder.Id);
        await _db.SaveChangesAsync();
        await RouteOrderToKitchenAsync(newOrder.Id, userId);

        return await GetOrderByIdAsync(newOrder.Id, userId);
    }

    private async Task<string> RequireManagerApprovalAsync(int userId, int outletId, string? username, string? password)
    {
        var actor = await _db.Users.Include(u => u.Role).FirstAsync(u => u.Id == userId && u.IsActive);
        static bool CanApprove(User user) => user.Role?.Name == "Owner" || (JsonSerializer.Deserialize<string[]>(user.Role?.Permissions ?? "[]") ?? []).Contains("ApproveManagerAction");
        if (CanApprove(actor)) return actor.Username;
        var approver = string.IsNullOrWhiteSpace(username) ? null : await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        if (approver == null || !CanApprove(approver) || (approver.Role?.Name != "Owner" && approver.OutletId != outletId)
            || string.IsNullOrEmpty(password) || !BCrypt.Net.BCrypt.Verify(password, approver.PasswordHash))
            throw new UnauthorizedAccessException("Approval from an active manager in this outlet is required.");
        return approver.Username;
    }

    private async Task AddItemToOrderAsync(Order order, OrderItemRequest request, int userId, bool save = true)
    {
        var product = await _db.Products.FindAsync(request.ProductId);
        if (product == null || product.IsDeleted) throw new KeyNotFoundException("Product not found.");
        if (!product.IsAvailable) throw new InvalidOperationException("Product is not available.");
        if (product.OutletId != order.OutletId) throw new InvalidOperationException("Product does not belong to this order's outlet.");
        if (request.Quantity < 1 || request.Quantity > 999) throw new InvalidOperationException("Quantity must be between 1 and 999.");

        var modifiers = await _db.ModifierOptions.Where(mo => request.ModifierOptionIds.Contains(mo.Id)).ToListAsync();
        var groups = await _db.ProductModifierGroups.Include(g => g.ModifierGroup).Where(g => g.ProductId == product.Id).ToListAsync();
        if (modifiers.Count != request.ModifierOptionIds.Distinct().Count() || modifiers.Any(m => !groups.Any(g => g.ModifierGroupId == m.ModifierGroupId)))
            throw new InvalidOperationException("Invalid product modifiers.");
        foreach (var g in groups)
        {
            var count = modifiers.Count(m => m.ModifierGroupId == g.ModifierGroupId);
            if (count < Math.Max(g.ModifierGroup.MinSelections, g.ModifierGroup.IsRequired ? 1 : 0) || count > g.ModifierGroup.MaxSelections)
                throw new InvalidOperationException($"Check selections for {g.ModifierGroup.Name}.");
        }
        var modifiersJson = System.Text.Json.JsonSerializer.Serialize(modifiers.Select(m => new { m.Id, m.Name, m.PriceImpact }));

        var modifierPriceImpact = modifiers.Sum(m => m.PriceImpact);
        var unitPrice = product.Price + modifierPriceImpact;
        var lineTotal = unitPrice * request.Quantity;

        var orderItem = new OrderItem
        {
            OrderId = order.Id,
            ProductId = request.ProductId,
            ProductName = product.Name,
            Quantity = request.Quantity,
            UnitPrice = unitPrice,
            Modifiers = modifiersJson,
            Notes = request.Notes,
            KitchenStation = DetermineKitchenStation(product),
            Status = "New",
            LineTotal = lineTotal,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = (await _db.Users.FindAsync(userId))?.Username
        };

        _db.OrderItems.Add(orderItem);
        if (save) await _db.SaveChangesAsync();
    }

    private async Task RecalculateOrderTotalsAsync(int orderId)
    {
        var order = await _db.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return;

        CalculateTotals(order);
        order.UpdatedAt = DateTime.UtcNow;
    }

    private static void CalculateTotals(Order order)
    {
        order.Subtotal = order.OrderItems.Sum(i => i.LineTotal);
        order.DiscountAmount = Math.Clamp(order.DiscountAmount, 0m, order.Subtotal);
        // Match the platform's existing 5% GST policy, rounded to paise on the server.
        order.TaxAmount = Math.Round((order.Subtotal - order.DiscountAmount) * 0.05m, 2, MidpointRounding.AwayFromZero);
        order.TotalAmount = order.Subtotal - order.DiscountAmount + order.TaxAmount;
    }

    private async Task RouteOrderToKitchenAsync(int orderId, int userId)
    {
        var order = await _db.Orders.Include(o => o.OrderItems).ThenInclude(oi => oi.Product).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return;

        var itemsByStation = order.OrderItems.GroupBy(i => i.KitchenStation ?? "General");
        foreach (var group in itemsByStation)
        {
            if (!group.Any(i => i.Status != "Served")) continue;
            if (await _db.KitchenTickets.AnyAsync(t => t.OrderId == orderId && t.StationName == group.Key && t.Status != "Served" && t.Status != "Cancelled")) continue;
            var ticket = new KitchenTicket
            {
                OrderId = orderId,
                StationName = group.Key,
                Status = KitchenTicketStatus.New.ToString(),
                Priority = 0,
                CreatedAt = DateTime.UtcNow
            };
            _db.KitchenTickets.Add(ticket);
        }
        await _db.SaveChangesAsync();
    }

    private static string DetermineKitchenStation(Product product)
    {
        var cat = (product.Category?.Name ?? "").ToLower();
        if (cat.Contains("bar") || cat.Contains("beverage")) return "Bar";
        if (cat.Contains("kitchen") || cat.Contains("hot") || cat.Contains("food")) return "Kitchen";
        return "General";
    }

    private async Task<string> GetTableNumberAsync(int tableId)
    {
        var table = await _db.Tables.FindAsync(tableId);
        return table?.TableNumber ?? string.Empty;
    }

    private static OrderDto MapToOrderDto(Order o)
    {
        return new OrderDto
        {
            Id = o.Id,
            OutletId = o.OutletId,
            TableId = o.TableId,
            TableNumber = o.Table?.TableNumber,
            UserId = o.UserId,
            UserName = o.User?.Username,
            ServiceType = o.ServiceType,
            OrderNumber = o.OrderNumber,
            Status = o.Status,
            Subtotal = o.Subtotal,
            DiscountAmount = o.DiscountAmount,
            TaxAmount = o.TaxAmount,
            TotalAmount = o.TotalAmount,
            Notes = o.Notes,
            CreatedAt = o.CreatedAt,
            ClosedAt = o.ClosedAt,
            OrderItems = o.OrderItems.Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                OrderId = oi.OrderId,
                ProductId = oi.ProductId,
                ProductName = oi.ProductName,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                DiscountAmount = oi.DiscountAmount,
                Modifiers = oi.Modifiers,
                Notes = oi.Notes,
                KitchenStation = oi.KitchenStation,
                Status = oi.Status,
                LineTotal = oi.LineTotal
            }).ToList(),
            Payments = o.Payments.Select(p => new PaymentDto
            {
                Id = p.Id,
                OrderId = p.OrderId,
                TenderType = p.TenderType,
                Amount = p.Amount,
                Provider = p.Provider,
                Reference = p.Reference,
                Status = p.Status,
                ProcessedAt = p.ProcessedAt
            }).ToList()
        };
    }
}
