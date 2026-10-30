import { Injectable, OnDestroy } from '@angular/core';
import { KitchenTicketDto } from '../models';

const MUTED_KEY = 'win_kitchen_bell_muted';

/** A ticket that has waited this long without acceptance triggers the siren. */
export const EMERGENCY_WAIT_MS = 5 * 60 * 1000;
/** This many tickets waiting at once escalates immediately. */
export const EMERGENCY_PENDING = 3;

/**
 * Kitchen order bell.
 *
 * Rings continuously while at least one ticket is still waiting for kitchen
 * acceptance (status "New") and falls silent once staff accept or progress it.
 * If a ticket is ignored for {@link EMERGENCY_WAIT_MS}, or {@link EMERGENCY_PENDING}
 * are waiting at once, the bell escalates to a two-tone emergency siren and the
 * banner turns red. A desktop notification is raised for each new ticket. The
 * tones are synthesised with WebAudio so no audio asset ships with the app.
 */
@Injectable({ providedIn: 'root' })
export class KitchenBellService implements OnDestroy {
  pendingCount = 0;
  ringing = false;
  emergency = false;
  /** Seconds the oldest still-untouched ticket has been waiting. */
  oldestWaitSeconds = 0;
  muted = localStorage.getItem(MUTED_KEY) === '1';
  /** True once the browser allowed audio (requires a user gesture). */
  armed = false;

  private ctx?: AudioContext;
  private timer?: ReturnType<typeof setInterval>;
  private audioInterval?: number;
  private seen = new Set<number>();

  get state(): 'emergency' | 'ringing' | 'muted' | 'needs-permission' | 'silent' {
    if (this.ringing && this.emergency) return 'emergency';
    if (this.ringing) return 'ringing';
    if (this.pendingCount && this.muted) return 'muted';
    if (this.pendingCount && !this.armed) return 'needs-permission';
    return 'silent';
  }

  get emergencyActive(): boolean {
    return this.emergency;
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
    this.refreshAudio();
    this.publish();
  }

  toggleMuted(): void {
    this.muted = !this.muted;
    localStorage.setItem(MUTED_KEY, this.muted ? '1' : '0');
    this.refreshAudio();
    this.publish();
  }

  /** Feed every refreshed ticket list from the Kitchen Display. */
  update(tickets: KitchenTicketDto[], now = Date.now()): void {
    const pending = tickets.filter(t => t.status === 'New');
    const fresh = pending.filter(t => !this.seen.has(t.id));
    tickets.forEach(t => this.seen.add(t.id));
    this.pendingCount = pending.length;

    let oldest = 0;
    for (const ticket of pending) {
      // `createdAt` is typed Date but arrives from JSON as a string.
      const created = new Date(ticket.createdAt as string | number | Date).getTime();
      if (Number.isNaN(created)) continue;
      oldest = Math.max(oldest, Math.max(0, now - created));
    }
    this.oldestWaitSeconds = Math.round(oldest / 1000);

    const wasEmergency = this.emergency;
    this.emergency =
      this.pendingCount > 0 &&
      (this.pendingCount >= EMERGENCY_PENDING || oldest >= EMERGENCY_WAIT_MS);

    if (fresh.length) this.notify(fresh);
    if (this.emergency && !wasEmergency) this.notifyEmergency();
    this.refreshAudio();
    this.publish();
  }

  /** Manual sound check so staff can confirm the device is audible. */
  testRing(emergency = false): void {
    if (!this.armed) this.arm();
    if (emergency) {
      this.siren();
    } else {
      this.strike();
    }
  }

  stopAll(): void {
    this.clearAudio();
    this.pendingCount = 0;
    this.emergency = false;
    this.oldestWaitSeconds = 0;
    this.publish();
  }

  ngOnDestroy(): void {
    this.stopAll();
  }

  /**
   * Keep the speaker matched to the current state: siren during an
   * emergency, plain bell otherwise, and silence while muted, disarmed or
   * with nothing waiting. Muting only silences audio — an emergency keeps
   * its red banner so staff still see the warning.
   */
  private refreshAudio(): void {
    const audible = this.pendingCount > 0 && !this.muted && this.armed;
    if (!audible) { this.clearAudio(); this.publish(); return; }
    const play = () => (this.emergency ? this.siren() : this.strike());
    if (!this.ringing) { this.ringing = true; play(); }
    const interval = this.emergency ? 1400 : 2400;
    if (this.audioInterval === interval) return;
    this.clearTimer();
    this.audioInterval = interval;
    this.timer = setInterval(play, interval);
    this.publish();
  }

  private clearTimer(): void {
    if (this.timer) { clearInterval(this.timer); this.timer = undefined; }
    this.audioInterval = undefined;
  }

  private clearAudio(): void {
    this.clearTimer();
    this.ringing = false;
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

  /**
   * Emergency siren: an oscillator sweeping rapidly between two pitches with a
   * hard on/off rhythm, so it is unmistakable against the normal bell.
   */
  private siren(): void {
    const ctx = this.ctx;
    if (!ctx) return;
    ctx.resume().catch(() => {});
    const t0 = ctx.currentTime + 0.02;
    const duration = 1.2;
    const master = ctx.createGain();
    master.gain.setValueAtTime(0.0001, t0);
    master.gain.linearRampToValueAtTime(0.34, t0 + 0.03);
    master.gain.setValueAtTime(0.34, t0 + duration - 0.12);
    master.gain.linearRampToValueAtTime(0.0001, t0 + duration);
    master.connect(ctx.destination);

    const osc = ctx.createOscillator();
    osc.type = 'sawtooth';
    // Sweep the pitch up and down three times over the burst.
    const low = 520;
    const high = 1180;
    const steps = 6;
    for (let i = 0; i <= steps; i++) {
      const at = t0 + (duration * i) / steps;
      osc.frequency.setValueAtTime(i % 2 === 0 ? low : high, at);
    }
    osc.connect(master);
    osc.start(t0);
    osc.stop(t0 + duration);
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

  private notifyEmergency(): void {
    if (typeof Notification === 'undefined' || Notification.permission !== 'granted') return;
    try {
      const n = new Notification('KITCHEN EMERGENCY', {
        body: `${this.pendingCount} order(s) waiting, oldest ${Math.round(this.oldestWaitSeconds / 60)} min. Start cooking now.`,
        tag: 'kitchen-emergency',
      } as NotificationOptions);
      n.onclick = () => { window.focus(); n.close(); };
    } catch { /* notification unsupported — the siren and red banner still alert */ }
  }

  /** Mirror the bell state onto <body> so automated checks can assert it. */
  private publish(): void {
    if (typeof document !== 'undefined') document.body.dataset['kitchenBell'] = this.state;
  }
}
