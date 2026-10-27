import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService, Toast } from '../../services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-stack" *ngIf="(toastService.toasts$ | async) as toasts">
      <div class="toast" *ngFor="let t of toasts" [attr.data-type]="t.type" (click)="toastService.dismiss(t.id)">
        <span class="t-icon">
          @switch (t.type) {
            @case ('success') { ✓ }
            @case ('error') { ! }
            @case ('warning') { ⚠ }
            @default { i }
          }
        </span>
        <span class="t-msg">{{ t.message }}</span>
        <button class="t-close" (click)="toastService.dismiss(t.id); $event.stopPropagation()">×</button>
      </div>
    </div>
  `,
  styles: [`
    .toast-stack {
      position: fixed;
      right: 1.25rem;
      bottom: 1.25rem;
      z-index: 200;
      display: flex;
      flex-direction: column;
      gap: .6rem;
      max-width: min(400px, calc(100vw - 2.5rem));
      pointer-events: none;
    }
    .toast {
      pointer-events: auto;
      display: flex;
      align-items: flex-start;
      gap: .7rem;
      padding: .85rem .9rem;
      background: var(--surface);
      border: 1px solid var(--border);
      border-left: 4px solid var(--info);
      border-radius: 12px;
      box-shadow: var(--shadow-lg);
      animation: toast-in .28s cubic-bezier(.2, .9, .3, 1.3);
      cursor: pointer;
    }
    @keyframes toast-in {
      from { opacity: 0; transform: translateY(14px) scale(.97); }
      to   { opacity: 1; transform: none; }
    }
    .toast[data-type="success"] { border-left-color: var(--success); }
    .toast[data-type="error"]   { border-left-color: var(--danger); }
    .toast[data-type="warning"] { border-left-color: var(--warning); }

    .t-icon {
      width: 22px; height: 22px; flex-shrink: 0;
      display: grid; place-items: center;
      border-radius: 50%;
      background: var(--info-soft); color: var(--info);
      font-size: .78rem; font-weight: 800; line-height: 1;
      margin-top: 1px;
    }
    .toast[data-type="success"] .t-icon { background: var(--success-soft); color: var(--success); }
    .toast[data-type="error"]   .t-icon { background: var(--danger-soft); color: var(--danger); }
    .toast[data-type="warning"] .t-icon { background: var(--warning-soft); color: var(--warning); }

    .t-msg { flex: 1; font-size: .88rem; font-weight: 500; color: var(--text); line-height: 1.4; }
    .t-close {
      background: none; border: none; color: var(--text-faint);
      font-size: 1.15rem; line-height: 1; cursor: pointer; padding: 0 .1rem;
      flex-shrink: 0;
    }
    .t-close:hover { color: var(--text); }
  `]
})
export class ToastComponent {
  constructor(public toastService: ToastService) {}
}
