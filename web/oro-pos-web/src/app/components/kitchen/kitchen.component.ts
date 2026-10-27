import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { KitchenTicketDto, ApiResponse } from '../../models';

@Component({
  selector: 'app-kitchen',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './kitchen.component.html',
  styleUrls: ['./kitchen.component.css']
})
export class KitchenComponent implements OnInit {
  ticketColumns: { name: string; tickets: KitchenTicketDto[] }[] = [];

  constructor(private api: ApiService, private auth: AuthService) {}

  ngOnInit() {
    this.loadTickets();
    setInterval(() => this.loadTickets(), 5000);
  }

  loadTickets() {
    const outletId = this.auth.getCurrentUser()?.outletId;
    this.api.getKitchenTickets(outletId).subscribe((res: ApiResponse<KitchenTicketDto[]>) => {
      if (res.success) {
        const tickets = res.data || [];
        const stations = [...new Set(tickets.map((t: any) => t.stationName || 'General'))];
        this.ticketColumns = stations.map(name => ({
          name,
          tickets: tickets.filter(t => (t.stationName || 'General') === name)
        }));
      }
    });
  }

  updateStatus(ticketId: number, status: string) {
    this.api.updateTicketStatus(ticketId, status).subscribe(() => this.loadTickets());
  }
}
