import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
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
  subtotal = 0; tax = 0; total = 0;
  user: any;
  outlet: any;
  selectedTableId: number | null = null;
  searchTerm = '';

  // Payment modal
  showPayment = false;
  tenderType = 'Cash';
  amountReceived = 0;
  processing = false;

  taxRate = 0.05; // 5% GST

  constructor(private api: ApiService, private auth: AuthService) { this.user = this.auth.getCurrentUser(); }

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
    return Math.max(0, this.amountReceived - this.total);
  }

  selectTable(t: TableDto) {
    this.selectedTableId = t.id;
    if (!t.hasOpenOrder) {
      this.createOrderForTable(t.id);
    } else {
      // load existing open order items
      this.api.getOrders(1, 50, this.user?.outletId).subscribe((res: ApiResponse<any>) => {
        const list = res.data?.items || res.data || [];
        const open = (list as any[]).find((o: any) => o.tableId === t.id && o.status !== 'Closed' && o.status !== 'Void');
        if (open) { this.currentOrder = open; this.orderItems = open.orderItems || []; this.calculateTotals(); }
      });
    }
  }

  createOrderForTable(tableId: number) {
    const req: CreateOrderRequest = {
      outletId: this.user?.outletId || 0,
      tableId,
      serviceType: 'DineIn',
      items: []
    };
    this.api.createOrder(req).subscribe((res: ApiResponse<OrderDto>) => {
      if (res.success && res.data) {
        this.currentOrder = res.data as OrderDto;
        this.orderItems = res.data.orderItems;
        this.calculateTotals();
      }
    });
  }

  addItem(p: ProductDto) {
    if (!this.currentOrder) { alert('Select a table first to start an order.'); return; }
    const item: OrderItemRequest = { productId: p.id, quantity: 1, modifierOptionIds: [], notes: '' };
    this.api.addOrderItem(this.currentOrder.id, item).subscribe((res: ApiResponse<OrderDto>) => {
      if (res.success && res.data) {
        this.currentOrder = res.data as OrderDto;
        this.orderItems = res.data.orderItems;
        this.calculateTotals();
      }
    });
  }

  removeItem(index: number) {
    const item = this.orderItems[index];
    this.api.removeOrderItem(this.currentOrder!.id, item.id).subscribe((res: ApiResponse<OrderDto>) => {
      if (res.success && res.data) {
        this.currentOrder = res.data as OrderDto;
        this.orderItems = res.data.orderItems;
        this.calculateTotals();
      }
    });
  }

  calculateTotals() {
    this.subtotal = this.orderItems.reduce((s: number, i: any) => s + i.lineTotal, 0);
    this.tax = this.subtotal * this.taxRate;
    this.total = this.subtotal + this.tax;
  }

  openPaymentModal() {
    if (!this.currentOrder || this.orderItems.length === 0) { alert('Add items before taking payment.'); return; }
    this.amountReceived = this.total;
    this.tenderType = 'Cash';
    this.showPayment = true;
  }

  closePayment() { this.showPayment = false; }

  confirmPayment() {
    if (this.processing) return;
    this.processing = true;
    const req = { orderId: this.currentOrder!.id, tenderType: this.tenderType, amount: this.amountReceived };
    this.api.processPayment(this.currentOrder!.id, req).subscribe({
      next: (res) => {
        this.processing = false;
        if (res.success) {
          this.showPayment = false;
          this.currentOrder = null;
          this.orderItems = [];
          this.subtotal = this.tax = this.total = 0;
          this.selectedTableId = null;
          // refresh table occupancy
          const outletId = this.user?.outletId;
          this.api.getTables(outletId).subscribe((r: ApiResponse<TableDto[]>) => { if (r.success) this.tables = r.data || []; });
          alert('Payment processed — order closed.');
        }
      },
      error: () => { this.processing = false; alert('Payment failed. Please try again.'); }
    });
  }

  voidOrder() {
    if (!this.currentOrder) return;
    if (!confirm('Void this order?')) return;
    this.api.voidOrder(this.currentOrder.id, { orderId: this.currentOrder.id, reason: 'Customer request' }).subscribe((res: ApiResponse<OrderDto>) => {
      if (res.success) {
        this.currentOrder = res.data as OrderDto;
        this.orderItems = [];
        this.calculateTotals();
      }
    });
  }
}
