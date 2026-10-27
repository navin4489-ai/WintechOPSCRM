import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ApiResponse } from '../../models';
import { InrPipe } from '../../pipes/inr.pipe';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, InrPipe],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  summary: any;
  recentSales: any[] = [];
  loaded = false;

  constructor(private api: ApiService, private auth: AuthService) {}

  ngOnInit() {
    const outletId = this.auth.getCurrentUser()?.outletId;
    this.api.getDashboard(outletId).subscribe((res: ApiResponse<any>) => {
      if (res.success) this.summary = res.data;
      this.loaded = true;
    });
    this.api.getSalesReport(outletId).subscribe((res: ApiResponse<any[]>) => {
      if (res.success) this.recentSales = (res.data || []).slice(0, 10);
    });
  }
}
