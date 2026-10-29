import { NgFor, NgIf } from '@angular/common';
import { Component, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AppointmentsApi } from '@core/api/appointments.api';
import { AuthService } from '@core/auth/auth.service';
import { addDays } from '@core/time/local-date';
import { ApiFailure, ApiService } from '@core/http/api.service';
import { Appointment, AppointmentDay, AppointmentStatus, CustomerRow, PosService } from '@core/models';
import { BranchContext } from '@core/state/branch-context';
import { ToastService } from '@core/state/toast.service';
import { SarPipe } from '@shared/pipes/format.pipes';
import { Icon } from '@shared/ui/icon';
import { EmptyState, LoadingRow, Modal } from '@shared/ui/ui';

type View = 'schedule' | 'list';

interface BookingForm {
  id: string | null;
  walkIn: boolean;
  customerId: string | null;
  customerName: string;
  customerPhone: string;
  barberId: string | null;
  serviceIds: string[];
  date: string;
  time: string | null;
  notes: string;
}

/** Vertical scale of the day schedule. */
const PX_PER_MINUTE = 1.2;

const toMinutes = (hhmm: string): number => {
  const [h, m] = hhmm.split(':').map(Number);
  return h * 60 + m;
};

/** Minutes since midnight right now, on the salon's wall clock. */
const nowMinutesIn = (timeZone: string): number => {
  const [h, m] = new Intl.DateTimeFormat('en-GB', { timeZone, hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
    .format(new Date()).split(':').map(Number);
  return h * 60 + m;
};

@Component({
  selector: 'app-appointments',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, Icon, SarPipe, LoadingRow, Modal, EmptyState],
  templateUrl: './appointments.html',
  styleUrl: './appointments.scss'
})
export class Appointments implements OnInit {
  private appointmentsApi = inject(AppointmentsApi);
  private api = inject(ApiService);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);
  private router = inject(Router);
  readonly auth = inject(AuthService);

  readonly date = signal(this.auth.today());
  readonly day = signal<AppointmentDay | null>(null);
  readonly loading = signal(true);
  readonly view = signal<View>(window.innerWidth < 900 ? 'list' : 'schedule');
  readonly services = signal<PosService[]>([]);

  readonly selected = signal<Appointment | null>(null);
  readonly form = signal<BookingForm | null>(null);
  readonly slots = signal<{ time: string; freeBarbers: number }[]>([]);
  readonly slotsLoading = signal(false);
  readonly saving = signal(false);
  readonly customerResults = signal<CustomerRow[]>([]);

  /** Walk-in about to start: pick the barber. */
  readonly starting = signal<Appointment | null>(null);
  startBarberId: string | null = null;
  cancelReason = '';
  readonly cancelling = signal<Appointment | null>(null);

  readonly canManage = computed(() => this.auth.can('appointment.manage'));
  readonly branchId = computed(() => this.branches.scopeId() ?? this.branches.branches()[0]?.id ?? null);

  readonly hours = computed(() => {
    const d = this.day();
    if (!d) return [];
    const open = toMinutes(d.branch.opensAt);
    let close = toMinutes(d.branch.closesAt);
    if (close <= open) close += 24 * 60;
    const list: { label: string; top: number }[] = [];
    for (let m = Math.ceil(open / 60) * 60; m <= close; m += 60)
      list.push({ label: `${String((m / 60) % 24).padStart(2, '0')}:00`, top: (m - open) * PX_PER_MINUTE });
    return list;
  });
  readonly scheduleHeight = computed(() => {
    const d = this.day();
    if (!d) return 0;
    let span = toMinutes(d.branch.closesAt) - toMinutes(d.branch.opensAt);
    if (span <= 0) span += 24 * 60;
    return span * PX_PER_MINUTE;
  });

  constructor() {
    // Reload when the user switches branch from the top bar
    effect(() => {
      const id = this.branchId();
      if (id) untracked(() => this.load());
    });
  }

  ngOnInit(): void {
    this.api.get<{ items: PosService[] }>('/services').subscribe({
      next: res => this.services.set(res.items.filter(s => s.status === 'Active')),
      error: () => this.services.set([])
    });
  }

  load(): void {
    const branchId = this.branchId();
    if (!branchId) { this.loading.set(false); return; }
    this.loading.set(true);
    this.appointmentsApi.day(branchId, this.date()).subscribe({
      next: d => { this.day.set(d); this.loading.set(false); },
      error: (e: ApiFailure) => { this.toast.error(e.message); this.loading.set(false); }
    });
  }

  shiftDay(delta: number): void {
    this.date.set(addDays(this.date(), delta));
    this.load();
  }

  today(): void {
    this.date.set(this.auth.today());
    this.load();
  }

  setDate(value: string): void {
    if (!value) return;
    this.date.set(value);
    this.load();
  }

  isToday(): boolean { return this.date() === this.auth.today(); }

  // ===== schedule geometry =====

  barberAppointments(barberId: string): Appointment[] {
    return (this.day()?.appointments ?? []).filter(a => a.barberId === barberId && this.isActive(a));
  }

  unassigned(): Appointment[] {
    return (this.day()?.appointments ?? []).filter(a => !a.barberId && this.isActive(a));
  }

  closed(): Appointment[] {
    return (this.day()?.appointments ?? []).filter(a => !this.isActive(a));
  }

  private isActive(a: Appointment): boolean {
    return a.status !== 'Cancelled' && a.status !== 'NoShow';
  }

  top(a: Appointment): number {
    const open = toMinutes(this.day()!.branch.opensAt);
    let start = toMinutes(a.startTime);
    if (start < open) start += 24 * 60;
    return (start - open) * PX_PER_MINUTE;
  }

  height(a: Appointment): number {
    return Math.max(28, a.durationMinutes * PX_PER_MINUTE - 3);
  }

  nowLine(): number | null {
    const d = this.day();
    if (!d || !this.isToday()) return null;
    const open = toMinutes(d.branch.opensAt);
    const offset = nowMinutesIn(this.auth.timeZone()) - open;
    return offset >= 0 && offset * PX_PER_MINUTE <= this.scheduleHeight() ? offset * PX_PER_MINUTE : null;
  }

  statusClass(status: AppointmentStatus): string {
    return {
      Pending: 'st-pending', Confirmed: 'st-confirmed', Waiting: 'st-waiting', InService: 'st-service',
      Completed: 'st-done', Cancelled: 'st-cancelled', NoShow: 'st-cancelled'
    }[status];
  }

  serviceNames(a: Appointment): string {
    return a.items.map(i => i.serviceName).join(' + ');
  }

  // ===== actions =====

  open(a: Appointment): void { this.selected.set(a); }

  confirm(a: Appointment): void { this.changeStatus(a, 'Confirmed'); }

  startService(a: Appointment): void {
    if (a.barberId) { this.changeStatus(a, 'InService', null, a.barberId); return; }
    this.startBarberId = null;
    this.starting.set(a);
  }

  confirmStart(): void {
    const a = this.starting();
    if (!a || !this.startBarberId) { this.toast.error('اختر الحلاق'); return; }
    this.changeStatus(a, 'InService', null, this.startBarberId);
    this.starting.set(null);
  }

  noShow(a: Appointment): void { this.changeStatus(a, 'NoShow'); }

  askCancel(a: Appointment): void {
    this.cancelReason = '';
    this.cancelling.set(a);
  }

  confirmCancel(): void {
    const a = this.cancelling();
    if (!a) return;
    this.changeStatus(a, 'Cancelled', this.cancelReason.trim() || null);
    this.cancelling.set(null);
  }

  checkout(a: Appointment): void {
    this.router.navigate(['/pos'], { queryParams: { appointmentId: a.id } });
  }

  /** Opens WhatsApp with a ready reminder message (no paid SMS gateway needed). */
  whatsAppLink(a: Appointment): string | null {
    if (!a.whatsApp) return null;
    const branch = this.day()?.branch.name ?? '';
    const text = `مرحبًا ${a.customerName} 👋\nنذكّرك بموعدك في ${branch} يوم ${a.date} الساعة ${a.startTime}.\nرقم الحجز: ${a.reference}`;
    return `https://wa.me/${a.whatsApp}?text=${encodeURIComponent(text)}`;
  }

  private changeStatus(a: Appointment, status: AppointmentStatus, reason: string | null = null, barberId: string | null = null): void {
    this.appointmentsApi.setStatus(a.id, status, reason, barberId).subscribe({
      next: res => {
        this.toast.success(res.message);
        this.selected.set(null);
        this.load();
      },
      error: (e: ApiFailure) => this.toast.error(e.message)
    });
  }

  // ===== booking form =====

  newBooking(walkIn = false, barberId: string | null = null): void {
    this.form.set({
      id: null, walkIn, customerId: null, customerName: '', customerPhone: '',
      barberId, serviceIds: [], date: this.date(), time: null, notes: ''
    });
    this.slots.set([]);
    this.customerResults.set([]);
  }

  edit(a: Appointment): void {
    this.selected.set(null);
    this.form.set({
      id: a.id, walkIn: a.status === 'Waiting', customerId: a.customerId, customerName: a.customerName,
      customerPhone: a.customerPhone ?? '', barberId: a.barberId, serviceIds: a.items.map(i => i.serviceId),
      date: a.date, time: a.startTime, notes: a.notes ?? ''
    });
    this.loadSlots();
  }

  patchForm(patch: Partial<BookingForm>): void {
    this.form.update(f => (f ? { ...f, ...patch } : f));
    if ('serviceIds' in patch || 'barberId' in patch || 'date' in patch) {
      this.form.update(f => (f ? { ...f, time: null } : f));
      this.loadSlots();
    }
  }

  toggleService(id: string): void {
    const f = this.form();
    if (!f) return;
    const ids = f.serviceIds.includes(id) ? f.serviceIds.filter(x => x !== id) : [...f.serviceIds, id];
    this.patchForm({ serviceIds: ids });
  }

  formTotal(): number {
    const f = this.form();
    return f ? this.services().filter(s => f.serviceIds.includes(s.id)).reduce((t, s) => t + s.price, 0) : 0;
  }

  formDuration(): number {
    const f = this.form();
    return f ? this.services().filter(s => f.serviceIds.includes(s.id)).reduce((t, s) => t + (s.durationMinutes ?? 30), 0) : 0;
  }

  loadSlots(): void {
    const f = this.form();
    const branchId = this.branchId();
    if (!f || f.walkIn || !branchId || f.serviceIds.length === 0 || !f.date) { this.slots.set([]); return; }
    this.slotsLoading.set(true);
    this.appointmentsApi.slots(branchId, f.date, f.serviceIds, f.barberId, f.id).subscribe({
      next: res => {
        // keep the appointment's current time selectable while editing
        const list = res.slots;
        if (f.time && !list.some(s => s.time === f.time)) list.unshift({ time: f.time, startAt: '', freeBarbers: 1 });
        this.slots.set(list);
        this.slotsLoading.set(false);
      },
      error: (e: ApiFailure) => { this.slots.set([]); this.slotsLoading.set(false); this.toast.error(e.message); }
    });
  }

  searchCustomers(term: string): void {
    this.patchForm({ customerName: term, customerId: null });
    if (term.trim().length < 2) { this.customerResults.set([]); return; }
    this.api.get<{ items: CustomerRow[] }>('/customers', { search: term.trim(), pageSize: 6 }).subscribe({
      next: res => this.customerResults.set(res.items),
      error: () => this.customerResults.set([])
    });
  }

  pickCustomer(c: CustomerRow): void {
    this.form.update(f => (f ? { ...f, customerId: c.id, customerName: c.fullName, customerPhone: c.phone ?? '' } : f));
    this.customerResults.set([]);
  }

  saveForm(): void {
    const f = this.form();
    const branchId = this.branchId();
    if (!f || !branchId || this.saving()) return;
    if (!f.customerId && !f.customerName.trim()) { this.toast.error('اكتب اسم العميل'); return; }
    if (f.serviceIds.length === 0) { this.toast.error('اختر خدمة واحدة على الأقل'); return; }
    if (!f.walkIn && !f.time) { this.toast.error('اختر وقت الموعد'); return; }

    const body = {
      branchId,
      barberId: f.barberId,
      customerId: f.customerId,
      customerName: f.customerId ? null : f.customerName.trim(),
      customerPhone: f.customerId ? null : f.customerPhone.trim() || null,
      date: f.walkIn ? null : f.date,
      time: f.walkIn ? null : f.time,
      serviceIds: f.serviceIds,
      notes: f.notes.trim() || null,
      walkIn: f.walkIn
    };
    this.saving.set(true);
    const request$ = f.id ? this.appointmentsApi.update(f.id, body) : this.appointmentsApi.create(body);
    request$.subscribe({
      next: res => {
        this.saving.set(false);
        this.toast.success(res.message);
        this.form.set(null);
        if (!f.walkIn && f.date !== this.date()) this.date.set(f.date);
        this.load();
      },
      error: (e: ApiFailure) => { this.saving.set(false); this.toast.error(e.message); }
    });
  }
}
