export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string;
  errors?: string[];
  totalCount?: number;
}

export interface UserDto {
  id: number;
  username: string;
  email: string;
  roleName?: string;
  permissions: string[];
  outletId?: number;
  outletName?: string;
  isActive: boolean;
}

export interface LoginRequest {
  username: string;
  password: string;
  deviceName?: string;
  stationType?: string;
}

export interface LoginResponse {
  token: string;
  refreshToken: string;
  expiresAt: Date;
  user: UserDto;
}

export interface OutletDto {
  id: number;
  name: string;
  address?: string;
  phone?: string;
  timezone: string;
  taxSetup?: string;
  currency: string;
  isOperating: boolean;
}

export interface CategoryDto {
  id: number;
  outletId: number;
  name: string;
  sortOrder: number;
}

export interface ModifierOptionDto {
  id: number;
  modifierGroupId: number;
  name: string;
  priceImpact: number;
}

export interface ModifierGroupDto {
  id: number;
  outletId: number;
  name: string;
  isRequired: boolean;
  minSelections: number;
  maxSelections: number;
  options: ModifierOptionDto[];
}

export interface ProductDto {
  id: number;
  outletId: number;
  categoryId?: number;
  categoryName?: string;
  sku: string;
  barcode?: string;
  name: string;
  description?: string;
  price: number;
  taxClass: string;
  isAvailable: boolean;
  sortOrder: number;
  imageUrl?: string;
  modifierGroups: ModifierGroupDto[];
}

export interface TableDto {
  id: number;
  outletId: number;
  zoneId?: number;
  zoneName?: string;
  tableNumber: string;
  status: string;
  capacity: number;
  floorPlanX?: number;
  floorPlanY?: number;
  hasOpenOrder: boolean;
}

export interface ZoneDto {
  id: number;
  outletId: number;
  name: string;
  floorPlanData?: string;
}

export interface OrderItemDto {
  id: number;
  orderId: number;
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  modifiers?: string;
  notes?: string;
  kitchenStation: string;
  status: string;
  lineTotal: number;
}

export interface PaymentDto {
  id: number;
  orderId: number;
  tenderType: string;
  amount: number;
  provider?: string;
  reference?: string;
  status: string;
  processedAt?: Date;
}

export interface OrderDto {
  id: number;
  outletId: number;
  tableId?: number;
  tableNumber?: string;
  userId?: number;
  userName?: string;
  serviceType: string;
  orderNumber: string;
  status: string;
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  notes?: string;
  createdAt: Date;
  closedAt?: Date;
  orderItems: OrderItemDto[];
  payments: PaymentDto[];
}

export interface ProcessPaymentRequest {
  orderId: number;
  tenderType: string;
  amount: number;
  provider?: string;
  reference?: string;
}

export interface VoidOrderRequest {
  orderId: number;
  reason: string;
  approvedByUserId?: number;
}

export interface KitchenTicketDto {
  id: number;
  orderId: number;
  orderNumber: string;
  tableNumber?: string;
  stationId?: number;
  stationName: string;
  status: string;
  priority: number;
  createdAt: Date;
  acceptedAt?: Date;
  readyAt?: Date;
  servedAt?: Date;
  items: KitchenTicketItemDto[];
}

export interface KitchenTicketItemDto {
  orderItemId: number;
  productName: string;
  quantity: number;
  modifiers?: string;
  notes?: string;
  status: string;
}

export interface UpdateTicketStatusRequest {
  ticketId: number;
  status: string;
}

export interface ProductStockDto {
  productId: number;
  productName: string;
  sku: string;
  currentStock: number;
  lowStockThreshold?: number;
  isLowStock: boolean;
}

export interface RecordInventoryRequest {
  productId?: number;
  movementType: string;
  quantity: number;
  location?: string;
  batch?: string;
  source?: string;
}

export interface InventoryLedgerDto {
  id: number;
  productId?: number;
  productName: string;
  movementType: string;
  quantity: number;
  location?: string;
  batch?: string;
  source?: string;
  actorId?: number;
  actorName?: string;
  createdAt: Date;
}

export interface RecipeDto {
  id: number;
  productId: number;
  productName: string;
  ingredientId: number;
  ingredientName: string;
  quantity: number;
  unit: string;
}

export interface CreateRecipeRequest {
  productId: number;
  ingredientId: number;
  quantity: number;
  unit: string;
}

export interface ShiftDto {
  id: number;
  userId: number;
  userName: string;
  outletId: number;
  openTime: Date;
  closeTime?: Date;
  openingFloat: number;
  paidIn: number;
  paidOut: number;
  countedCash: number;
  variance: number;
  status: string;
}

export interface OpenShiftRequest {
  userId: number;
  outletId: number;
  openingFloat: number;
}

export interface CloseShiftRequest {
  shiftId: number;
  countedCash: number;
  closedBy?: string;
}

export interface ZReportDto {
  shiftId: number;
  userName: string;
  openTime: Date;
  closeTime?: Date;
  openingFloat: number;
  grossSales: number;
  discounts: number;
  taxes: number;
  netSales: number;
  cashExpected: number;
  cashCounted: number;
  variance: number;
  orderCount: number;
}

export interface SalesReportDto {
  groupKey: string;
  orderCount: number;
  grossSales: number;
  discounts: number;
  taxes: number;
  netSales: number;
  cash: number;
  card: number;
  digitalWallet: number;
}

export interface SalesReportRequest {
  outletId?: number;
  fromDate?: Date;
  toDate?: Date;
  groupBy?: string;
}

export interface AuditLogDto {
  id: number;
  actorName?: string;
  action: string;
  entity: string;
  entityId?: number;
  reason?: string;
  createdAt: Date;
}

export interface DashboardSummaryDto {
  todaySales: number;
  todayOrders: number;
  todayCash: number;
  openTables: number;
  pendingKitchenTickets: number;
  lowStockItems: number;
  recentSales: SalesReportDto[];
}

export interface PagedRequest {
  page: number;
  pageSize: number;
  search?: string;
  sortBy?: string;
  sortDescending: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
