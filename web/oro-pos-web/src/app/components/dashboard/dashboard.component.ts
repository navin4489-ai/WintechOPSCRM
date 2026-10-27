import { Component } from '@angular/core';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { CommonModule } from '@angular/common';
import { ApiResponse } from '../../models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent {
  summary: any;
  recentSales: any[] = [];

  constructor(private api: ApiService, private auth: AuthService) {
    const outletId = auth.getCurrentUser()?.outletId;
    api.getDashboard(outletId).subscribe((res: ApiResponse<any>) => {
      if (res.success) this.summary = res.data;
    });
    api.getSalesReport(outletId).subscribe((res: ApiResponse<any[]>) => {
      if (res.success) this.recentSales = (res.data || []).slice(0, 10);
    });
  }
}
