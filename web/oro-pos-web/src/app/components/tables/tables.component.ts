import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../services/toast.service';
import { TableDto, CreateTableRequest, ZoneDto, CreateZoneRequest, ApiResponse } from '../../models';

@Component({
  selector: 'app-tables',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './tables.component.html',
  styleUrls: ['./tables.component.css']
})
export class TablesComponent implements OnInit {
  tables: TableDto[] = [];
  zones: ZoneDto[] = [];
  newTable: CreateTableRequest = { outletId: 0, tableNumber: '', capacity: 4 };
  newZone: CreateZoneRequest = { outletId: 0, name: '' };

  constructor(private api: ApiService, private auth: AuthService, private toast: ToastService) {
    const user = auth.getCurrentUser();
    this.newTable.outletId = user?.outletId || 0;
    this.newZone.outletId = user?.outletId || 0;
  }

  ngOnInit() {
    const outletId = this.newTable.outletId;
    this.api.getTables(outletId).subscribe((res: ApiResponse<TableDto[]>) => { if (res.success) this.tables = res.data || []; });
    this.api.getZones(outletId).subscribe((res: ApiResponse<ZoneDto[]>) => { if (res.success) this.zones = res.data || []; });
  }

  createTable() {
    this.api.createTable(this.newTable).subscribe({
      next: (res: ApiResponse<TableDto>) => {
        if (res.success && res.data) {
          this.tables.push(res.data);
          this.newTable = { outletId: this.newTable.outletId, tableNumber: '', capacity: 4 };
        } else {
          this.toast.error(res.errors?.[0] || 'Could not add table');
        }
      },
      error: () => this.toast.error('Could not add table. Only store managers and owners can set up tables.')
    });
  }

  createZone() {
    this.api.createZone(this.newZone).subscribe({
      next: (res: ApiResponse<ZoneDto>) => {
        if (res.success && res.data) {
          this.zones.push(res.data);
          this.newZone = { outletId: this.newZone.outletId, name: '' };
        } else {
          this.toast.error(res.errors?.[0] || 'Could not add zone');
        }
      },
      error: () => this.toast.error('Could not add zone. Only store managers and owners can set up zones.')
    });
  }

  changeStatus(tableId: number, status: string) {
    this.api.updateTableStatus(tableId, status).subscribe((res: ApiResponse<TableDto>) => {
      if (res.success) {
        const t = this.tables.find(x => x.id === tableId);
        if (t) t.status = status;
      }
    });
  }
}
