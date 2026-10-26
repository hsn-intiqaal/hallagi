import { NgFor, NgIf } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { BookingApi } from '@core/api/booking.api';
import { ApiFailure } from '@core/http/api.service';
import { PublicBookingInfo, PublicBookingResult } from '@core/models';
import { SarPipe } from '@shared/pipes/format.pipes';
import { Icon } from '@shared/ui/icon';
import { LoadingRow } from '@shared/ui/ui';

type Step = 'services' | 'time' | 'details' | 'done';

const isoDate = (d: Date): string =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

/** Customer-facing online booking — anonymous, mobile-first, branded with the salon colors. */
@Component({
  selector: 'app-public-booking',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, Icon, SarPipe, LoadingRow],
  templateUrl: './public-booking.html',
  styleUrl: './public-booking.scss'
})
export class PublicBooking implements OnInit {
  private bookingApi = inject(BookingApi);
  private route = inject(ActivatedRoute);

  readonly slug = this.route.snapshot.paramMap.get('slug') ?? '';
  readonly info = signal<PublicBookingInfo | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal<string | null>(null);

  readonly step = signal<Step>('services');
  readonly branchId = signal<string | null>(null);
  readonly serviceIds = signal<string[]>([]);
  readonly barberId = signal<string | null>(null);
  readonly date = signal<string>('');
  readonly time = signal<string | null>(null);
  readonly slots = signal<string[]>([]);
  readonly slotsLoading = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<PublicBookingResult | null>(null);

  name = '';
  phone = '';
  notes = '';

  // cancel-a-booking panel
  readonly showCancel = signal(false);
  cancelRef = '';
  cancelPhone = '';
  readonly cancelMessage = signal<string | null>(null);

  readonly branch = computed(() => this.info()?.branches.find(b => b.id === this.branchId()) ?? null);
  readonly selectedServices = computed(() => (this.info()?.services ?? []).filter(s => this.serviceIds().includes(s.id)));
  readonly total = computed(() => this.selectedServices().reduce((t, s) => t + s.price, 0));
  readonly duration = computed(() => this.selectedServices().reduce((t, s) => t + s.durationMinutes, 0));
  readonly branchBarbers = computed(() => (this.info()?.barbers ?? []).filter(b => b.branchIds.includes(this.branchId() ?? '')));
  readonly days = computed(() => {
    const info = this.info();
    if (!info) return [];
    const start = new Date(info.today + 'T00:00:00');
    return Array.from({ length: info.maxDaysAhead + 1 }, (_, i) => {
      const d = new Date(start);
      d.setDate(d.getDate() + i);
      return {
        value: isoDate(d),
        weekday: i === 0 ? 'اليوم' : i === 1 ? 'غدًا' : d.toLocaleDateString('ar-SA-u-ca-gregory', { weekday: 'short' }),
        day: d.getDate(),
        month: d.toLocaleDateString('ar-SA-u-nu-latn-ca-gregory', { month: 'short' })
      };
    });
  });
  readonly brand = computed(() => this.info()?.organization.primaryColor || '#2563EB');

  ngOnInit(): void {
    this.bookingApi.info(this.slug).subscribe({
      next: info => {
        this.info.set(info);
        this.date.set(info.today);
        if (info.branches.length === 1) this.branchId.set(info.branches[0].id);
        this.loading.set(false);
        document.title = `احجز موعدك — ${info.organization.name}`;
      },
      error: (e: ApiFailure) => { this.notFound.set(e.message); this.loading.set(false); }
    });
  }

  selectBranch(id: string): void {
    this.branchId.set(id);
    this.barberId.set(null);
  }

  toggleService(id: string): void {
    this.serviceIds.update(ids => (ids.includes(id) ? ids.filter(x => x !== id) : [...ids, id]));
  }

  goToTime(): void {
    if (!this.branchId() || this.serviceIds().length === 0) return;
    this.step.set('time');
    this.loadSlots();
  }

  selectBarber(id: string | null): void {
    this.barberId.set(id);
    this.loadSlots();
  }

  selectDate(value: string): void {
    this.date.set(value);
    this.loadSlots();
  }

  loadSlots(): void {
    const branchId = this.branchId();
    if (!branchId) return;
    this.time.set(null);
    this.slotsLoading.set(true);
    this.bookingApi.slots(this.slug, branchId, this.date(), this.serviceIds(), this.barberId()).subscribe({
      next: res => { this.slots.set(res.slots.map(s => s.time)); this.slotsLoading.set(false); },
      error: (e: ApiFailure) => { this.slots.set([]); this.slotsLoading.set(false); this.error.set(e.message); }
    });
  }

  selectTime(t: string): void {
    this.time.set(t);
    this.error.set(null);
    this.step.set('details');
  }

  book(): void {
    const branchId = this.branchId();
    const time = this.time();
    if (!branchId || !time || this.submitting()) return;
    if (this.name.trim().length < 2) { this.error.set('اكتب اسمك'); return; }
    if (!/^(05\d{8}|\+?9665\d{8})$/.test(this.phone.replace(/\s/g, ''))) { this.error.set('رقم الجوال غير صحيح — مثال: 05XXXXXXXX'); return; }

    this.submitting.set(true);
    this.error.set(null);
    this.bookingApi.book(this.slug, {
      branchId, barberId: this.barberId(), serviceIds: this.serviceIds(), date: this.date(), time,
      name: this.name.trim(), phone: this.phone.trim(), notes: this.notes.trim() || null
    }).subscribe({
      next: res => { this.result.set(res); this.step.set('done'); this.submitting.set(false); },
      error: (e: ApiFailure) => {
        this.submitting.set(false);
        this.error.set(e.message);
        // the slot may have just been taken: refresh the times
        if (e.code === 'slot_taken') { this.step.set('time'); this.loadSlots(); }
      }
    });
  }

  restart(): void {
    this.serviceIds.set([]);
    this.time.set(null);
    this.result.set(null);
    this.step.set('services');
  }

  cancelBooking(): void {
    if (!this.cancelRef.trim() || !this.cancelPhone.trim()) return;
    this.bookingApi.cancel(this.slug, this.cancelRef.trim(), this.cancelPhone.trim()).subscribe({
      next: res => this.cancelMessage.set(res.message),
      error: (e: ApiFailure) => this.cancelMessage.set(e.message)
    });
  }
}
