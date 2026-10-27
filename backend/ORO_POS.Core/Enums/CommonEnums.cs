namespace ORO_POS.Core.Enums;

public enum ServiceType
{
    DineIn,
    Takeaway,
    QuickService,
    Retail
}

public enum OrderStatus
{
    Open,
    SentToKitchen,
    InProgress,
    Ready,
    Served,
    Closed,
    Voided,
    Refunded
}

public enum PaymentStatus
{
    Pending,
    Authorized,
    Captured,
    Failed,
    Refunded,
    Voided
}

public enum TenderType
{
    Cash,
    Card,
    DigitalWallet,
    Other
}

public enum KitchenTicketStatus
{
    New,
    Accepted,
    InProgress,
    Ready,
    Served,
    Closed
}

public enum InventoryMovementType
{
    Sale,
    Receipt,
    TransferIn,
    TransferOut,
    Adjustment,
    Count,
    Waste
}

public enum ShiftStatus
{
    Open,
    Closed
}

public enum UserRole
{
    Owner,
    StoreManager,
    Waiter,
    Bartender,
    Cashier,
    KitchenStaff,
    InventoryController,
    Finance
}

public enum Permission
{
    // Order permissions
    CreateOrder,
    EditOrder,
    VoidOrder,
    RefundOrder,
    ApplyDiscount,
    OverridePrice,
    // Table permissions
    TransferTable,
    MergeTable,
    SplitTable,
    // Inventory permissions
    ManageInventory,
    RecordReceipt,
    RecordWaste,
    PhysicalCount,
    // Staff permissions
    ManageUsers,
    ManageRoles,
    ClockInOut,
    ApproveManagerAction,
    // Report permissions
    ViewReports,
    ExportReports,
    // Finance permissions
    CloseShift,
    ViewFinanceDashboard,
    // System
    ManageSettings,
    ManagePeripherals
}
