import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type ToastType = 'success' | 'error' | 'info' | 'warning';

export interface Toast {
  id: number;
  message: string;
  type: ToastType;
}

/**
 * Lightweight in-app notifications. Replaces native alert()/confirm() so the
 * UI stays on-brand and non-blocking.
 *
 *   this.toast.success('Payment processed');
 *   this.toast.error('Open a shift first');
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly state = new BehaviorSubject<Toast[]>([]);
  readonly toasts$ = this.state.asObservable();

  private seq = 0;

  show(message: string, type: ToastType = 'info', durationMs = 4000): void {
    const toast: Toast = { id: ++this.seq, message, type };
    this.state.next([...this.state.value, toast]);
    if (durationMs > 0) {
      setTimeout(() => this.dismiss(toast.id), durationMs);
    }
  }

  success(message: string): void { this.show(message, 'success'); }
  error(message: string): void { this.show(message, 'error', 6000); }
  warning(message: string): void { this.show(message, 'warning', 5000); }
  info(message: string): void { this.show(message, 'info'); }

  dismiss(id: number): void {
    this.state.next(this.state.value.filter(t => t.id !== id));
  }
}
