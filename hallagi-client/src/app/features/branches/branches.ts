import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { BranchContext } from '../../core/branch-context';
import { ToastService } from '../../core/toast.service';
import { Branch } from '../../core/models';
import { Icon } from '../../shared/icon';
import { LoadingRow, Modal } from '../../shared/ui';

@Component({
  selector: 'app-branches',
  standalone: true,
  imports: [FormsModule, Icon, LoadingRow, Modal, NgIf, NgFor],
  templateUrl: './branches.html',
  styleUrl: './branches.scss'
})
export class Branches implements OnInit {
  private api = inject(ApiService);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);

  readonly items = signal<Branch[]>([]);
  readonly loading = signal(true);
  readonly showForm = signal(false);
  readonly editing = signal<Branch | null>(null);
  form = { name: '', city: '', address: '', active: true };
  formError = '';

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.get<{ items: Branch[] }>('/branches').subscribe({
      next: res => { this.items.set(res.items); this.loading.set(false); },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل الفروع'); this.loading.set(false); }
    });
  }

  openAdd(): void {
    this.form = { name: '', city: 'الرياض', address: '', active: true };
    this.formError = '';
    this.editing.set(null);
    this.showForm.set(true);
  }

  openEdit(b: Branch): void {
    this.form = { name: b.name, city: b.city, address: b.address ?? '', active: (b.status ?? 'Active') === 'Active' };
    this.formError = '';
    this.editing.set(b);
    this.showForm.set(true);
  }

  save(): void {
    if (!this.form.name.trim()) { this.formError = 'اسم الفرع مطلوب'; return; }
    if (!this.form.city.trim()) { this.formError = 'المدينة مطلوبة'; return; }
    const body = { name: this.form.name.trim(), city: this.form.city.trim(), address: this.form.address?.trim() || null, active: this.form.active };
    const editing = this.editing();
    const req = editing ? this.api.put(`/branches/${editing.id}`, body) : this.api.post('/branches', body);
    req.subscribe({
      next: () => {
        this.toast.success(editing ? 'تم تحديث بيانات الفرع' : 'تمت إضافة الفرع');
        this.showForm.set(false);
        this.load();
        this.branches.init();
      },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر حفظ الفرع')
    });
  }

  switchTo(id: string): void {
    this.branches.select(id);
    this.toast.success('تم تبديل الفرع');
  }
}
