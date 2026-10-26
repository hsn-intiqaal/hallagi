import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '@core/http/api.service';
import { BranchContext } from '@core/state/branch-context';
import { ToastService } from '@core/state/toast.service';
import { PosService } from '@core/models';
import { Icon } from '@shared/ui/icon';
import { SarPipe } from '@shared/pipes/format.pipes';
import { EmptyState, LoadingRow, Modal } from '@shared/ui/ui';

@Component({
  selector: 'app-services',
  standalone: true,
  imports: [FormsModule, Icon, SarPipe, EmptyState, LoadingRow, Modal, NgIf, NgFor],
  templateUrl: './services.html',
  styleUrl: './services.scss'
})
export class Services implements OnInit {
  private api = inject(ApiService);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);

  readonly services = signal<PosService[]>([]);
  readonly loading = signal(true);
  readonly editing = signal<PosService | null>(null);
  readonly showForm = signal(false);

  form = { name: '', description: '', basePrice: 0, durationMinutes: 30, active: true };
  formError = '';

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.get<{ items: PosService[] }>('/services', { branchId: this.branches.scopeId() }).subscribe({
      next: res => { this.services.set(res.items); this.loading.set(false); },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل الخدمات'); this.loading.set(false); }
    });
  }

  openAdd(): void {
    this.form = { name: '', description: '', basePrice: 0, durationMinutes: 30, active: true };
    this.formError = '';
    this.editing.set(null);
    this.showForm.set(true);
  }

  openEdit(s: PosService): void {
    this.form = { name: s.name, description: s.description ?? '', basePrice: s.basePrice ?? s.price, durationMinutes: s.durationMinutes ?? 30, active: s.status === 'Active' };
    this.formError = '';
    this.editing.set(s);
    this.showForm.set(true);
  }

  save(): void {
    if (!this.form.name.trim()) { this.formError = 'اسم الخدمة مطلوب'; return; }
    if (this.form.basePrice === null || this.form.basePrice === undefined || Number.isNaN(this.form.basePrice) || this.form.basePrice < 0) {
      this.formError = 'السعر يجب أن يكون رقمًا أكبر من أو يساوي صفرًا';
      return;
    }
    const body = {
      name: this.form.name.trim(),
      description: this.form.description?.trim() || null,
      basePrice: this.form.basePrice,
      durationMinutes: Number(this.form.durationMinutes) || 30,
      active: this.form.active
    };
    const editing = this.editing();
    const req = editing
      ? this.api.put(`/services/${editing.id}`, body)
      : this.api.post('/services', body);
    req.subscribe({
      next: () => {
        this.toast.success(editing ? 'تم تحديث الخدمة بنجاح' : 'تمت إضافة الخدمة بنجاح');
        this.showForm.set(false);
        this.load();
      },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر حفظ الخدمة')
    });
  }

  toggleStatus(s: PosService): void {
    const active = s.status !== 'Active';
    this.api.post(`/services/${s.id}/status`, { active }).subscribe({
      next: () => {
        this.toast.success(active ? 'تم تنشيط الخدمة' : 'تم إيقاف الخدمة في عمليات البيع الجديدة');
        this.load();
      },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر تحديث حالة الخدمة')
    });
  }
}
