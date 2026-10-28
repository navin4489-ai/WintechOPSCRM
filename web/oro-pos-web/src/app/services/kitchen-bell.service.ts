import { Injectable, OnDestroy } from '@angular/core';
import { KitchenTicketDto } from '../models';

const MUTED_KEY = 'win_kitchen_bell_muted';
/**
 * Kitchen order bell.
 *
 * Rings continuously while at least one ticket is still waiting for kitchen
 * acceptance (status "New") and falls silent once staff accept or progress it.
 * A desktop notification is also raised for each ticket that appears while the
 * Kitchen Display is open. The bell tone is synthesised with WebAudio so no
 * audio asset ships with the app and it works offline.
 */
@Injectable({ providedIn: 'root' })
export class KitchenBellService implements OnDestroy {
  pendingCount = 0;
  ringing = false;
  muted = localStorage.getItem(MUTED_KEY) === '1';
  /** True once the browser allowed audio (requires a user gesture). */
  armed = false;

  private ctx?: AudioContext;
  private timer?: ReturnType<typeof setInterval>;
  private seen = new Set<number>();

  get state(): 'ringing' | 'muted' | 'needs-permission' | 'silent' {
    if (this.ringing) return 'ringing';
    if (this.pendingCount && this.muted) return 'muted';
    if (this.pendingCount && !this.armed) return 'needs-permission';
    return 'silent';
  }

  /** Must be called from a click: unlocks audio and notification permission. */
  arm(): void {
    if (!this.ctx) {
      const Ctor = window.AudioContext || (window as any).webkitAudioContext;
      if (Ctor) this.ctx = new Ctor();
    }
    this.ctx?.resume().catch(() => {});
    this.armed = true;
    if (typeof Notification !== 'undefined' && Notification.permission === 'default') {
      Notification.requestPermission().catch(() => {});
    }
    if (this.pendingCount && !this.muted) this.startRinging();
    this.publish();
  }

  toggleMuted(): void {
    this.muted = !this.muted;
    localStorage.setItem(MUTED_KEY, this.muted ? '1' : '0');
    if (this.muted) this.stopRinging();
    else if (this.pendingCount) this.startRinging();
    this.publish();
  }

  /** Feed every refreshed ticket list from the Kitchen Display. */
  update(tickets: KitchenTicketDto[]): void {
    const pending = tickets.filter(t => t.status === 'New');
    const fresh = pending.filter(t => !this.seen.has(t.id));
    tickets.forEach(t => this.seen.add(t.id));
    this.pendingCount = pending.length;
    if (fresh.length) this.notify(fresh);
    if (this.pendingCount && !this.muted) this.startRinging();
    else if (!this.pendingCount) this.stopRinging();
    this.publish();
  }

  stopAll(): void {
    this.stopRinging();
    this.pendingCount = 0;
    this.publish();
  }

  ngOnDestroy(): void {
    this.stopAll();
  }

  private startRinging(): void {
    if (!this.armed || this.ringing) return;
    this.ringing = true;
    this.strike();
    this.timer = setInterval(() => this.strike(), 2400);
    this.publish();
  }

  private stopRinging(): void {
    if (this.timer) { clearInterval(this.timer); this.timer = undefined; }
    const wasRinging = this.ringing;
    this.ringing = false;
    if (wasRinging) this.publish();
  }

  /** One bell strike: a few detuned partials with an exponential decay. */
  private strike(): void {
    const ctx = this.ctx;
    if (!ctx) return;
    ctx.resume().catch(() => {});
    const t0 = ctx.currentTime + 0.02;
    const master = ctx.createGain();
    master.gain.setValueAtTime(0.32, t0);
    master.gain.exponentialRampToValueAtTime(0.0001, t0 + 1.6);
    master.connect(ctx.destination);
    for (const [ratio, amp] of [[1, 0.55], [2.76, 0.28], [5.4, 0.12]] as const) {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sine';
      osc.frequency.value = 660 * ratio;
      gain.gain.setValueAtTime(amp, t0);
      gain.gain.exponentialRampToValueAtTime(0.0001, t0 + 1.5);
      osc.connect(gain);
      gain.connect(master);
      osc.start(t0);
      osc.stop(t0 + 1.6);
    }
  }

  private notify(tickets: KitchenTicketDto[]): void {
    if (typeof Notification === 'undefined' || Notification.permission !== 'granted') return;
    for (const ticket of tickets) {
      try {
        const n = new Notification(`New order #${ticket.orderNumber}`, {
          body: `${tickets.length > 1 ? tickets.length + ' orders waiting. ' : ''}Accept it in the Kitchen Display.`,
          tag: `kitchen-ticket-${ticket.id}`,
        } as NotificationOptions);
        n.onclick = () => { window.focus(); n.close(); };
      } catch { /* notification unsupported — the bell and banner still alert */ }
    }
  }

  /** Mirror the bell state onto <body> so automated checks can assert it. */
  private publish(): void {
    if (typeof document !== 'undefined') document.body.dataset['kitchenBell'] = this.state;
  }
}
