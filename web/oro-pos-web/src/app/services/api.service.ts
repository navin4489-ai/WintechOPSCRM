import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ApiResponse, OutletDto, CategoryDto, CreateCategoryRequest,
  ProductDto, CreateProductRequest, ModifierGroupDto, CreateModifierGroupRequest,
  TableDto, CreateTableRequest, ZoneDto, CreateZoneRequest,
  OrderDto, CreateOrderRequest, OrderItemRequest, PagedRequest, PagedResult,
  ProcessPaymentRequest, VoidOrderRequest, ApplyDiscountRequest,
  KitchenTicketDto, UpdateTicketStatusRequest,
  ProductStockDto, RecordInventoryRequest, InventoryLedgerDto,
  RecipeDto, CreateRecipeRequest,
  ShiftDto, OpenShiftRequest, CloseShiftRequest, ZReportDto,
  DashboardSummaryDto, SalesReportRequest, SalesReportDto, AuditLogDto,
  UserDto, RoleDto, CreateUserRequest, UpdateUserRequest
} from '../models';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private base = `${environment.apiUrl}`;

  constructor(private http: HttpClient) {}
  uploadProductImage(id: number, file: File): Observable<ApiResponse<{imageUrl:string}>> { const form = new FormData(); form.append('file',file); return this.http.post<ApiResponse<{imageUrl:string}>>(`${this.base}/products/${id}/image`, form); }
  removeProductImage(id: number): Observable<ApiResponse<unknown>> { return this.http.delete<ApiResponse<unknown>>(`${this.base}/products/${id}/image`); }
  saveProduct(id: number, request: CreateProductRequest): Observable<ApiResponse<ProductDto>> { return id ? this.http.put<ApiResponse<ProductDto>>(`${this.base}/products/${id}`, request) : this.createProduct(request); }
  superAdminOutlets(): Observable<ApiResponse<OutletDto[]>> { return this.http.get<ApiResponse<OutletDto[]>>(`${this.base}/super-admin/outlets`); }
  onboardOutlet(outlet:OutletDto,adminUsername:string,adminPassword:string,adminEmail?:string):Observable<ApiResponse<{outlet:OutletDto;administrator:UserDto}>> {return this.http.post<ApiResponse<{outlet:OutletDto;administrator:UserDto}>>(`${this.base}/super-admin/outlets`,{outlet,adminUsername,adminPassword,adminEmail:adminEmail?.trim()||null});}
  ownOutlet():Observable<ApiResponse<OutletDto>> {return this.http.get<ApiResponse<OutletDto>>(`${this.base}/outlet-settings`);}
  updateOwnOutlet(dto:OutletDto):Observable<ApiResponse<OutletDto>> {return this.http.put<ApiResponse<OutletDto>>(`${this.base}/outlet-settings`,dto);}
  ownOrdering():Observable<ApiResponse<any>> {return this.http.get<ApiResponse<any>>(`${this.base}/outlet-settings/ordering`);}
  configureOwnOrdering(enabled:boolean,rotateKey=false):Observable<ApiResponse<any>> {return this.http.post<ApiResponse<any>>(`${this.base}/outlet-settings/ordering`,{enabled,rotateKey});}
  saveOutlet(dto: OutletDto): Observable<ApiResponse<OutletDto>> { return dto.id ? this.http.put<ApiResponse<OutletDto>>(`${this.base}/super-admin/outlets/${dto.id}`, dto) : this.http.post<ApiResponse<OutletDto>>(`${this.base}/super-admin/outlets`, dto); }
  orderingSettings(id: number): Observable<ApiResponse<any>> { return this.http.get<ApiResponse<any>>(`${this.base}/super-admin/outlets/${id}/ordering`); }
  configureOrdering(id: number, enabled: boolean, rotateKey = false): Observable<ApiResponse<any>> { return this.http.post<ApiResponse<any>>(`${this.base}/super-admin/outlets/${id}/ordering`, {enabled, rotateKey}); }
  publicMenu(key: string): Observable<ApiResponse<any>> { return this.http.get<ApiResponse<any>>(`${this.base}/public/outlets/${encodeURIComponent(key)}/menu`); }
  placeGuestOrder(key: string, body: unknown): Observable<ApiResponse<any>> { return this.http.post<ApiResponse<any>>(`${this.base}/public/outlets/${encodeURIComponent(key)}/orders`, body); }
  trackGuestOrder(key: string, number: string, proof: string): Observable<ApiResponse<any>> { return this.http.post<ApiResponse<any>>(`${this.base}/public/outlets/${encodeURIComponent(key)}/orders/${encodeURIComponent(number)}/status`, {proof}); }
  changePassword(currentPassword: string, newPassword: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/auth/password`, {currentPassword, newPassword});
  }
  exportSales(outletId?: number): Observable<Blob> {
    return this.http.get(`${this.base}/reports/export?reportType=sales${outletId ? '&outletId=' + outletId : ''}`, {responseType: 'blob'});
  }

  // Outlets
  getOutlets(): Observable<ApiResponse<OutletDto[]>> {
    return this.http.get<ApiResponse<OutletDto[]>>(`${this.base}/outlets`);
  }
  getOutlet(id: number): Observable<ApiResponse<OutletDto>> {
    return this.http.get<ApiResponse<OutletDto>>(`${this.base}/outlets/${id}`);
  }
  createOutlet(dto: OutletDto): Observable<ApiResponse<OutletDto>> {
    return this.http.post<ApiResponse<OutletDto>>(`${this.base}/outlets`, dto);
  }
  updateOutlet(id: number, dto: OutletDto): Observable<ApiResponse<OutletDto>> {
    return this.http.put<ApiResponse<OutletDto>>(`${this.base}/outlets/${id}`, dto);
  }

  // Categories
  getCategories(outletId: number): Observable<ApiResponse<CategoryDto[]>> {
    return this.http.get<ApiResponse<CategoryDto[]>>(`${this.base}/categories/outlet/${outletId}`);
  }
  createCategory(req: CreateCategoryRequest): Observable<ApiResponse<CategoryDto>> {
    return this.http.post<ApiResponse<CategoryDto>>(`${this.base}/categories`, req);
  }

  // Products
  getProducts(outletId: number, categoryId?: number, availableOnly = true): Observable<ApiResponse<ProductDto[]>> {
    let url = `${this.base}/products?outletId=${outletId}&availableOnly=${availableOnly}`;
    if (categoryId) url += `&categoryId=${categoryId}`;
    return this.http.get<ApiResponse<ProductDto[]>>(url);
  }
  createProduct(req: CreateProductRequest): Observable<ApiResponse<ProductDto>> {
    return this.http.post<ApiResponse<ProductDto>>(`${this.base}/products`, req);
  }

  // Modifier Groups
  getModifierGroups(outletId: number): Observable<ApiResponse<ModifierGroupDto[]>> {
    return this.http.get<ApiResponse<ModifierGroupDto[]>>(`${this.base}/modifiergroups/outlet/${outletId}`);
  }
  createModifierGroup(req: CreateModifierGroupRequest): Observable<ApiResponse<ModifierGroupDto>> {
    return this.http.post<ApiResponse<ModifierGroupDto>>(`${this.base}/modifiergroups`, req);
  }

  // Tables/Zones
  getTables(outletId: number): Observable<ApiResponse<TableDto[]>> {
    return this.http.get<ApiResponse<TableDto[]>>(`${this.base}/tables/outlet/${outletId}`);
  }
  createTable(req: CreateTableRequest): Observable<ApiResponse<TableDto>> {
    return this.http.post<ApiResponse<TableDto>>(`${this.base}/tables`, req);
  }
  updateTableStatus(tableId: number, status: string): Observable<ApiResponse<TableDto>> {
    return this.http.put<ApiResponse<TableDto>>(`${this.base}/tables/${tableId}/status?status=${status}`, {});
  }
  getZones(outletId: number): Observable<ApiResponse<ZoneDto[]>> {
    return this.http.get<ApiResponse<ZoneDto[]>>(`${this.base}/zones/outlet/${outletId}`);
  }
  createZone(req: CreateZoneRequest): Observable<ApiResponse<ZoneDto>> {
    return this.http.post<ApiResponse<ZoneDto>>(`${this.base}/zones`, req);
  }

  // Orders
  createOrder(req: CreateOrderRequest): Observable<ApiResponse<OrderDto>> {
    return this.http.post<ApiResponse<OrderDto>>(`${this.base}/orders`, req);
  }
  getOrder(orderId: number): Observable<ApiResponse<OrderDto>> {
    return this.http.get<ApiResponse<OrderDto>>(`${this.base}/orders/${orderId}`);
  }
  getOrders(page = 1, pageSize = 20, outletId?: number, search?: string): Observable<ApiResponse<PagedResult<OrderDto>>> {
    let url = `${this.base}/orders?page=${page}&pageSize=${pageSize}`;
    if (outletId) url += `&outletId=${outletId}`;
    if (search) url += `&search=${encodeURIComponent(search)}`;
    return this.http.get<ApiResponse<PagedResult<OrderDto>>>(url);
  }
  getOrderById(orderId: number): Observable<ApiResponse<OrderDto>> {
    return this.http.get<ApiResponse<OrderDto>>(`${this.base}/orders/${orderId}`);
  }
  getReceipt(orderId: number): Observable<ApiResponse<OrderDto>> {
    return this.http.post<ApiResponse<OrderDto>>(`${this.base}/orders/${orderId}/receipt`, {});
  }
  refundOrder(orderId: number, body: {reason: string; reference: string; restock: boolean; approverUsername?: string; approverPassword?: string}): Observable<ApiResponse<OrderDto>> {
    return this.http.post<ApiResponse<OrderDto>>(`${this.base}/orders/${orderId}/refund`, body);
  }
  getOpenOrderForTable(tableId: number): Observable<ApiResponse<OrderDto>> {
    return this.http.get<ApiResponse<OrderDto>>(`${this.base}/orders/table/${tableId}/open`);
  }
  updateOrderStatus(orderId: number, status: string, reason?: string): Observable<ApiResponse<OrderDto>> {
    let url = `${this.base}/orders/${orderId}/status?status=${status}`;
    if (reason) url += `&reason=${reason}`;
    return this.http.put<ApiResponse<OrderDto>>(url, {});
  }
  voidOrder(orderId: number, req: VoidOrderRequest): Observable<ApiResponse<OrderDto>> {
    return this.http.post<ApiResponse<OrderDto>>(`${this.base}/orders/${orderId}/void`, req);
  }
  applyDiscount(orderId: number, req: ApplyDiscountRequest): Observable<ApiResponse<OrderDto>> {
    return this.http.post<ApiResponse<OrderDto>>(`${this.base}/orders/${orderId}/discount`, req);
  }
  addOrderItem(orderId: number, item: OrderItemRequest): Observable<ApiResponse<OrderDto>> {
    return this.http.post<ApiResponse<OrderDto>>(`${this.base}/orders/${orderId}/items`, item);
  }
  removeOrderItem(orderId: number, orderItemId: number): Observable<ApiResponse<OrderDto>> {
    return this.http.delete<ApiResponse<OrderDto>>(`${this.base}/orders/${orderId}/items/${orderItemId}`);
  }
  processPayment(orderId: number, req: ProcessPaymentRequest): Observable<ApiResponse<OrderDto>> {
    return this.http.post<ApiResponse<OrderDto>>(`${this.base}/orders/${orderId}/payment`, req);
  }
  transferTable(orderId: number, newTableId: number): Observable<ApiResponse<OrderDto>> {
    return this.http.post<ApiResponse<OrderDto>>(`${this.base}/orders/${orderId}/transfer-table?newTableId=${newTableId}`, {});
  }

  // Kitchen
  getKitchenTickets(outletId?: number, stationId?: number): Observable<ApiResponse<KitchenTicketDto[]>> {
    let url = `${this.base}/kitchentickets`;
    const params: string[] = [];
    if (outletId) params.push(`outletId=${outletId}`);
    if (stationId) params.push(`stationId=${stationId}`);
    if (params.length) url += '?' + params.join('&');
    return this.http.get<ApiResponse<KitchenTicketDto[]>>(url);
  }
  getKitchenTicket(ticketId: number): Observable<ApiResponse<KitchenTicketDto>> {
    return this.http.get<ApiResponse<KitchenTicketDto>>(`${this.base}/kitchentickets/${ticketId}`);
  }
  updateTicketStatus(ticketId: number, status: string): Observable<ApiResponse<KitchenTicketDto>> {
    return this.http.put<ApiResponse<KitchenTicketDto>>(`${this.base}/kitchentickets/${ticketId}/status?status=${status}`, {});
  }

  // Inventory
  getStock(outletId?: number): Observable<ApiResponse<ProductStockDto[]>> {
    let url = `${this.base}/inventory/stock`;
    if (outletId) url += `?outletId=${outletId}`;
    return this.http.get<ApiResponse<ProductStockDto[]>>(url);
  }
  recordInventory(req: RecordInventoryRequest): Observable<ApiResponse<InventoryLedgerDto>> {
    return this.http.post<ApiResponse<InventoryLedgerDto>>(`${this.base}/inventory/movement`, req);
  }
  getLowStock(outletId?: number): Observable<ApiResponse<ProductStockDto[]>> {
    let url = `${this.base}/inventory/low-stock`;
    if (outletId) url += `?outletId=${outletId}`;
    return this.http.get<ApiResponse<ProductStockDto[]>>(url);
  }

  // Recipes
  getRecipes(productId?: number): Observable<ApiResponse<RecipeDto[]>> {
    let url = `${this.base}/recipes`;
    if (productId) url += `?productId=${productId}`;
    return this.http.get<ApiResponse<RecipeDto[]>>(url);
  }
  createRecipe(req: CreateRecipeRequest): Observable<ApiResponse<RecipeDto>> {
    return this.http.post<ApiResponse<RecipeDto>>(`${this.base}/recipes`, req);
  }

  // Shifts
  openShift(req: OpenShiftRequest): Observable<ApiResponse<ShiftDto>> {
    return this.http.post<ApiResponse<ShiftDto>>(`${this.base}/shifts/open`, req);
  }
  closeShift(req: CloseShiftRequest): Observable<ApiResponse<ShiftDto>> {
    return this.http.post<ApiResponse<ShiftDto>>(`${this.base}/shifts/close`, req);
  }
  getOpenShift(outletId: number): Observable<ApiResponse<ShiftDto | null>> {
    return this.http.get<ApiResponse<ShiftDto | null>>(`${this.base}/shifts/open?outletId=${outletId}`);
  }
  getShifts(outletId?: number, page = 1, pageSize = 20): Observable<ApiResponse<ShiftDto[]>> {
    let url = `${this.base}/shifts?page=${page}&pageSize=${pageSize}`;
    if (outletId) url += `&outletId=${outletId}`;
    return this.http.get<ApiResponse<ShiftDto[]>>(url);
  }
  getZReport(shiftId: number): Observable<ApiResponse<ZReportDto>> {
    return this.http.get<ApiResponse<ZReportDto>>(`${this.base}/shifts/${shiftId}/z-report`);
  }

  // Reports
  getSalesReport(outletId?: number, fromDate?: Date, toDate?: Date, groupBy = 'outlet'): Observable<ApiResponse<SalesReportDto[]>> {
    let url = `${this.base}/reports/sales?groupBy=${groupBy}`;
    if (outletId) url += `&outletId=${outletId}`;
    if (fromDate) url += `&fromDate=${fromDate.toISOString()}`;
    if (toDate) url += `&toDate=${toDate.toISOString()}`;
    return this.http.get<ApiResponse<SalesReportDto[]>>(url);
  }
  getDashboard(outletId?: number): Observable<ApiResponse<DashboardSummaryDto>> {
    let url = `${this.base}/reports/dashboard`;
    if (outletId) url += `?outletId=${outletId}`;
    return this.http.get<ApiResponse<DashboardSummaryDto>>(url);
  }
  getAuditLogs(outletId?: number, page = 1, pageSize = 20): Observable<ApiResponse<AuditLogDto[]>> {
    let url = `${this.base}/reports/audit?page=${page}&pageSize=${pageSize}`;
    if (outletId) url += `&outletId=${outletId}`;
    return this.http.get<ApiResponse<AuditLogDto[]>>(url);
  }

  // Staff management (HR-001)
  getUsers(): Observable<ApiResponse<UserDto[]>> {
    return this.http.get<ApiResponse<UserDto[]>>(`${this.base}/users`);
  }
  createUser(req: CreateUserRequest): Observable<ApiResponse<UserDto>> {
    return this.http.post<ApiResponse<UserDto>>(`${this.base}/users`, req);
  }
  updateUser(id: number, req: UpdateUserRequest): Observable<ApiResponse<UserDto>> {
    return this.http.put<ApiResponse<UserDto>>(`${this.base}/users/${id}`, req);
  }
  deleteUser(id: number): Observable<ApiResponse<UserDto>> {
    return this.http.delete<ApiResponse<UserDto>>(`${this.base}/users/${id}`);
  }
  getRoles(): Observable<ApiResponse<RoleDto[]>> {
    return this.http.get<ApiResponse<RoleDto[]>>(`${this.base}/roles`);
  }
}
