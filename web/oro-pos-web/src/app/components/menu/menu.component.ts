import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ApiResponse } from '../../models';

@Component({
  selector: 'app-menu',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './menu.component.html',
  styleUrls: ['./menu.component.css']
})
export class MenuComponent implements OnInit {
  products: any[] = [];
  categories: any[] = [];

  constructor(private api: ApiService, private auth: AuthService) {}

  ngOnInit() {
    const outletId = this.auth.getCurrentUser()?.outletId || 0;
    this.api.getProducts(outletId).subscribe((res: ApiResponse<any[]>) => { if (res.success) this.products = res.data || []; });
    this.api.getCategories(outletId).subscribe((res: ApiResponse<any[]>) => { if (res.success) this.categories = res.data || []; });
  }
}
