import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../services/toast.service';
import { InrPipe } from '../../pipes/inr.pipe';
import { OrderDto, AuditLogDto } from '../../models';

@Component({selector: 'app-operations', standalone: true, imports: [CommonModule, FormsModule, InrPipe], templateUrl: './operations.component.html', styleUrls: ['./operations.component.css']})
export class OperationsComponent implements OnInit {
  audit = false;
  rows: OrderDto[] = [];
  logs: AuditLogDto[] = [];
  page = 1;
  loading = false;
  busy = false;
  search = '';
  selected: OrderDto | null = null;
  refund = false;
  reason = '';
  reference = '';
  restock = false;
  approverUsername = '';
  approverPassword = '';
  cashReceived = 0;
  get balance() { return this.selected ? Math.max(0, Math.round((this.selected.totalAmount - this.selected.payments.filter(p=>p.status==='Captured').reduce((s,p)=>s+p.amount,0))*100)/100) : 0; }
  collectCash() {if(!this.selected||this.busy)return;if(!Number.isFinite(this.cashReceived)||this.cashReceived<this.balance){this.toast.warning('Enter cash received covering the outstanding balance.');return;}const change=this.cashReceived-this.balance;this.busy=true;this.api.processPayment(this.selected.id,{orderId:this.selected.id,tenderType:'Cash',amount:this.balance}).subscribe({next:r=>{this.busy=false;this.selected=r.data||null;this.toast.success(`Cash collected. Change due ${new InrPipe().transform(change)}.`);this.load();},error:e=>this.fail(e)});}
  constructor(private api: ApiService, private auth: AuthService, private route: ActivatedRoute, private toast: ToastService) {}
  ngOnInit() { this.route.data.subscribe(data => { this.audit = data['audit'] === true; this.page = 1; this.load(); }); }
  can(permission: string) { const user = this.auth.getCurrentUser(); return user?.roleName === 'Owner' || !!user?.permissions?.includes(permission); }
  load() {
    this.loading = true;
    const outlet = this.auth.getCurrentUser()?.outletId;
    if (this.audit) this.api.getAuditLogs(outlet, this.page, 20).subscribe({next: r => { this.logs = r.data || []; this.loading = false; }, error: e => this.fail(e)});
    else this.api.getOrders(this.page, 20, outlet, this.search).subscribe({next: r => { this.rows = r.data?.items || []; this.loading = false; }, error: e => this.fail(e)});
  }
  detail(id: number) { if (this.busy) return; this.busy = true; this.api.getOrderById(id).subscribe({next: r => { this.busy = false; this.selected = r.data || null; this.refund = false; this.reason = this.reference = ''; this.restock = false; }, error: e => this.fail(e)}); }
  submitRefund() {
    if (this.busy || !this.selected) return;
    if (!this.reason.trim() || !this.reference.trim()) { this.toast.warning('Reason and refund reference are required.'); return; }
    this.busy = true;
    this.api.refundOrder(this.selected.id, {reason: this.reason.trim(), reference: this.reference.trim(), restock: this.restock, approverUsername: this.approverUsername, approverPassword: this.approverPassword}).subscribe({next: r => { this.busy = false; this.selected = r.data || null; this.refund = false; this.approverUsername = this.approverPassword = ''; this.toast.success('External refund recorded and audited.'); this.load(); }, error: e => this.fail(e)});
  }
  print() {
    if (!this.selected || this.busy) return;
    const win = window.open('', '_blank', 'width=500,height=720');
    if (!win) { this.toast.warning('Allow popups to print the receipt.'); return; }
    win.document.body.textContent = 'Preparing receipt…';
    this.busy = true;
    this.api.getReceipt(this.selected.id).subscribe({next: r => {
      this.busy = false;
      const order = r.data;
      if (!order) { win.close(); return; }
      const esc = (v: unknown) => String(v ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
      const inr = (n: number) => esc(new InrPipe().transform(n));
      win.document.title = `ORO receipt ${order.orderNumber}`;
      win.document.body.innerHTML = `<style>body{font:14px system-ui;max-width:380px;margin:32px auto;color:#0b1b3a}table{width:100%;border-collapse:collapse}td{padding:8px 0;border-bottom:1px solid #ddd}td:last-child{text-align:right}h1{letter-spacing:4px}</style><h1>ORO POS</h1><h3>Transaction receipt</h3><p>${esc(order.orderNumber)} · ${esc(order.status)}</p><p>${esc(order.serviceType)} · Table ${esc(order.tableNumber || '—')}</p><table>${order.orderItems.map(i => `<tr><td>${esc(i.quantity)} × ${esc(i.productName)}</td><td>${inr(i.lineTotal)}</td></tr>`).join('')}</table><p>Subtotal: ${inr(order.subtotal)}</p><p>Discount: ${inr(order.discountAmount)}</p><p>GST: ${inr(order.taxAmount)}</p><h2>Total: ${inr(order.totalAmount)}</h2><p>Thank you. Keep this receipt for your records.</p>`;
      win.focus(); win.print();
    }, error: e => { win.close(); this.fail(e); }});
  }
  changePage(delta: number) { this.page = Math.max(1, this.page + delta); this.load(); }
  fail(e: any) { this.busy = this.loading = false; this.toast.error(e?.error?.errors?.[0] || 'Request failed. Refresh before retrying.'); }
}
