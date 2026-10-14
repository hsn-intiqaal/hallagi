import { Component, OnInit, inject, signal } from '@angular/core';
import { NgIf, NgFor } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { BranchContext } from '../../core/branch-context';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';
import { DateArPipe, SarPipe } from '../../core/format.pipes';
import { Icon } from '../../shared/icon';
import { EmptyState, LoadingRow, Modal } from '../../shared/ui';

@Component({
  selector: 'app-handovers',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, Icon, DateArPipe, SarPipe, EmptyState, LoadingRow, Modal],
  templateUrl: './handovers.html',
  styleUrl: './handovers.scss'
})
export class Handovers implements OnInit {
  private api = inject(ApiService);
  private branches = inject(BranchContext);
  readonly auth = inject(AuthService);
  private toast = inject(ToastService);

  readonly items = signal<any[]>([]);
  readonly users = signal<{ id: string; fullName: string }[]>([]);
  readonly loading = signal(true);
  readonly showForm = signal(false);

  form = { branchId: '', fromUserId: '', toUserId: '', amount: 0, note: '' };

  ngOnInit(): void {
    const id = this.branches.scopeId();
    this.form.branchId = id ?? '';
    this.load();
    this.api.get<{ items: { id: string; profileId: string; fullName: string }[] }>('/users').subscribe({
      next: (res: any) => this.users.set(res.items.map((u: any) => ({ id: u.profileId, fullName: u.fullName }))),
      error: () => this.users.set([])
    });
  }

  load(): void {
    this.loading.set(true);
    const id = this.branches.scopeId();
    this.api.get<{ items: any[] }>('/ledger/handovers', { branchId: id }).subscribe({
      next: (res: any) => { this.items.set(res.items); this.loading.set(false); },
      error: (e: any) => { this.loading.set(false); this.toast.error(e?.message ?? 'تعذّر تحميل التسليمات'); }
    });
  }

  save(): void {
    if (!this.form.branchId || !this.form.fromUserId || !this.form.toUserId) { this.toast.error('أكخدم بيانات التسليم'); return; }
    if (!this.form.amount || this.form.amount <= 0) { this.toast.error('أدخل مبلغًا صحيحًا'); return; }
    this.api.post('/ledger/handovers', {
      branchId: this.form.branchId,
      fromUserId: this.form.fromUserId,
      toUserId: this.form.toUserId,
      amount: this.form.amount,
      note: this.form.note
    }).subscribe({
      next: () => { this.toast.success('تم تسجيل التسليم النقدي'); this.showForm.set(false); this.load(); },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر تسجيل التسليم')
    });
  }
}
