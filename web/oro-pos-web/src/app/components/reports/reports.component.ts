import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ApiResponse } from '../../models';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule],
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

  exportReport() {
    const outletId = this.auth.getCurrentUser()?.outletId;
    this.api.getSalesReport(outletId).subscribe((res: ApiResponse<any[]>) => {
      if (res.success) {
        let csv = 'Group,Orders,Gross,Net,Cash,Card\n';
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
