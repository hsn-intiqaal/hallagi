import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiFailure, ApiService } from '@core/http/api.service';
import { LoyaltyApi } from '@core/api/inventory.api';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/state/toast.service';
import { CustomerFile, LoyaltyHistory } from '@core/models';
import { Icon } from '@shared/ui/icon';
import { SarPipe, DateTimeArPipe, DateShortPipe } from '@shared/pipes/format.pipes';
import { LoadingRow } from '@shared/ui/ui';

@Component({
  selector: 'app-customer-profile',
  standalone: true,
  imports: [FormsModule, Icon, SarPipe, DateTimeArPipe, DateShortPipe, LoadingRow, NgIf, NgFor],
  templateUrl: './customer-profile.html',
  styleUrl: './customer-profile.scss'
})
export class CustomerProfile implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private toast = inject(ToastService);
  private loyaltyApi = inject(LoyaltyApi);
  readonly auth = inject(AuthService);
  readonly router = inject(Router);

  readonly loyalty = signal<LoyaltyHistory | null>(null);
  readonly showLoyalty = signal(false);
  adjustPoints: number | null = null;

  readonly file = signal<CustomerFile | null>(null);
  readonly loading = signal(true);
  readonly editing = signal(false);
  form = { fullName: '', phone: '', notes: '' };

  ngOnInit(): void { this.load(); }

  load(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loading.set(true);
    this.api.get<CustomerFile>(`/customers/${id}`).subscribe({
      next: f => {
        this.file.set(f);
        this.form = { fullName: f.customer.fullName, phone: f.customer.phone ?? '', notes: f.customer.notes ?? '' };
        this.loading.set(false);
        this.loyaltyApi.history(id).subscribe({ next: l => this.loyalty.set(l), error: () => this.loyalty.set(null) });
      },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل ملف العميل'); this.loading.set(false); }
    });
  }

  save(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    if (!this.form.fullName.trim()) { this.toast.error('اسم العميل مطلوب'); return; }
    this.api.put(`/customers/${id}`, {
      fullName: this.form.fullName.trim(),
      phone: this.form.phone.trim() || null,
      notes: this.form.notes.trim() || null
    }).subscribe({
      next: () => { this.toast.success('تم تحديث بيانات العميل'); this.editing.set(false); this.load(); },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر حفظ بيانات العميل')
    });
  }

  adjust(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    const points = Math.trunc(Number(this.adjustPoints));
    if (!points) { this.toast.error('أدخل عدد النقاط (موجب للإضافة، سالب للخصم)'); return; }
    this.loyaltyApi.adjust(id, points, null).subscribe({
      next: res => { this.toast.success(res.message); this.adjustPoints = null; this.load(); },
      error: (e: ApiFailure) => this.toast.error(e.message)
    });
  }

  openSale(id: string): void { this.router.navigate(['/sales', id]); }
}
