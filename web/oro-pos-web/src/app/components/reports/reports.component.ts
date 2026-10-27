import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ApiResponse } from '../../models';
import { InrPipe } from '../../pipes/inr.pipe';
import {DocumentPrintService} from '../../services/document-print.service';
import {ToastService} from '../../services/toast.service';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, InrPipe],
  templateUrl: './reports.component.html',
  styleUrls: ['./reports.component.css']
})
export class ReportsComponent implements OnInit {
  sales: any[] = [];
  outletId?:number;printing=false;
  get canExport(){const u=this.auth.getCurrentUser();return u?.roleName==='Owner'||!!u?.permissions.includes('ExportReports');}

  constructor(private api: ApiService, private auth: AuthService,private documents:DocumentPrintService,private toast:ToastService) {}

  ngOnInit() {
    const outletId = this.auth.getCurrentUser()?.outletId;
    this.outletId=outletId;
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
    this.api.exportSales(outletId).subscribe(blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'sales_report.csv';
        a.click();
        window.URL.revokeObjectURL(url);
    });
  }
  printPdf(){if(this.printing||!this.outletId||!this.canExport)return;const id=this.outletId;const rows=[...this.sales];const win=window.open('','_blank','width=800,height=760');if(!win){this.toast.warning('Allow popups to print the report.');return;}win.document.body.textContent='Preparing sales report…';this.printing=true;this.api.getOutlet(id).subscribe({next:r=>{this.printing=false;try{if(!r.data)throw new Error('Outlet details unavailable.');this.documents.sales(win,r.data,rows);}catch(e){win.close();this.toast.error((e as Error).message);}},error:e=>{win.close();this.printing=false;this.toast.error(e?.error?.errors?.[0]||'Report printing failed.');}});}
}
