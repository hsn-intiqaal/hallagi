import { Component, OnInit, inject, signal } from '@angular/core';
import { NgIf, NgFor } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, ApiFailure } from '@core/http/api.service';
import { BranchContext } from '@core/state/branch-context';
import { ToastService } from '@core/state/toast.service';
import { Icon } from '@shared/ui/icon';
import { EmptyState, LoadingRow, Modal } from '@shared/ui/ui';
import { Device, ItemsOf } from '@core/models';

@Component({
  selector: 'app-devices',
  standalone: true,
  imports: [FormsModule, Icon, EmptyState, LoadingRow, Modal, NgIf, NgFor, Modal, EmptyState],
  templateUrl: './devices.html',
  styleUrl: './devices.scss'
})
export class Devices implements OnInit {
  private api = inject(ApiService);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);

  readonly items = signal<Device[]>([]);
  readonly branchesList = signal<{ id: string; name: string }[]>([]);
  readonly loading = signal(true);
  readonly showForm = signal(false);
  form = { branchId: '', name: '', deviceCode: '' };

  ngOnInit(): void { this.load(); }

  load(): void {
    const id = this.branches.scopeId();
    this.loading.set(true);
    this.api.get<ItemsOf<Device>>('/devices', { branchId: id }).subscribe({
      next: res => { this.items.set(res.items); this.loading.set(false); },
      error: (e: ApiFailure) => { this.loading.set(false); this.toast.error(e?.message ?? 'تعذّر تحميل الأجهزة'); }
    });
    this.api.get<{ items: { id: string; name: string }[] }>('/branches').subscribe({
      next: res => {
        this.branchesList.set(res.items);
        this.form.branchId = this.branches.scopeId() ?? res.items[0]?.id ?? '';
      }
    });
  }

  openAdd(): void { this.form = { branchId: this.branches.scopeId() ?? '', name: '', deviceCode: '' }; this.showForm.set(true); }

  save(): void {
    if (!this.form.name.trim()) { this.toast.error('اسم الجهاز مطلوب'); return; }
    this.api.post('/devices', this.form).subscribe({
      next: () => { this.toast.success('تمت إضافة الجهاز'); this.showForm.set(false); this.load(); },
      error: (e: ApiFailure) => this.toast.error(e?.message ?? 'تعذّر إضافة الجهاز')
    });
  }

  toggleStatus(d: Device): void {
    const active = d.status !== 'Active';
    this.api.post(`/devices/${d.id}/status`, { active }).subscribe({
      next: () => { this.toast.success(active ? 'تم تفعيل الجهاز' : 'تم تعطيل الجهاز'); this.load(); },
      error: (e: ApiFailure) => this.toast.error(e?.message ?? 'تعذّر تغيير حالة الجهاز')
    });
  }
}
