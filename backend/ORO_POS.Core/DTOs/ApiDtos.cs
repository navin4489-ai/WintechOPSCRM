namespace ORO_POS.Core.DTOs;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public string[]? Errors { get; set; }
    public int? TotalCount { get; set; }
}

public class PagedRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

// Auth DTOs
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public string? StationType { get; set; }
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = new();
}

// User DTOs
public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? RoleName { get; set; }
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public int? OutletId { get; set; }
    public string? OutletName { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int? RoleId { get; set; }
    public int? OutletId { get; set; }
}

public class UpdateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Null/empty keeps the current password.</summary>
    public string? Password { get; set; }
    public int? RoleId { get; set; }
    public int? OutletId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class RoleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[] Permissions { get; set; } = Array.Empty<string>();
}

// Outlet DTOs
public class OutletDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string? TaxSetup { get; set; }
    public string? Currency { get; set; } = "USD";
    public bool IsOperating { get; set; }
}

// Category DTOs
public class CategoryDto
{
    public int Id { get; set; }
    public int OutletId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class CreateCategoryRequest
{
    public int OutletId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
}

// Product DTOs
public class ProductDto
{
    public int Id { get; set; }
    public int OutletId { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string TaxClass { get; set; } = "Standard";
    public bool IsAvailable { get; set; }
    public int SortOrder { get; set; }
    public string? ImageUrl { get; set; }
    public List<ModifierGroupDto> ModifierGroups { get; set; } = new();
}

public class CreateProductRequest
{
    public int OutletId { get; set; }
    public int? CategoryId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string TaxClass { get; set; } = "Standard";
    public bool IsAvailable { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public List<int> ModifierGroupIds { get; set; } = new();
}

// Modifier DTOs
public class ModifierOptionDto
{
    public int Id { get; set; }
    public int ModifierGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceImpact { get; set; }
}

public class ModifierGroupDto
{
    public int Id { get; set; }
    public int OutletId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int MinSelections { get; set; }
    public int MaxSelections { get; set; }
    public List<ModifierOptionDto> Options { get; set; } = new();
}

public class CreateModifierGroupRequest
{
    public int OutletId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int MinSelections { get; set; }
    public int MaxSelections { get; set; }
    public List<ModifierOptionDto> Options { get; set; } = new();
}

// Table DTOs
public class TableDto
{
    public int Id { get; set; }
    public int OutletId { get; set; }
    public int? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int? FloorPlanX { get; set; }
    public int? FloorPlanY { get; set; }
    public bool HasOpenOrder { get; set; }
}

public class CreateTableRequest
{
    public int OutletId { get; set; }
    public int? ZoneId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public int Capacity { get; set; } = 4;
    public int? FloorPlanX { get; set; }
    public int? FloorPlanY { get; set; }
}

// Zone DTOs
public class ZoneDto
{
    public int Id { get; set; }
    public int OutletId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FloorPlanData { get; set; }
}

public class CreateZoneRequest
{
    public int OutletId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FloorPlanData { get; set; }
}

// Order DTOs
public class OrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
    public List<int> ModifierOptionIds { get; set; } = new();
}

public class CreateOrderRequest
{
    public Guid? ClientRequestId { get; set; }
    public int OutletId { get; set; }
    public int? TableId { get; set; }
    public string ServiceType { get; set; } = "DineIn";
    public List<OrderItemRequest> Items { get; set; } = new();
    public string? Notes { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public int OutletId { get; set; }
    public int? TableId { get; set; }
    public string? TableNumber { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string ServiceType { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<OrderItemDto> OrderItems { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? Modifiers { get; set; }
    public string? Notes { get; set; }
    public string KitchenStation { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal LineTotal { get; set; }
}

public class PaymentDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string TenderType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Provider { get; set; }
    public string? Reference { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ProcessedAt { get; set; }
}

public class ProcessPaymentRequest
{
    public int OrderId { get; set; }
    public string TenderType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Provider { get; set; }
    public string? Reference { get; set; }
}

public class VoidOrderRequest
{
    public string? ApproverUsername { get; set; }
    public string? ApproverPassword { get; set; }
    public int OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? ApprovedByUserId { get; set; }
}

public class ApplyDiscountRequest
{
    public string? ApproverUsername { get; set; }
    public string? ApproverPassword { get; set; }
    /// <summary>Flat discount amount in currency units. Ignored when <see cref="Percent"/> is set.</summary>
    public decimal Amount { get; set; }
    /// <summary>Optional percentage discount (0-100) applied to the subtotal.</summary>
    public decimal? Percent { get; set; }
    public string? Reason { get; set; }
}

// Kitchen Ticket DTOs
public class KitchenTicketDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? TableNumber { get; set; }
    public int? StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? ServedAt { get; set; }
    public List<KitchenTicketItemDto> Items { get; set; } = new();
}

public class KitchenTicketItemDto
{
    public int OrderItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Modifiers { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class UpdateTicketStatusRequest
{
    public int TicketId { get; set; }
    public string Status { get; set; } = string.Empty;
}

// Inventory DTOs
public class InventoryLedgerDto
{
    public int Id { get; set; }
    public int? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Location { get; set; }
    public string? Batch { get; set; }
    public string? Source { get; set; }
    public int? ActorId { get; set; }
    public string? ActorName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RecordInventoryRequest
{
    public int? ProductId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Location { get; set; }
    public string? Batch { get; set; }
    public string? Source { get; set; }
}

public class ProductStockDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal? LowStockThreshold { get; set; }
    public bool IsLowStock { get; set; }
}

// Recipe DTOs
public class RecipeDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class CreateRecipeRequest
{
    public int ProductId { get; set; }
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}

// Shift DTOs
public class ShiftDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int OutletId { get; set; }
    public DateTime OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public decimal OpeningFloat { get; set; }
    public decimal PaidIn { get; set; }
    public decimal PaidOut { get; set; }
    public decimal CountedCash { get; set; }
    public decimal Variance { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class OpenShiftRequest
{
    public int UserId { get; set; }
    public int OutletId { get; set; }
    public decimal OpeningFloat { get; set; } = 0;
}

public class CloseShiftRequest
{
    public int ShiftId { get; set; }
    public decimal CountedCash { get; set; }
    public string? ClosedBy { get; set; }
}

// Report DTOs
public class SalesReportRequest
{
    public int? OutletId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? GroupBy { get; set; } // outlet, day, shift, employee, product, tender
}

public class SalesReportDto
{
    public string GroupKey { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal GrossSales { get; set; }
    public decimal Discounts { get; set; }
    public decimal Taxes { get; set; }
    public decimal Refunds { get; set; }
    public decimal NetSales { get; set; }
    public decimal Cash { get; set; }
    public decimal Card { get; set; }
    public decimal DigitalWallet { get; set; }
}

public class ZReportDto
{
    public int ShiftId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public decimal OpeningFloat { get; set; }
    public decimal GrossSales { get; set; }
    public decimal Discounts { get; set; }
    public decimal Taxes { get; set; }
    public decimal NetSales { get; set; }
    public decimal CashExpected { get; set; }
    public decimal CashCounted { get; set; }
    public decimal Variance { get; set; }
    public int OrderCount { get; set; }
}

public class AuditLogDto
{
    public int Id { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Dashboard DTOs
public class DashboardSummaryDto
{
    public decimal TodaySales { get; set; }
    public int TodayOrders { get; set; }
    public decimal TodayCash { get; set; }
    public int OpenTables { get; set; }
    public int PendingKitchenTickets { get; set; }
    public int LowStockItems { get; set; }
    public List<SalesReportDto> RecentSales { get; set; } = new();
}
