import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '@core/http/api.service';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/state/toast.service';
import { ThemeService } from '@core/state/theme.service';
import { BookingSettings, LoyaltySettings, Settings as SettingsModel, TaxSettings } from '@core/models';
import { Icon } from '@shared/ui/icon';
import { SarPipe } from '@shared/pipes/format.pipes';
import { LoadingRow } from '@shared/ui/ui';
import { resizeImageFile } from '@shared/ui/image-upload';

interface Palette { name: string; primary: string; secondary: string; }

const DEFAULT_PRIMARY = '#2563EB';
const DEFAULT_SECONDARY = '#173653';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, Icon, SarPipe, LoadingRow],
  templateUrl: './settings.html',
  styleUrl: './settings.scss'
})
export class Settings implements OnInit {
  private api = inject(ApiService);
  readonly auth = inject(AuthService);
  private toast = inject(ToastService);
  private theme = inject(ThemeService);

  readonly loading = signal(true);
  readonly canManage = signal(false);
  readonly subscription = signal<any>(null);
  readonly plans = signal<any[]>([]);
  readonly savingPlan = signal(false);
  readonly logoUrl = signal<string | null>(null);
  readonly coverUrl = signal<string | null>(null);
  /** Empty string clears the stored image (null means "unchanged" on the API). */
  readonly logoRemoved = signal(false);
  readonly coverRemoved = signal(false);
  form = {
    organizationName: '', currency: 'SAR', timeZone: 'Asia/Riyadh',
    enableCash: true, enableCard: true, receiptFooter: '', receiptPhone: '',
    primaryColor: DEFAULT_PRIMARY, secondaryColor: DEFAULT_SECONDARY
  };
  password = { current: '', next: '' };

  tax: TaxSettings = {
    vatEnabled: false, vatRate: 0.15, pricesIncludeVat: true,
    vatNumber: null, commercialRegistration: null, legalName: null, taxAddress: null
  };
  loyalty: LoyaltySettings = { enabled: false, pointsPerSar: 1, pointValue: 0.05, minRedeem: 100 };
  booking: BookingSettings = { enabled: false, slug: null, slotMinutes: 15, maxDaysAhead: 14 };
  maxManualDiscountPercent = 30;

  readonly palettes: Palette[] = [
    { name: 'كحلي كلاسيكي', primary: '#2563EB', secondary: '#173653' },
    { name: 'ذهبي ملكي', primary: '#C79A3B', secondary: '#2A2113' },
    { name: 'زمردي', primary: '#0E9F6E', secondary: '#0B3B2E' },
    { name: 'عنابي', primary: '#BE123C', secondary: '#3F0D1F' },
    { name: 'برتقالي', primary: '#EA580C', secondary: '#431407' },
    { name: 'بنفسجي', primary: '#7C3AED', secondary: '#2E1065' },
    { name: 'فيروزي', primary: '#0D9488', secondary: '#134E4A' }
  ];

  ngOnInit(): void {
    this.canManage.set(this.auth.can('settings.manage'));
    this.api.get<any>('/subscription').subscribe({
      next: s => this.subscription.set(s),
      error: () => this.subscription.set(null)
    });
    this.api.get<any>('/subscription/plans').subscribe({
      next: (res: any) => this.plans.set(res),
      error: () => this.plans.set([])
    });
    this.api.get<SettingsModel>('/settings').subscribe({
      next: s => {
        this.form = {
          organizationName: s.organization?.name ?? '',
          currency: s.organization?.currency ?? 'SAR',
          timeZone: s.organization?.timeZone ?? 'Asia/Riyadh',
          enableCash: s.settings.enableCash,
          enableCard: s.settings.enableCard,
          receiptFooter: s.settings.receiptFooter ?? '',
          receiptPhone: s.settings.receiptPhone ?? '',
          primaryColor: s.organization?.primaryColor ?? DEFAULT_PRIMARY,
          secondaryColor: s.organization?.secondaryColor ?? DEFAULT_SECONDARY
        };
        this.tax = { ...s.tax };
        this.loyalty = { ...s.loyalty };
        this.booking = { ...s.booking };
        this.maxManualDiscountPercent = s.settings.maxManualDiscountPercent;
        this.logoUrl.set(s.organization?.logoUrl ?? null);
        this.coverUrl.set(s.organization?.coverImageUrl ?? null);
        this.loading.set(false);
      },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل الإعدادات'); this.loading.set(false); }
    });
  }

  applyPreset(p: Palette): void {
    this.form.primaryColor = p.primary;
    this.form.secondaryColor = p.secondary;
  }

  /** Placeholder gradient for the cover picker before an image is chosen. */
  coverGradient(): string {
    const pri = this.form.primaryColor && /^#([0-9a-fA-F]{6})$/.test(this.form.primaryColor)
      ? this.form.primaryColor : DEFAULT_PRIMARY;
    return `linear-gradient(135deg, ${pri}, ${this.theme.darken(pri, 0.3)})`;
  }

  removeLogo(): void { this.logoUrl.set(null); this.logoRemoved.set(true); }
  removeCover(): void { this.coverUrl.set(null); this.coverRemoved.set(true); }

  async pickImage(kind: 'logo' | 'cover', event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (!file.type.startsWith('image/')) { this.toast.error('الملف المختار ليس صورة'); return; }
    try {
      const dataUrl = await resizeImageFile(file, kind === 'logo' ? 256 : 1280, kind === 'logo' ? 0.85 : 0.75);
      if (kind === 'logo') { this.logoUrl.set(dataUrl); this.logoRemoved.set(false); }
      else { this.coverUrl.set(dataUrl); this.coverRemoved.set(false); }
    } catch {
      this.toast.error('تعذّر معالجة الصورة، جرّب صورة أخرى');
    }
  }

  /** Saudi VAT registration: 15 digits starting and ending with 3. */
  vatNumberValid(): boolean {
    return /^3\d{13}3$/.test(this.tax.vatNumber ?? '');
  }

  /** 100 points × 0.05 = 5 SAR → shown as a percentage of spend returned to the customer. */
  loyaltyReturnPercent(): number {
    return Math.round(this.loyalty.pointsPerSar * this.loyalty.pointValue * 1000) / 10;
  }

  subscriptionStatus(): string {
    const labels: Record<string, string> = { Active: 'نشط', Trial: 'فترة تجريبية', PastDue: 'متأخر السداد', Cancelled: 'ملغى' };
    const status = this.subscription()?.status as string | undefined;
    return status ? labels[status] ?? status : '—';
  }

  bookingUrl(): string | null {
    return this.booking.slug ? `${location.origin}/book/${this.booking.slug}` : null;
  }

  copyBookingUrl(): void {
    const url = this.bookingUrl();
    if (!url) return;
    navigator.clipboard?.writeText(url).then(
      () => this.toast.success('تم نسخ رابط الحجز'),
      () => this.toast.info(url)
    );
  }

  save(): void {
    if (!this.form.organizationName.trim()) { this.toast.error('اسم المنشأة مطلوب'); return; }
    if (this.tax.vatEnabled && !this.vatNumberValid()) {
      this.toast.error('الرقم الضريبي يجب أن يكون 15 رقمًا يبدأ وينتهي بالرقم 3');
      return;
    }
    this.api.put('/settings', {
      organizationName: this.form.organizationName.trim(),
      currency: this.form.currency,
      timeZone: this.form.timeZone,
      logoUrl: this.logoRemoved() ? '' : this.logoUrl(),
      enableCash: this.form.enableCash,
      enableCard: this.form.enableCard,
      receiptFooter: this.form.receiptFooter?.trim() || null,
      receiptPhone: this.form.receiptPhone?.trim() || null,
      coverImageUrl: this.coverRemoved() ? '' : this.coverUrl(),
      primaryColor: this.form.primaryColor,
      secondaryColor: this.form.secondaryColor,
      tax: { ...this.tax, vatRate: Number(this.tax.vatRate) },
      loyalty: this.loyalty,
      booking: { ...this.booking, slug: this.booking.slug?.trim().toLowerCase() || null },
      maxManualDiscountPercent: Number(this.maxManualDiscountPercent)
    }).subscribe({
      next: () => {
        this.toast.success('تم حفظ الإعدادات بنجاح');
        this.auth.refreshMe();
        // the server may have generated a booking slug
        this.api.get<SettingsModel>('/settings').subscribe({ next: s => (this.booking = { ...s.booking }) });
      },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر حفظ الإعدادات')
    });
  }

  changePassword(): void {
    if (this.password.next.length < 6) { this.toast.error('كلمة المرور الجديدة يجب أن تكون 6 أحرف على الأقل'); return; }
    this.api.post('/auth/change-password', { currentPassword: this.password.current, newPassword: this.password.next }).subscribe({
      next: () => { this.toast.success('تم تغيير كلمة المرور بنجاح'); this.password = { current: '', next: '' }; },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر تغيير كلمة المرور')
    });
  }

  changePlan(planId: string): void {
    this.savingPlan.set(true);
    this.api.post('/subscription/change-plan', { planId }).subscribe({
      next: () => {
        this.savingPlan.set(false);
        this.toast.success('تم تغيير الخطة بنجاح');
        this.api.get<any>('/subscription').subscribe({ next: s => this.subscription.set(s) });
      },
      error: (e: any) => { this.savingPlan.set(false); this.toast.error(e?.message ?? 'تعذّر تغيير الخطة'); }
    });
  }
}
