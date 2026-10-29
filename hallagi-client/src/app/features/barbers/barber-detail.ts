import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService, ApiFailure } from '@core/http/api.service';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/state/toast.service';
import { SarPipe, PctPipe, DateShortPipe } from '@shared/pipes/format.pipes';
import { Icon } from '@shared/ui/icon';
import { LoadingRow } from '@shared/ui/ui';
import { BarberDetail, BarberFormModel, CompensationType, CompensationTypeLabel } from '@features/barbers/models-barbers';

@Component({
  selector: 'app-barber-detail',
  standalone: true,
  imports: [FormsModule, Icon, SarPipe, PctPipe, DateShortPipe, LoadingRow, NgIf, NgFor],
  templateUrl: './barber-detail.html',
  styleUrl: './barber-detail.scss'
})
export class BarberDetailPage implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private toast = inject(ToastService);
  readonly auth = inject(AuthService);
  readonly router = inject(Router);

  readonly detail = signal<BarberDetail | null>(null);
  readonly loading = signal(true);
  readonly canManage = signal(false);
  readonly editing = signal(false);
  readonly form = signal<BarberFormModel | null>(null);
  readonly branches = signal<{ id: string; name: string }[]>([]);
  readonly compTypes: { value: CompensationType; label: string }[] =
    (Object.keys(CompensationTypeLabel) as CompensationType[]).map(k => ({ value: k, label: CompensationTypeLabel[k] }));

  private barberId = '';

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.barberId = id;
    this.load();
    this.api.get<{ items: { id: string; name: string }[] }>('/branches').subscribe({
      next: res => this.branches.set(res.items)
    });
    this.canManage.set(this.auth.can('barber.manage'));
  }

  load(): void {
    this.loading.set(true);
    this.api.get<BarberDetail>(`/barbers/${this.barberId}`).subscribe({
      next: d => { this.detail.set(d); this.loading.set(false); },
      error: (e: ApiFailure) => { this.toast.error(e?.message ?? 'تعذّر تحميل بيانات الحلاق'); this.loading.set(false); }
    });
  }

  startEdit(): void {
    this.router.navigate(['/barbers', this.barberId, 'edit']);
  }

  save(): void {
    const f = this.form();
    if (!f) return;
    if (!f.fullName.trim()) { this.toast.error('اسم الحلاق مطلوب'); return; }
    const body = {
      fullName: f.fullName.trim(),
      phone: f.phone?.trim() || null,
      startDate: f.startDate || null,
      active: f.active,
      branchIds: f.branchIds,
      compensation: f.compensation
    };
    this.api.put(`/barbers/${this.barberId}`, body).subscribe({
      next: () => { this.toast.success('تم تحديث بيانات الحلاق بنجاح'); this.editing.set(false); this.load(); },
      error: (e: ApiFailure) => this.toast.error(e?.message ?? 'تعذّر حفظ بيانات الحلاق')
    });
  }

  toggleBranch(id: string): void {
    this.form.update(f => {
      if (!f) return f;
      const has = f.branchIds.includes(id);
      return { ...f, branchIds: has ? f.branchIds.filter(b => b !== id) : [...f.branchIds, id] };
    });
  }

  updateField(field: 'fullName' | 'phone' | 'startDate' | 'active', value: string | boolean): void {
    this.form.update(f => f ? { ...f, [field]: value } : f);
  }

  updateComp(field: 'monthlySalary' | 'commissionRate' | 'chairRentAmount', value: number): void {
    this.form.update(f => f && f.compensation ? { ...f, compensation: { ...f.compensation, [field]: value } } : f);
  }

  commissionInput(): number {
    const f = this.form();
    return Math.round((f?.compensation?.commissionRate ?? 0) * 100);
  }

  setCommissionPercent(value: number): void {
    this.updateComp('commissionRate', (value || 0) / 100);
  }

  setCompType(t: CompensationType): void {
    this.form.update(f => f ? {
      ...f,
      compensation: { type: t, monthlySalary: null, commissionRate: null, chairRentAmount: null, notes: '' }
    } : f);
  }
}
