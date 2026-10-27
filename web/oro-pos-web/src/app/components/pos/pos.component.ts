import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../services/toast.service';
import { TableDto, ProductDto, OrderItemRequest, CreateOrderRequest, OrderDto, ApiResponse } from '../../models';
import { InrPipe } from '../../pipes/inr.pipe';

@Component({
  selector: 'app-pos',
  standalone: true,
  imports: [CommonModule, FormsModule, InrPipe],
  templateUrl: './pos.component.html',
  styleUrls: ['./pos.component.css']
})
export class PosComponent implements OnInit {
  tables: TableDto[] = [];
  products: ProductDto[] = [];
  filteredProducts: ProductDto[] = [];
  currentOrder: OrderDto | null = null;
  orderItems: any[] = [];
  subtotal = 0; tax = 0; total = 0; discount = 0;
  user: any;
  outlet: any;
  selectedTableId: number | null = null;
  searchTerm = '';
  tableLoading = false;
  savingItem = false;

  // Payment modal
  showPayment = false;
  tenderType = 'Cash';
  amountReceived = 0;
  processing = false;

  // Discount modal
  showDiscount = false;
  discountMode: 'amount' | 'percent' = 'amount';
  discountValue: number | null = 0;
  discountReason = '';
  applyingDiscount = false;
  showNewSale = false;
  serviceType = 'Takeaway';
  showVoid = false;
  voidReason = '';
  showTransfer = false;
  transferTableId: number | null = null;
  productToAdd: ProductDto | null = null;
  itemQuantity = 1;
  itemNotes = '';
  modifiers: Record<number, boolean> = {};
  approverUsername = '';
  approverPassword = '';
  get needsApproval(): boolean { return this.user?.roleName !== 'Owner' && !this.user?.permissions?.includes('ApproveManagerAction'); }

  taxRate = 0.05; // 5% GST

  constructor(private api: ApiService, private auth: AuthService, private toast: ToastService) { this.user = this.auth.getCurrentUser(); }

  private errMsg(err: any, fallback: string): string {
    return err?.error?.errors?.[0] || err?.error?.message || fallback;
  }

  ngOnInit() {
    const outletId = this.user?.outletId;
    if (outletId) {
      this.api.getTables(outletId).subscribe((res: ApiResponse<TableDto[]>) => { if (res.success) this.tables = res.data || []; });
      this.api.getProducts(outletId).subscribe((res: ApiResponse<ProductDto[]>) => {
        if (res.success) { this.products = res.data || []; this.applyFilter(); }
      });
      this.api.getOutlet(outletId).subscribe((res: ApiResponse<any>) => { if (res.success) this.outlet = res.data; });
    }
  }

  applyFilter() {
    const term = this.searchTerm.trim().toLowerCase();
    this.filteredProducts = !term
      ? this.products
      : this.products.filter(p => p.name.toLowerCase().includes(term) || (p.sku || '').toLowerCase().includes(term));
  }

  get changeDue(): number {
    return Math.max(0, this.amountReceived - this.amountDue);
  }

  get amountDue(): number {
    const paid = (this.currentOrder?.payments || [])
      .filter(p => p.status === 'Captured').reduce((sum, p) => sum + p.amount, 0);
    return Math.max(0, Math.round((this.total - paid) * 100) / 100);
  }

  /** Live preview of the discounted total (subtotal - discount + GST on the net). */
  get previewTotal(): number {
    const val = Number(this.discountValue ?? 0);
    const disc = this.discountMode === 'percent'
      ? this.subtotal * Math.min(100, Math.max(0, val)) / 100
      : Math.min(this.subtotal, Math.max(0, val));
    const net = this.subtotal - disc;
    return net + net * this.taxRate;
  }

  selectTable(t: TableDto) {
    if (this.tableLoading || this.savingItem || this.processing) return;
    this.selectedTableId = t.id;
    this.currentOrder = null;
    this.orderItems = [];
    this.calculateTotals();
    this.tableLoading = true;
    if (!t.hasOpenOrder) {
      this.createOrderForTable(t.id);
    } else {
      // Dedicated lookup avoids silently missing orders beyond the first list page.
      this.api.getOpenOrderForTable(t.id).subscribe({
        next: (res) => {
          this.tableLoading = false;
          if (res.success && res.data) {
            this.currentOrder = res.data;
            this.orderItems = res.data.orderItems || [];
            this.calculateTotals();
          } else this.toast.warning('No open order found. Refresh the table list.');
        },
        error: (err) => {
          this.tableLoading = false;
          this.toast.error(this.errMsg(err, 'Could not load this table. Please retry.'));
        }
      });
    }
  }

  createOrderForTable(tableId?: number, serviceType = 'DineIn') {
    this.tableLoading = true;
    const req: CreateOrderRequest = {
      outletId: this.user?.outletId || 0,
      tableId,
      serviceType,
      items: []
    };
    this.api.createOrder(req).subscribe({
      next: (res: ApiResponse<OrderDto>) => {
        this.tableLoading = false;
        if (res.success && res.data) {
          this.currentOrder = res.data as OrderDto;
          this.orderItems = res.data.orderItems;
          this.calculateTotals();
          this.showNewSale = false;
          this.selectedTableId = tableId ?? null;
          const table = this.tables.find(t => t.id === tableId);
          if (table) { table.hasOpenOrder = true; table.status = 'Occupied'; }
        }
      },
      error: (err) => {
        this.tableLoading = false;
        const m = this.errMsg(err, 'Could not start an order for this table.');
        this.toast.error(m);
        if (/shift/i.test(m)) this.toast.info('Open a shift from the Shifts page, then try again.');
      }
    });
  }

  addItem(p: ProductDto) {
    if (this.tableLoading || this.savingItem || this.processing) return;
    if (!this.currentOrder) { this.toast.warning('Select a table first to start an order.'); return; }
    this.productToAdd = p;
    this.itemQuantity = 1;
    this.itemNotes = '';
    this.modifiers = {};
  }
  confirmAddItem() {
    const p = this.productToAdd;
    if (!p || !this.currentOrder || this.savingItem) return;
    if (!Number.isInteger(this.itemQuantity) || this.itemQuantity < 1 || this.itemQuantity > 999) { this.toast.warning('Enter a whole quantity between 1 and 999.'); return; }
    this.savingItem = true;
    const item: OrderItemRequest = { productId: p.id, quantity: this.itemQuantity, modifierOptionIds: Object.keys(this.modifiers).filter(k => this.modifiers[+k]).map(Number), notes: this.itemNotes };
    this.api.addOrderItem(this.currentOrder.id, item).subscribe({
      next: (res: ApiResponse<OrderDto>) => {
        this.savingItem = false;
        if (res.success && res.data) {
          this.currentOrder = res.data as OrderDto;
          this.orderItems = res.data.orderItems;
          this.calculateTotals();
          this.productToAdd = null;
        }
      },
      error: (err) => { this.savingItem = false; this.toast.error(this.errMsg(err, `Could not add ${p.name}.`)); }
    });
  }

  removeItem(index: number) {
    if (this.tableLoading || this.savingItem || this.processing || !this.currentOrder) return;
    this.savingItem = true;
    const item = this.orderItems[index];
    this.api.removeOrderItem(this.currentOrder!.id, item.id).subscribe({
      next: (res: ApiResponse<OrderDto>) => {
        this.savingItem = false;
        if (res.success && res.data) {
          this.currentOrder = res.data as OrderDto;
          this.orderItems = res.data.orderItems;
          this.calculateTotals();
        }
      },
      error: (err) => { this.savingItem = false; this.toast.error(this.errMsg(err, 'Could not remove item.')); }
    });
  }

  calculateTotals() {
    this.subtotal = this.currentOrder?.subtotal ?? 0;
    this.discount = this.currentOrder?.discountAmount ?? 0;
    this.tax = this.currentOrder?.taxAmount ?? 0;
    this.total = this.currentOrder?.totalAmount ?? 0;
  }

  openDiscountModal() {
    if (!this.currentOrder || this.orderItems.length === 0) { this.toast.warning('Add items before applying a discount.'); return; }
    this.discountMode = 'amount';
    this.discountValue = 0;
    this.discountReason = '';
    this.approverUsername = this.approverPassword = '';
    this.showDiscount = true;
  }

  closeDiscount() { this.showDiscount = false; }

  applyDiscount() {
    if (this.applyingDiscount || !this.currentOrder) return;
    const val = Number(this.discountValue ?? 0);
    if (val <= 0) { this.toast.warning('Enter a discount greater than zero.'); return; }
    if (!this.discountReason.trim()) { this.toast.warning('A discount reason is required.'); return; }
    const req: any = this.discountMode === 'percent'
      ? { percent: val, reason: this.discountReason || 'Discount applied' }
      : { amount: val, reason: this.discountReason || 'Discount applied' };
    req.approverUsername = this.approverUsername;
    req.approverPassword = this.approverPassword;
    this.applyingDiscount = true;
    this.api.applyDiscount(this.currentOrder.id, req).subscribe({
      next: (res) => {
        this.applyingDiscount = false;
        if (res.success && res.data) {
          this.currentOrder = res.data as OrderDto;
          this.orderItems = res.data.orderItems;
          this.calculateTotals();
          this.showDiscount = false;
          this.approverUsername = this.approverPassword = '';
          this.toast.success(`Discount of ₹${(res.data.discountAmount ?? 0).toFixed(2)} applied.`);
        }
      },
      error: (err) => { this.applyingDiscount = false; this.toast.error(this.errMsg(err, 'Could not apply discount.')); }
    });
  }

  openPaymentModal() {
    if (!this.currentOrder || this.orderItems.length === 0) { this.toast.warning('Add items before taking payment.'); return; }
    this.amountReceived = this.amountDue;
    this.tenderType = 'Cash';
    this.showPayment = true;
  }

  closePayment() { this.showPayment = false; }

  confirmPayment() {
    if (this.processing) return;
    if (!Number.isFinite(this.amountReceived) || this.amountReceived < this.amountDue) { this.toast.warning('Enter a valid amount covering the remaining balance.'); return; }
    this.processing = true;
    const req = { orderId: this.currentOrder!.id, tenderType: this.tenderType, amount: this.amountDue };
    this.api.processPayment(this.currentOrder!.id, req).subscribe({
      next: (res) => {
        this.processing = false;
        if (res.success) {
          if (res.data && res.data.status !== 'Closed') {
            this.currentOrder = res.data;
            this.orderItems = res.data.orderItems || [];
            this.calculateTotals();
            this.showPayment = false;
            this.toast.warning('Payment recorded, but a balance remains. Refresh and review the order before continuing.');
            return;
          }
          this.showPayment = false;
          this.currentOrder = null;
          this.orderItems = [];
          this.subtotal = this.tax = this.total = this.discount = 0;
          this.selectedTableId = null;
          // refresh table occupancy
          const outletId = this.user?.outletId;
          this.api.getTables(outletId).subscribe((r: ApiResponse<TableDto[]>) => { if (r.success) this.tables = r.data || []; });
          this.toast.success('Payment processed — order closed.');
        }
      },
      error: (err) => { this.processing = false; this.toast.error(this.errMsg(err, 'Payment failed. Please try again.')); }
    });
  }

  voidOrder() {
    if (!this.currentOrder) return;
    this.showVoid = true;
    this.voidReason = '';
    this.approverUsername = this.approverPassword = '';
  }
  confirmVoid() {
    if (!this.currentOrder || this.savingItem) return;
    const reason = this.voidReason;
    if (!reason.trim()) { this.toast.warning('A reason is required to void an order.'); return; }
    this.savingItem = true;
    this.api.voidOrder(this.currentOrder.id, { orderId: this.currentOrder.id, reason: reason.trim(), approverUsername: this.approverUsername, approverPassword: this.approverPassword }).subscribe({
      next: (res: ApiResponse<OrderDto>) => {
        this.savingItem = false;
        if (res.success) {
          this.currentOrder = null;
          this.orderItems = [];
          this.subtotal = this.tax = this.total = this.discount = 0;
          this.selectedTableId = null;
          const outletId = this.user?.outletId;
          this.api.getTables(outletId).subscribe((r: ApiResponse<TableDto[]>) => { if (r.success) this.tables = r.data || []; });
          this.toast.success('Order voided.');
          this.showVoid = false;
          this.approverUsername = this.approverPassword = '';
        }
      },
      error: (err) => {this.savingItem = false; this.toast.error(this.errMsg(err, 'Could not void the order.'));}
    });
  }
  get freeTables() { return this.tables.filter(t => !t.hasOpenOrder); }
  transfer() {
    if (!this.currentOrder || !this.transferTableId || this.savingItem) return;
    this.savingItem = true;
    this.api.transferTable(this.currentOrder.id, this.transferTableId).subscribe({next: r => { this.savingItem = false; if (r.data) {this.currentOrder = r.data; this.selectedTableId = r.data.tableId ?? null;} this.showTransfer = false; this.api.getTables(this.user.outletId).subscribe(t => this.tables = t.data || []); this.toast.success('Table transferred.'); }, error: e => {this.savingItem = false; this.toast.error(this.errMsg(e, 'Transfer failed.'));}});
  }
}
