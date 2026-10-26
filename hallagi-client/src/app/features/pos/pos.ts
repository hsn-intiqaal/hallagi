import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '@core/http/api.service';
import { AuthService } from '@core/auth/auth.service';
import { BranchContext } from '@core/state/branch-context';
import { ToastService } from '@core/state/toast.service';
import { PosData, PaymentMethod, Sale, PosBarber, PosCustomer, PosService } from '@core/models';
import { Icon } from '@shared/ui/icon';
import { SarPipe, DateTimeArPipe } from '@shared/pipes/format.pipes';
import { LoadingRow } from '@shared/ui/ui';

interface CartLine { service: PosService; qty: number; }

@Component({
  selector: 'app-pos',
  standalone: true,
  imports: [FormsModule, Icon, SarPipe, DateTimeArPipe, LoadingRow, NgIf, NgFor],
  templateUrl: './pos.html',
  styleUrl: './pos.scss'
})
export class Pos implements OnInit {
  private api = inject(ApiService);
  readonly auth = inject(AuthService);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);
  readonly router = inject(Router);

  readonly branchId = signal<string | null>(null);
  readonly data = signal<PosData | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly cart = signal<CartLine[]>([]);
  readonly barberId = signal<string>('');
  readonly customerId = signal<string | null>(null);
  readonly payment = signal<PaymentMethod>('Cash');
  readonly completedSale = signal<Sale | null>(null);

  /** Barber-operated POS mode: the authenticated barber is preselected and fixed. */
  readonly selfBarberMode = signal(false);
  readonly linkedBarberId = signal<string | null>(null);

  readonly total = computed(() => this.cart().reduce((sum, l) => sum + l.service.price * l.qty, 0));
  readonly cartCount = computed(() => this.cart().reduce((n, l) => n + l.qty, 0));

  customerSearch = '';

  ngOnInit(): void {
    // Barber-operated POS: identify the barber from the authenticated session
    if (this.auth.role() === 'Barber') {
      this.selfBarberMode.set(true);
      this.api.get<any>('/me/summary').subscribe({
        next: (s: any) => {
          this.linkedBarberId.set(s?.barber?.barber?.id ?? null);
          this.barberId.set(s?.barber?.barber?.id ?? '');
          const branchId = s?.branchIds?.[0] ?? this.branches.scopeId();
          if (branchId) {
            this.branchId.set(branchId);
            this.loadPosData(branchId);
          } else {
            this.loading.set(false);
          }
        },
        error: (e: any) => { this.loading.set(false); this.toast.error(e?.message ?? 'تعذّر تحميل بياناتك'); }
      });
      return;
    }

    const id = this.branches.scopeId();
    if (!id) {
      this.toast.error('اختر فرعًا محددًا لتسجيل عملية بيع');
      this.router.navigate(['/dashboard']);
      return;
    }
    this.branchId.set(id);
    this.loadPosData(id);
  }

  loadPosData(id: string): void {
    this.loading.set(true);
    this.api.get<PosData>('/pos/data', { branchId: id }).subscribe({
      next: d => {
        this.data.set(d);
        const cash = d.payment.methods.find(m => m.value === 'Cash' && m.enabled);
        const card = d.payment.methods.find(m => m.value === 'Card' && m.enabled);
        if (!cash && card) this.payment.set('Card');
        this.loading.set(false);
      },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل بيانات شاشة البيع'); this.loading.set(false); }
    });
  }

  cartQty(serviceId: string): number {
    return this.cart().find(l => l.service.id === serviceId)?.qty ?? 0;
  }

  /** The signed-in barber's own profile (barber-operated POS). */
  selfBarber(): PosBarber | null {
    return this.data()?.barbers.find(b => b.id === this.barberId()) ?? null;
  }

  add(service: PosService): void {    this.cart.update(lines => {
      const found = lines.find(l => l.service.id === service.id);
      if (found) return lines.map(l => l.service.id === service.id ? { ...l, qty: l.qty + 1 } : l);
      return [...lines, { service, qty: 1 }];
    });
  }

  setQty(serviceId: string, qty: number): void {
    if (qty <= 0) { this.remove(serviceId); return; }
    this.cart.update(lines => lines.map(l => l.service.id === serviceId ? { ...l, qty } : l));
  }

  remove(serviceId: string): void {
    this.cart.update(lines => lines.filter(l => l.service.id !== serviceId));
  }

  clear(): void {
    this.cart.set([]);
    this.customerId.set(null);
    this.customerSearch = '';
  }

  selectBarber(b: PosBarber): void {
    this.barberId.set(b.id);
  }

  selectCustomer(c: PosCustomer): void {
    this.customerId.set(c.id);
    this.customerSearch = '';
  }

  clearCustomer(): void {
    this.customerId.set(null);
  }

  filteredCustomers(): PosCustomer[] {
    const s = this.customerSearch.trim();
    const list = this.data()?.customers ?? [];
    if (!s) return [];
    return list.filter(c => c.fullName.includes(s) || (c.phone ?? '').includes(s)).slice(0, 6);
  }

  selectedCustomer(): PosCustomer | null {
    return this.data()?.customers.find(c => c.id === this.customerId()) ?? null;
  }

  save(): void {
    if (this.saving()) return; // guard against double-submit
    const barberId = this.barberId();
    const branchId = this.branchId();
    if (!barberId) { this.toast.error('اختر الحلاق لإكمال البيع'); return; }
    if (this.cart().length === 0) { this.toast.error('أضف خدمة واحدة على الأقل لإتمام البيع'); return; }
    if (!branchId) return;

    this.saving.set(true);
    // Single-use idempotency key per save attempt prevents duplicate sales on retries
    const idempotencyKey = crypto.randomUUID();
    this.api.post<any>('/sales', {
      branchId,
      barberId,
      customerId: this.customerId(),
      paymentMethod: this.payment(),
      idempotencyKey,
      items: this.cart().map(l => ({ serviceId: l.service.id, quantity: l.qty }))
    }).subscribe({
      next: (res: any) => {
        this.saving.set(false);
        this.completedSale.set(res.sale as Sale);
        this.clear();
        this.barberId.set('');
        this.toast.success('تم تسجيل البيع بنجاح');
      },
      error: (e: any) => {
        this.saving.set(false);
        this.toast.error(e?.message ?? 'لم نتمكن من حفظ العملية. تحقق من الاتصال وحاول مرة أخرى');
      }
    });
  }

  viewReceipt(sale: Sale): void {
    this.router.navigate(['/sales', sale.id]);
  }

  newSale(): void {
    this.completedSale.set(null);
  }

  terminate(): void {
    this.completedSale.set(null);
    this.router.navigate(['/dashboard']);
  }

  printReceipt(): void {
    window.print();
  }
}
