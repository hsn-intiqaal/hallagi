import { NgFor, NgIf } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CouponsApi } from '@core/api/inventory.api';
import { ApiFailure } from '@core/http/api.service';
import { CouponInput, CouponRow, DiscountType } from '@core/models';
import { ToastService } from '@core/state/toast.service';
import { DateShortPipe, SarPipe } from '@shared/pipes/format.pipes';
import { Icon } from '@shared/ui/icon';
import { EmptyState, LoadingRow, Modal } from '@shared/ui/ui';

interface CouponForm extends Omit<CouponInput, 'description' | 'validFrom' | 'validTo'> {
  id: string | null;
  description: string;
  validFrom: string;
  validTo: string;
}

@Component({
  selector: 'app-coupons',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, Icon, SarPipe, DateShortPipe, LoadingRow, Modal, EmptyState],
  templateUrl: './coupons.html',
  styleUrl: './coupons.scss'
})
export class Coupons implements OnInit {
  private couponsApi = inject(CouponsApi);
  private toast = inject(ToastService);

  readonly items = signal<CouponRow[]>([]);
  readonly loading = signal(true);
  readonly form = signal<CouponForm | null>(null);
  readonly saving = signal(false);

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.couponsApi.list().subscribe({
      next: res => { this.items.set(res.items); this.loading.set(false); },
      error: (e: ApiFailure) => { this.toast.error(e.message); this.loading.set(false); }
    });
  }

  usage(c: CouponRow): string {
    return c.maxUses ? `${c.usedCount} / ${c.maxUses}` : `${c.usedCount}`;
  }

  isExpired(c: CouponRow): boolean {
    return !!c.validTo && new Date(c.validTo + 'T23:59:59') < new Date();
  }

  openAdd(): void {
    this.form.set({
      id: null, code: '', description: '', type: 'Percent', value: 10,
      minSubtotal: null, maxDiscount: null, maxUses: null, validFrom: '', validTo: '', active: true
    });
  }

  openEdit(c: CouponRow): void {
    this.form.set({
      id: c.id, code: c.code, description: c.description ?? '', type: c.type, value: c.value,
      minSubtotal: c.minSubtotal ?? null, maxDiscount: c.maxDiscount ?? null, maxUses: c.maxUses ?? null,
      validFrom: c.validFrom ?? '', validTo: c.validTo ?? '', active: c.status === 'Active'
    });
  }

  setType(type: DiscountType): void {
    this.form.update(f => (f ? { ...f, type } : f));
  }

  save(): void {
    const f = this.form();
    if (!f || this.saving()) return;
    const body: CouponInput = {
      code: f.code.trim().toUpperCase(), description: f.description.trim() || null, type: f.type, value: Number(f.value),
      minSubtotal: f.minSubtotal || null, maxDiscount: f.type === 'Percent' ? f.maxDiscount || null : null,
      maxUses: f.maxUses || null, validFrom: f.validFrom || null, validTo: f.validTo || null, active: f.active
    };
    this.saving.set(true);
    (f.id ? this.couponsApi.update(f.id, body) : this.couponsApi.create(body)).subscribe({
      next: res => { this.saving.set(false); this.toast.success(res.message); this.form.set(null); this.load(); },
      error: (e: ApiFailure) => { this.saving.set(false); this.toast.error(e.message); }
    });
  }
}
