import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../services/toast.service';
import { KitchenTicketDto, ApiResponse } from '../../models';

@Component({
  selector: 'app-kitchen',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './kitchen.component.html',
  styleUrls: ['./kitchen.component.css']
})
export class KitchenComponent implements OnInit, OnDestroy {
  ticketColumns: { name: string; tickets: KitchenTicketDto[] }[] = [];
  private poll?: any;

  constructor(private api: ApiService, private auth: AuthService, private toast: ToastService) {}

  ngOnInit() {
    this.loadTickets();
    this.poll = setInterval(() => this.loadTickets(), 5000);
  }

  ngOnDestroy() {
    if (this.poll) { clearInterval(this.poll); this.poll = undefined; }
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
    this.api.updateTicketStatus(ticketId, status).subscribe({
      next: () => this.loadTickets(),
      error: (err) => this.toast.error(err?.error?.errors?.[0] || 'Could not update ticket status.')
    });
  }
}
