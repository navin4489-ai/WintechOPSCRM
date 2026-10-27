import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ProductStockDto, ApiResponse } from '../../models';

@Component({
  selector: 'app-inventory',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './inventory.component.html',
  styleUrls: ['./inventory.component.css']
})
export class InventoryComponent implements OnInit {
  stock: ProductStockDto[] = [];
  movement: any = { productId: 0, movementType: 'Receipt', quantity: 0 };

  constructor(private api: ApiService, private auth: AuthService) {}

  ngOnInit() {
    this.load();
  }

  get lowCount(): number {
    return this.stock.filter(p => p.isLowStock).length;
  }

  load() {
    const outletId = this.auth.getCurrentUser()?.outletId;
    this.api.getStock(outletId).subscribe((res: ApiResponse<ProductStockDto[]>) => { if (res.success) this.stock = res.data || []; });
  }

  recordMovement() {
    if (!this.movement.productId || !this.movement.quantity) return;
    this.api.recordInventory(this.movement).subscribe((res: ApiResponse<any>) => {
      if (res.success) {
        alert('Movement recorded');
        this.movement = { productId: 0, movementType: 'Receipt', quantity: 0 };
        this.load();
      }
    });
  }
}
