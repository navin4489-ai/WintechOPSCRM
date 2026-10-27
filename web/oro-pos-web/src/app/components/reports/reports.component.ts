import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ApiResponse } from '../../models';
import { InrPipe } from '../../pipes/inr.pipe';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, InrPipe],
  templateUrl: './reports.component.html',
  styleUrls: ['./reports.component.css']
})
export class ReportsComponent implements OnInit {
  sales: any[] = [];

  constructor(private api: ApiService, private auth: AuthService) {}

  ngOnInit() {
    const outletId = this.auth.getCurrentUser()?.outletId;
    this.api.getSalesReport(outletId).subscribe((res: ApiResponse<any[]>) => { if (res.success) this.sales = res.data || []; });
  }

  get totals() {
    return this.sales.reduce((a, r) => ({
      orders: a.orders + (r.orderCount || 0),
      gross: a.gross + (r.grossSales || 0),
      net: a.net + (r.netSales || 0),
      cash: a.cash + (r.cash || 0),
      card: a.card + (r.card || 0),
    }), { orders: 0, gross: 0, net: 0, cash: 0, card: 0 });
  }

  exportReport() {
    const outletId = this.auth.getCurrentUser()?.outletId;
    this.api.getSalesReport(outletId).subscribe((res: ApiResponse<any[]>) => {
      if (res.success) {
        let csv = 'Group,Orders,Gross (INR),Net (INR),Cash (INR),Card (INR)\n';
        (res.data || []).forEach((r: any) => {
          csv += `${r.groupKey},${r.orderCount},${r.grossSales},${r.netSales},${r.cash},${r.card}\n`;
        });
        const blob = new Blob([csv], { type: 'text/csv' });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'sales_report.csv';
        a.click();
      }
    });
  }
}
