import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../services/toast.service';
import { InrPipe } from '../../pipes/inr.pipe';
import { ShiftDto, ZReportDto } from '../../models';

@Component({
  selector: 'app-shifts',
  standalone: true,
  imports: [CommonModule, FormsModule, InrPipe],
  templateUrl: './shifts.component.html',
  styleUrls: ['./shifts.component.css']
})
export class ShiftsComponent implements OnInit {
  user: any;
  outletId = 1;

  loading = true;
  openShift: ShiftDto | null = null;
  history: ShiftDto[] = [];

  // open-shift form
  openingFloat: number | null = 0;

  // close-shift form
  countedCash: number | null = 0;
  closing = false;

  // z-report modal
  showZReport = false;
  zLoading = false;
  zReport: ZReportDto | null = null;

  constructor(
    private api: ApiService,
    private auth: AuthService,
    private toast: ToastService
  ) {
    this.user = this.auth.getCurrentUser();
    this.outletId = this.user?.outletId ?? 1;
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.api.getOpenShift(this.outletId).subscribe({
      next: (res) => {
        this.openShift = res.data ?? null;
        this.countedCash = this.openShift ? this.expectedCash(this.openShift) : 0;
        this.loadHistory();
      },
      error: () => { this.loading = false; this.loadHistory(); }
    });
  }

  loadHistory(): void {
    this.api.getShifts(this.outletId, 1, 50).subscribe({
      next: (res) => {
        this.history = res.data ?? [];
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  openShiftNow(): void {
    const float = Number(this.openingFloat ?? 0);
    if (float < 0) { this.toast.warning('Opening float cannot be negative.'); return; }
    this.api.openShift({ userId: this.user.id, outletId: this.outletId, openingFloat: float }).subscribe({
      next: (res) => {
        this.openShift = res.data ?? null;
        this.countedCash = float;
        this.toast.success('Shift opened. You can now take orders.');
        this.loadHistory();
      },
      error: (err) => this.toast.error(this.msg(err, 'Could not open shift.'))
    });
  }

  closeShiftNow(): void {
    if (!this.openShift) return;
    const counted = Number(this.countedCash ?? 0);
    this.closing = true;
    this.api.closeShift({ shiftId: this.openShift.id, countedCash: counted }).subscribe({
      next: (res) => {
        this.closing = false;
        const variance = res.data?.variance ?? 0;
        this.toast.success(variance === 0
          ? 'Shift closed. Cash balanced.'
          : `Shift closed. Cash variance ${variance > 0 ? '+' : ''}${variance.toFixed(2)}.`);
        this.openShift = null;
        this.openingFloat = 0;
        this.load();
      },
      error: (err) => { this.closing = false; this.toast.error(this.msg(err, 'Could not close shift.')); }
    });
  }

  viewZReport(shiftId: number): void {
    this.showZReport = true;
    this.zLoading = true;
    this.zReport = null;
    this.api.getZReport(shiftId).subscribe({
      next: (res) => { this.zReport = res.data ?? null; this.zLoading = false; },
      error: (err) => { this.zLoading = false; this.toast.error(this.msg(err, 'Could not load Z-report.')); }
    });
  }

  closeZReport(): void {
    this.showZReport = false;
    this.zReport = null;
  }

  expectedCash(s: ShiftDto): number {
    const gross = (s as any).grossSales ?? 0;
    return s.openingFloat + gross;
  }

  hoursBetween(s: ShiftDto): string {
    const start = new Date(s.openTime).getTime();
    const end = s.closeTime ? new Date(s.closeTime).getTime() : Date.now();
    const mins = Math.max(0, Math.round((end - start) / 60000));
    const h = Math.floor(mins / 60);
    const m = mins % 60;
    return `${h}h ${m}m`;
  }

  private msg(err: any, fallback: string): string {
    return err?.error?.errors?.[0] || err?.error?.message || fallback;
  }
}
