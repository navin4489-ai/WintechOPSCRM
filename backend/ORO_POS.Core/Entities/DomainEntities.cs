using ORO_POS.Core.Enums;

namespace ORO_POS.Core.Entities;

public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

public class Outlet : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string? TaxSetup { get; set; }
    public bool IsOperating { get; set; } = true;
    public string? Currency { get; set; } = "USD";
    public virtual ICollection<User> Users { get; set; } = new List<User>();
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
    public virtual ICollection<Table> Tables { get; set; } = new List<Table>();
    public virtual ICollection<Zone> Zones { get; set; } = new List<Zone>();
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    public virtual ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public virtual ICollection<InventoryLedger> InventoryLedgers { get; set; } = new List<InventoryLedger>();
}

public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Permissions { get; set; } = "[]"; // JSON array of Permission values
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int? RoleId { get; set; }
    public virtual Role? Role { get; set; }
    public int? OutletId { get; set; }
    public virtual Outlet? Outlet { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public virtual ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public virtual ICollection<InventoryLedger> InventoryMovements { get; set; } = new List<InventoryLedger>();
}

public class Category : BaseEntity
{
    public int OutletId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    public virtual Outlet Outlet { get; set; } = null!;
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product : BaseEntity
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
    public string? ImageUrl { get; set; }
    public virtual Outlet Outlet { get; set; } = null!;
    public virtual Category? Category { get; set; }
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual ICollection<ProductModifierGroup> ModifierGroups { get; set; } = new List<ProductModifierGroup>();
    public virtual ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
    public virtual ICollection<InventoryLedger> InventoryLedgers { get; set; } = new List<InventoryLedger>();
}

public class ModifierGroup : BaseEntity
{
    public int OutletId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = false;
    public int MinSelections { get; set; } = 0;
    public int MaxSelections { get; set; } = 1;
    public virtual Outlet Outlet { get; set; } = null!;
    public virtual ICollection<ModifierOption> Options { get; set; } = new List<ModifierOption>();
    public virtual ICollection<ProductModifierGroup> ProductModifierGroups { get; set; } = new List<ProductModifierGroup>();
}

public class ModifierOption : BaseEntity
{
    public int ModifierGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceImpact { get; set; } = 0;
    public virtual ModifierGroup ModifierGroup { get; set; } = null!;
}

public class ProductModifierGroup : BaseEntity
{
    public int ProductId { get; set; }
    public int ModifierGroupId { get; set; }
    public virtual Product Product { get; set; } = null!;
    public virtual ModifierGroup ModifierGroup { get; set; } = null!;
}

public class Table : BaseEntity
{
    public int OutletId { get; set; }
    public int? ZoneId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Free"; // Free, Occupied, Reserved, Closed
    public int Capacity { get; set; } = 4;
    public int? FloorPlanX { get; set; }
    public int? FloorPlanY { get; set; }
    public virtual Outlet Outlet { get; set; } = null!;
    public virtual Zone? Zone { get; set; }
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class Zone : BaseEntity
{
    public int OutletId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FloorPlanData { get; set; } // JSON for floor plan editor
    public virtual Outlet Outlet { get; set; } = null!;
    public virtual ICollection<Table> Tables { get; set; } = new List<Table>();
}

public class Order : BaseEntity
{
    public int OutletId { get; set; }
    public int? TableId { get; set; }
    public int? UserId { get; set; }
    public int? ShiftId { get; set; }
    public string ServiceType { get; set; } = "DineIn";
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public decimal Subtotal { get; set; } = 0;
    public decimal DiscountAmount { get; set; } = 0;
    public decimal TaxAmount { get; set; } = 0;
    public decimal TotalAmount { get; set; } = 0;
    public string? Notes { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public virtual Outlet Outlet { get; set; } = null!;
    public virtual Table? Table { get; set; }
    public virtual User? User { get; set; }
    public virtual Shift? Shift { get; set; }
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<KitchenTicket> KitchenTickets { get; set; } = new List<KitchenTicket>();
}

public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; } = 0;
    public decimal DiscountAmount { get; set; } = 0;
    public string? Modifiers { get; set; } // JSON array of selected modifier options
    public string? Notes { get; set; }
    public string KitchenStation { get; set; } = string.Empty;
    public string Status { get; set; } = "New"; // New, Sent, InProgress, Ready, Served, Voided
    public decimal LineTotal { get; set; } = 0;
    public virtual Order Order { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}

public class Payment : BaseEntity
{
    public int OrderId { get; set; }
    public string TenderType { get; set; } = "Cash";
    public decimal Amount { get; set; } = 0;
    public string? Provider { get; set; }
    public string? Reference { get; set; } // Tokenized payment reference
    public string Status { get; set; } = "Pending";
    public DateTime? ProcessedAt { get; set; }
    public virtual Order Order { get; set; } = null!;
}

public class KitchenTicket : BaseEntity
{
    public int OrderId { get; set; }
    public int? StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string Status { get; set; } = "New";
    public int Priority { get; set; } = 0;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? ServedAt { get; set; }
    public virtual Order Order { get; set; } = null!;
}

public class Recipe : BaseEntity
{
    public int ProductId { get; set; }
    public int IngredientId { get; set; } // References Product used as ingredient
    public decimal Quantity { get; set; } = 0;
    public string Unit { get; set; } = string.Empty;
    public virtual Product Product { get; set; } = null!;
    public virtual Product Ingredient { get; set; } = null!;
}

public class InventoryLedger : BaseEntity
{
    public int? ProductId { get; set; }
    public string MovementType { get; set; } = "Sale";
    public decimal Quantity { get; set; } = 0;
    public string? Location { get; set; }
    public string? Batch { get; set; }
    public string? Source { get; set; } // SaleId, ReceiptId, etc.
    public int? ActorId { get; set; }
    public virtual Product? Product { get; set; }
    public virtual User? Actor { get; set; }
}

public class Shift : BaseEntity
{
    public int UserId { get; set; }
    public int OutletId { get; set; }
    public DateTime OpenTime { get; set; } = DateTime.UtcNow;
    public DateTime? CloseTime { get; set; }
    public decimal OpeningFloat { get; set; } = 0;
    public decimal PaidIn { get; set; } = 0;
    public decimal PaidOut { get; set; } = 0;
    public decimal CountedCash { get; set; } = 0;
    public decimal Variance { get; set; } = 0;
    public string Status { get; set; } = "Open";
    public string? ClosedBy { get; set; }
    public virtual User User { get; set; } = null!;
    public virtual Outlet Outlet { get; set; } = null!;
}

public class AuditLog : BaseEntity
{
    public int? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string? Reason { get; set; }
    public string? BeforeValues { get; set; } // JSON
    public string? AfterValues { get; set; } // JSON
    public virtual User? Actor { get; set; }
}

public class DeviceConfig : BaseEntity
{
    public string DeviceName { get; set; } = string.Empty;
    public int? OutletId { get; set; }
    public string StationType { get; set; } = string.Empty; // POS, KDS, Waiter, CustomerDisplay
    public string? Config { get; set; } // JSON
    public bool IsActive { get; set; } = true;
    public DateTime? LastSeenAt { get; set; }
    public virtual Outlet? Outlet { get; set; }
}
