import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NgIf, NgFor } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { BranchContext } from '../../core/branch-context';
import { ToastService } from '../../core/toast.service';
import { Icon } from '../../shared/icon';
import { LoadingRow } from '../../shared/ui';
import { CompensationType, CompensationTypeLabel } from './models-barbers';

@Component({
  selector: 'app-barber-form',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, Icon, LoadingRow],
  templateUrl: './barber-form.html',
  styleUrl: './barber-form.scss'
})
export class BarberForm implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  readonly router = inject(Router);
  private toast = inject(ToastService);
  private branches = inject(BranchContext);

  readonly isEdit = computed(() => !!this.route.snapshot.paramMap.get('id'));
  readonly barberId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);

  readonly compTypes: { value: CompensationType; label: string }[] =
    (Object.keys(CompensationTypeLabel) as CompensationType[]).map(k => ({ value: k, label: CompensationTypeLabel[k] }));

  allBranches: { id: string; name: string }[] = [];
  form = {
    fullName: '',
    phone: '',
    startDate: '',
    active: true,
    branchIds: [] as string[],
    compType: 'Commission' as CompensationType,
    monthlySalary: null as number | null,
    commissionRate: null as number | null, // stored as percent for the form
    chairRentAmount: null as number | null,
    compNotes: ''
  };

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.barberId.set(id);
      this.loading.set(true);
      this.api.get<any>(`/barbers/${id}`).subscribe({
        next: (d: any) => {
          this.form.fullName = d.barber.fullName ?? '';
          this.form.phone = d.barber.phone ?? '';
          this.form.startDate = d.barber.startDate ?? '';
          this.form.active = d.barber.status === 'Active';
          this.form.branchIds = [...(d.barber.branchIds ?? [])];
          if (d.compensation) {
            this.form.compType = d.compensation.type as CompensationType;
            this.form.monthlySalary = d.compensation.monthlySalary;
            this.form.commissionRate = d.compensation.commissionRate != null
              ? Math.round(d.compensation.commissionRate * 100) : null;
            this.form.chairRentAmount = d.compensation.chairRentAmount;
            this.form.compNotes = d.compensation.notes ?? '';
          }
          this.loading.set(false);
        },
        error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل بيانات الحلاق'); this.loading.set(false); }
      });
    }

    // Owner sees all branches; others only the ones assigned to them
    this.api.get<any>('/branches').subscribe({
      next: (res: any) => {
        this.allBranches = this.branches.showAllOption()
          ? res.items
          : res.items.filter((b: any) => this.branches.branches().some(mb => mb.id === b.id));
      }
    });
  }

  toggleBranch(id: string): void {
    this.form.branchIds = this.form.branchIds.includes(id)
      ? this.form.branchIds.filter(b => b !== id)
      : [...this.form.branchIds, id];
  }

  save(): void {
    if (this.saving()) return;
    if (!this.form.fullName.trim()) { this.toast.error('اسم الحلاق مطلوب'); return; }
    if (this.form.branchIds.length === 0) { this.toast.error('اختر فرعًا واحدًا على الأقل لهذا الحلاق'); return; }

    const compensation = this.form.compType
      ? {
          type: this.form.compType,
          monthlySalary: this.form.monthlySalary,
          commissionRate: this.form.commissionRate != null ? this.form.commissionRate / 100 : null,
          chairRentAmount: this.form.chairRentAmount,
          notes: this.form.compNotes?.trim() || null,
          effectiveFrom: null as string | null
        }
      : null;

    const body = {
      fullName: this.form.fullName.trim(),
      phone: this.form.phone.trim() || null,
      startDate: this.form.startDate || null,
      active: this.form.active,
      branchIds: this.form.branchIds,
      compensation
    };

    this.saving.set(true);
    const id = this.barberId();
    const req = id ? this.api.put(`/barbers/${id}`, body) : this.api.post('/barbers', body);
    req.subscribe({
      next: (res: any) => {
        this.saving.set(false);
        this.toast.success(id ? 'تم تحديث بيانات الحلاق' : 'تمت إضافة الحلاق بنجاح');
        this.router.navigate(['/barbers', id ?? res?.id]);
      },
      error: (e: any) => { this.saving.set(false); this.toast.error(e?.message ?? 'تعذّر حفظ بيانات الحلاق'); }
    });
  }
}
