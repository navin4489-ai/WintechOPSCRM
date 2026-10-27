using ORO_POS.Core.DTOs;
using ORO_POS.Core.Enums;

namespace ORO_POS.Core.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<bool> ValidatePermissionAsync(int userId, Permission permission);
    Task<bool> ValidateOutletAccessAsync(int userId, int outletId);
}

public interface IUserService
{
    Task<List<UserDto>> GetUsersAsync(int actorId);
    Task<UserDto?> GetUserByIdAsync(int id, int actorId);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, int actorId);
    Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request, int actorId);
    Task<UserDto> DeleteUserAsync(int id, int actorId);
    Task<List<RoleDto>> GetRolesAsync();
}

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, int userId);
    Task<OrderDto?> GetOrderByIdAsync(int orderId, int userId);
    Task<OrderDto?> GetOpenOrderForTableAsync(int tableId, int userId);
    Task<PagedResult<OrderDto>> GetOrdersAsync(PagedRequest request, int? outletId, int userId);
    Task<OrderDto> UpdateOrderStatusAsync(int orderId, string status, int userId, string? reason = null);
    Task<OrderDto> VoidOrderAsync(int orderId, VoidOrderRequest request, int userId);
    Task<OrderDto> RefundAsync(int orderId, RefundRequest request, int userId);
    Task<OrderDto> ApplyDiscountAsync(int orderId, ApplyDiscountRequest request, int userId);
    Task<OrderDto> AddOrderItemAsync(int orderId, OrderItemRequest request, int userId);
    Task<OrderDto> RemoveOrderItemAsync(int orderId, int orderItemId, int userId);
    Task<OrderDto> ProcessPaymentAsync(int orderId, ProcessPaymentRequest request, int userId);
    Task<OrderDto> TransferTableAsync(int orderId, int newTableId, int userId);
    Task<OrderDto> MergeOrdersAsync(int sourceOrderId, int targetOrderId, int userId);
    Task<OrderDto> SplitOrderAsync(int orderId, List<int> orderItemIds, int newTableId, int userId);
}

public interface IKitchenService
{
    Task<List<KitchenTicketDto>> GetKitchenTicketsAsync(int? outletId, int? stationId, int userId);
    Task<KitchenTicketDto?> GetKitchenTicketByIdAsync(int ticketId, int userId);
    Task<KitchenTicketDto> UpdateTicketStatusAsync(UpdateTicketStatusRequest request, int userId);
    Task<List<KitchenTicketDto>> GetTicketsByStationAsync(int stationId, int userId);
}

public interface IInventoryService
{
    Task<List<ProductStockDto>> GetStockLevelsAsync(int? outletId, int userId);
    Task<List<InventoryLedgerDto>> GetInventoryMovementsAsync(int? outletId, PagedRequest request, int userId);
    Task<InventoryLedgerDto> RecordInventoryMovementAsync(RecordInventoryRequest request, int actorId);
    Task<List<ProductStockDto>> GetLowStockItemsAsync(int? outletId, int userId);
    Task<RecipeDto> CreateRecipeAsync(CreateRecipeRequest request, int userId);
    Task<List<RecipeDto>> GetRecipesAsync(int? productId, int userId);
}

public interface IShiftService
{
    Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, int userId);
    Task<ShiftDto> CloseShiftAsync(CloseShiftRequest request, int userId);
    Task<ShiftDto?> GetOpenShiftAsync(int userId, int outletId);
    Task<List<ShiftDto>> GetShiftsAsync(int? outletId, PagedRequest request, int userId);
    Task<ZReportDto> GenerateZReportAsync(int shiftId, int userId);
}

public interface IReportService
{
    Task<List<SalesReportDto>> GetSalesReportAsync(SalesReportRequest request, int userId);
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(int? outletId, int userId);
    Task<List<AuditLogDto>> GetAuditLogsAsync(int? outletId, PagedRequest request, int userId);
    Task<byte[]> ExportReportAsync(string reportType, SalesReportRequest request, int userId);
}

public interface IOutletService
{
    Task<List<OutletDto>> GetOutletsAsync(int userId);
    Task<OutletDto?> GetOutletByIdAsync(int id, int userId);
    Task<OutletDto> CreateOutletAsync(OutletDto dto, int userId);
    Task<OutletDto> UpdateOutletAsync(int id, OutletDto dto, int userId);
}

public interface IMenuService
{
    Task<List<CategoryDto>> GetCategoriesAsync(int outletId, int userId);
    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, int userId);
    Task<List<ProductDto>> GetProductsAsync(int outletId, int? categoryId, bool availableOnly, int userId);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request, int userId);
    Task<ProductDto> UpdateProductAsync(int productId, CreateProductRequest request, int userId);
    Task<List<ModifierGroupDto>> GetModifierGroupsAsync(int outletId, int userId);
    Task<ModifierGroupDto> CreateModifierGroupAsync(CreateModifierGroupRequest request, int userId);
}

public interface ITableService
{
    Task<List<TableDto>> GetTablesAsync(int outletId, int userId);
    Task<TableDto> CreateTableAsync(CreateTableRequest request, int userId);
    Task<TableDto> UpdateTableStatusAsync(int tableId, string status, int userId);
    Task<List<ZoneDto>> GetZonesAsync(int outletId, int userId);
    Task<ZoneDto> CreateZoneAsync(CreateZoneRequest request, int userId);
}
