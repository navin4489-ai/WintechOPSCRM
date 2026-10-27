import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { TableDto, ProductDto, OrderItemRequest, CreateOrderRequest, OrderDto, ApiResponse } from '../../models';

@Component({
  selector: 'app-pos',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './pos.component.html',
  styleUrls: ['./pos.component.css']
})
export class PosComponent implements OnInit {
  tables: TableDto[] = [];
  products: ProductDto[] = [];
  currentOrder: OrderDto | null = null;
  orderItems: any[] = [];
  subtotal = 0; tax = 0; total = 0;
  user: any;
  outlet: any;

  constructor(private api: ApiService, private auth: AuthService) { this.user = this.auth.getCurrentUser(); }

  ngOnInit() {
    const outletId = this.user?.outletId;
    if (outletId) {
      this.api.getTables(outletId).subscribe((res: ApiResponse<TableDto[]>) => { if (res.success) this.tables = res.data || []; });
      this.api.getProducts(outletId).subscribe((res: ApiResponse<ProductDto[]>) => { if (res.success) this.products = res.data || []; });
      this.api.getOutlet(outletId).subscribe((res: ApiResponse<any>) => { if (res.success) this.outlet = res.data; });
    }
  }

  selectTable(t: TableDto) {
    this.tables.forEach(x => x.status = x.id === t.id ? 'Selected' : x.status);
    if (t.hasOpenOrder) {
    } else {
      this.createOrderForTable(t.id);
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
    if (!this.currentOrder) return;
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
    this.tax = this.subtotal * 0.1;
    this.total = this.subtotal + this.tax;
  }

  openPaymentModal() {
    const amount = prompt('Enter payment amount:', this.total.toString());
    if (amount) {
      const req = { orderId: this.currentOrder!.id, tenderType: 'Cash', amount: parseFloat(amount) };
      this.api.processPayment(this.currentOrder!.id, req).subscribe((res: ApiResponse<OrderDto>) => {
        if (res.success) {
          this.currentOrder = res.data ?? null;
          alert('Payment processed!');
        }
      });
    }
  }

  voidOrder() {
    if (!this.currentOrder) return;
    if (!confirm('Void this order?')) return;
    this.api.voidOrder(this.currentOrder.id, { orderId: this.currentOrder.id, reason: 'Customer request' }).subscribe((res: ApiResponse<OrderDto>) => {
      if (res.success) {
        this.currentOrder = res.data as OrderDto;
        this.orderItems = [];
      }
    });
  }
}
