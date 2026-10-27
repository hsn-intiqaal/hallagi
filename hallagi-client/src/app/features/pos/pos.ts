import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService, ApiFailure } from '@core/http/api.service';
import { AppointmentsApi } from '@core/api/appointments.api';
import { SalesApi } from '@core/api/sales.api';
import { AuthService } from '@core/auth/auth.service';
import { BranchContext } from '@core/state/branch-context';
import { ToastService } from '@core/state/toast.service';
import { calculatePrice } from '@core/pricing/pricing';
import { Appointment, PaymentMethod, PosBarber, PosCustomer, PosData, PosProduct, PosService, Sale } from '@core/models';
import { Icon } from '@shared/ui/icon';
import { SarPipe, DateTimeArPipe } from '@shared/pipes/format.pipes';
import { LoadingRow } from '@shared/ui/ui';

type CatalogTab = 'services' | 'products';

interface CartLine {
  kind: 'Service' | 'Product';
  id: string;
  name: string;
  price: number;
  qty: number;
  /** Products only: units available in this branch. */
  max?: number;
}

const lineKey = (l: Pick<CartLine, 'kind' | 'id'>) => `${l.kind}:${l.id}`;

@Component({
  selector: 'app-pos',
  standalone: true,
  imports: [FormsModule, Icon, SarPipe, DateTimeArPipe, LoadingRow, NgIf, NgFor],
  templateUrl: './pos.html',
  styleUrl: './pos.scss'
})
export class Pos implements OnInit {
  private api = inject(ApiService);
  private salesApi = inject(SalesApi);
  private appointmentsApi = inject(AppointmentsApi);
  readonly auth = inject(AuthService);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);
  private route = inject(ActivatedRoute);
  readonly router = inject(Router);

  readonly branchId = signal<string | null>(null);
  readonly data = signal<PosData | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly tab = signal<CatalogTab>('services');
  readonly cart = signal<CartLine[]>([]);
  readonly barberId = signal<string>('');
  readonly customerId = signal<string | null>(null);
  readonly payment = signal<PaymentMethod>('Cash');
  readonly completedSale = signal<Sale | null>(null);

  /** Checkout of a booked / walk-in appointment. */
  readonly appointment = signal<Appointment | null>(null);

  // ===== discounts =====
  readonly showDiscounts = signal(false);
  readonly couponCode = signal<string | null>(null);
  readonly couponDiscount = signal(0);
  readonly manualDiscount = signal(0);
  readonly redeemPoints = signal(0);
  couponInput = '';
  checkingCoupon = false;

  /** Barber-operated POS mode: the authenticated barber is preselected and fixed. */
  readonly selfBarberMode = signal(false);
  readonly linkedBarberId = signal<string | null>(null);

  readonly gross = computed(() => this.cart().reduce((sum, l) => sum + l.price * l.qty, 0));
  readonly cartCount = computed(() => this.cart().reduce((n, l) => n + l.qty, 0));
  readonly pointsValue = computed(() => this.redeemPoints() * (this.data()?.pricing.loyaltyPointValue ?? 0));
  readonly price = computed(() => {
    const p = this.data()?.pricing;
    const discount = this.couponDiscount() + this.manualDiscount() + this.pointsValue();
    return calculatePrice(this.gross(), discount, p?.vatEnabled ?? false, p?.vatRate ?? 0, p?.pricesIncludeVat ?? true);
  });
  readonly maxManualDiscount = computed(() =>
    Math.round(this.gross() * (this.data()?.pricing.maxManualDiscountPercent ?? 0)) / 100);

  customerSearch = '';
  productSearch = '';

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
        error: (e: ApiFailure) => { this.loading.set(false); this.toast.error(e.message ?? 'تعذّر تحميل بياناتك'); }
      });
      return;
    }

    const appointmentId = this.route.snapshot.queryParamMap.get('appointmentId');
    if (appointmentId) {
      this.appointmentsApi.get(appointmentId).subscribe({
        next: a => {
          this.appointment.set(a);
          this.branchId.set(a.branchId);
          this.loadPosData(a.branchId);
        },
        error: (e: ApiFailure) => { this.toast.error(e.message); this.router.navigate(['/appointments']); }
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
    this.salesApi.posData(id).subscribe({
      next: d => {
        this.data.set(d);
        const cash = d.payment.methods.find(m => m.value === 'Cash' && m.enabled);
        const card = d.payment.methods.find(m => m.value === 'Card' && m.enabled);
        if (!cash && card) this.payment.set('Card');
        this.applyAppointment(d);
        this.loading.set(false);
      },
      error: (e: ApiFailure) => { this.toast.error(e.message ?? 'تعذّر تحميل بيانات شاشة البيع'); this.loading.set(false); }
    });
  }

  /** Prefills barber, customer and services from the appointment being checked out. */
  private applyAppointment(d: PosData): void {
    const a = this.appointment();
    if (!a) return;
    if (a.barberId && d.barbers.some(b => b.id === a.barberId)) this.barberId.set(a.barberId);
    if (a.customerId) {
      if (!d.customers.some(c => c.id === a.customerId))
        d.customers.unshift({ id: a.customerId, fullName: a.customerName, phone: a.customerPhone ?? undefined });
      this.customerId.set(a.customerId);
    }
    this.cart.set(a.items
      .map(i => d.services.find(s => s.id === i.serviceId))
      .filter((s): s is PosService => !!s)
      .map(s => ({ kind: 'Service' as const, id: s.id, name: s.name, price: s.price, qty: 1 })));
  }

  // ===== catalog =====

  cartQty(kind: CartLine['kind'], id: string): number {
    return this.cart().find(l => lineKey(l) === lineKey({ kind, id }))?.qty ?? 0;
  }

  filteredProducts(): PosProduct[] {
    const s = this.productSearch.trim();
    const list = this.data()?.products ?? [];
    return s ? list.filter(p => p.name.includes(s) || p.barcode === s) : list;
  }

  addService(service: PosService): void {
    this.addLine({ kind: 'Service', id: service.id, name: service.name, price: service.price, qty: 1 });
  }

  addProduct(product: PosProduct): void {
    if (this.cartQty('Product', product.id) >= product.stock) {
      this.toast.error(`الكمية المتوفرة من «${product.name}» ${product.stock} فقط`);
      return;
    }
    this.addLine({ kind: 'Product', id: product.id, name: product.name, price: product.price, qty: 1, max: product.stock });
  }

  /** Barcode scanners type the code and press Enter. */
  scanBarcode(): void {
    const code = this.productSearch.trim();
    const product = this.data()?.products.find(p => p.barcode && p.barcode === code);
    if (!product) return;
    this.addProduct(product);
    this.productSearch = '';
  }

  private addLine(line: CartLine): void {
    this.cart.update(lines => {
      const key = lineKey(line);
      return lines.some(l => lineKey(l) === key)
        ? lines.map(l => (lineKey(l) === key ? { ...l, qty: l.qty + 1 } : l))
        : [...lines, line];
    });
    this.resetCoupon();
  }

  setQty(line: CartLine, qty: number): void {
    if (qty <= 0) { this.remove(line); return; }
    if (line.max !== undefined && qty > line.max) {
      this.toast.error(`الكمية المتوفرة ${line.max} فقط`);
      return;
    }
    this.cart.update(lines => lines.map(l => (lineKey(l) === lineKey(line) ? { ...l, qty } : l)));
    this.resetCoupon();
  }

  remove(line: CartLine): void {
    this.cart.update(lines => lines.filter(l => lineKey(l) !== lineKey(line)));
    this.resetCoupon();
  }

  clear(): void {
    this.cart.set([]);
    this.customerId.set(null);
    this.customerSearch = '';
    this.appointment.set(null);
    this.resetDiscounts();
  }

  // ===== people =====

  selectBarber(b: PosBarber): void {
    this.barberId.set(b.id);
  }

  /** The signed-in barber's own profile (barber-operated POS). */
  selfBarber(): PosBarber | null {
    return this.data()?.barbers.find(b => b.id === this.barberId()) ?? null;
  }

  selectCustomer(c: PosCustomer): void {
    this.customerId.set(c.id);
    this.customerSearch = '';
    this.redeemPoints.set(0);
  }

  clearCustomer(): void {
    this.customerId.set(null);
    this.redeemPoints.set(0);
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

  // ===== discounts =====

  applyCoupon(): void {
    const code = this.couponInput.trim();
    if (!code || this.gross() <= 0) return;
    this.checkingCoupon = true;
    this.salesApi.previewCoupon(code, this.gross()).subscribe({
      next: res => {
        this.checkingCoupon = false;
        this.couponCode.set(res.code);
        this.couponDiscount.set(res.discount);
        this.toast.success(`تم تطبيق الكود ${res.code}`);
      },
      error: (e: ApiFailure) => { this.checkingCoupon = false; this.resetCoupon(); this.toast.error(e.message); }
    });
  }

  /** Cart changes invalidate a coupon's computed value — the cashier re-applies it. */
  resetCoupon(): void {
    if (!this.couponCode()) return;
    this.couponCode.set(null);
    this.couponDiscount.set(0);
  }

  setManualDiscount(value: number | null): void {
    const v = Math.max(0, Number(value) || 0);
    this.manualDiscount.set(Math.min(v, this.maxManualDiscount()));
  }

  canRedeem(): boolean {
    const p = this.data()?.pricing;
    const c = this.selectedCustomer();
    return !!p?.loyaltyEnabled && !!c && (c.loyaltyPoints ?? 0) >= p.loyaltyMinRedeem;
  }

  toggleRedeemAll(): void {
    const c = this.selectedCustomer();
    const p = this.data()?.pricing;
    if (!c || !p) return;
    if (this.redeemPoints() > 0) { this.redeemPoints.set(0); return; }
    // never redeem more value than the remaining bill
    const remaining = this.gross() - this.couponDiscount() - this.manualDiscount();
    const affordable = Math.floor(remaining / p.loyaltyPointValue);
    const points = Math.max(0, Math.min(c.loyaltyPoints ?? 0, affordable));
    if (points < p.loyaltyMinRedeem) {
      this.toast.error(`الحد الأدنى للاستبدال ${p.loyaltyMinRedeem} نقطة — قيمة الفاتورة لا تكفي`);
      return;
    }
    this.redeemPoints.set(points);
  }

  private resetDiscounts(): void {
    this.couponCode.set(null);
    this.couponDiscount.set(0);
    this.manualDiscount.set(0);
    this.redeemPoints.set(0);
    this.couponInput = '';
    this.showDiscounts.set(false);
  }

  // ===== save =====

  save(): void {
    if (this.saving()) return; // guard against double-submit
    const barberId = this.barberId();
    const branchId = this.branchId();
    if (!barberId) { this.toast.error('اختر الحلاق لإكمال البيع'); return; }
    if (this.cart().length === 0) { this.toast.error('أضف خدمة واحدة على الأقل لإتمام البيع'); return; }
    if (!branchId) return;

    this.saving.set(true);
    this.salesApi.create({
      branchId,
      barberId,
      customerId: this.customerId(),
      paymentMethod: this.payment(),
      // Single-use idempotency key per save attempt prevents duplicate sales on retries
      idempotencyKey: crypto.randomUUID(),
      items: this.cart().map(l => ({
        serviceId: l.kind === 'Service' ? l.id : null,
        productId: l.kind === 'Product' ? l.id : null,
        quantity: l.qty
      })),
      couponCode: this.couponCode(),
      manualDiscount: this.manualDiscount() || null,
      redeemPoints: this.redeemPoints(),
      appointmentId: this.appointment()?.id ?? null
    }).subscribe({
      next: res => {
        this.saving.set(false);
        this.completedSale.set(res.sale);
        this.clear();
        if (!this.selfBarberMode()) this.barberId.set('');
        this.toast.success('تم تسجيل البيع بنجاح');
        // refresh stock and loyalty balances for the next sale
        this.loadPosData(branchId);
      },
      error: (e: ApiFailure) => {
        this.saving.set(false);
        this.toast.error(e.message ?? 'لم نتمكن من حفظ العملية. تحقق من الاتصال وحاول مرة أخرى');
      }
    });
  }

  /** Phones: jump from the catalog to the cart/payment panel. */
  scrollToCart(): void {
    document.querySelector('.cart-panel')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  viewReceipt(sale: Sale): void {
    this.router.navigate(['/sales', sale.id]);
  }

  newSale(): void {
    this.completedSale.set(null);
    if (this.route.snapshot.queryParamMap.has('appointmentId'))
      this.router.navigate([], { queryParams: {}, replaceUrl: true });
  }

  terminate(): void {
    this.completedSale.set(null);
    this.router.navigate(['/dashboard']);
  }
}
