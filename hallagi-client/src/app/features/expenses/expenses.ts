import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '@core/http/api.service';
import { BranchContext } from '@core/state/branch-context';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/state/toast.service';
import { ExpenseCategory, ExpenseRow, ExpenseSource, ExpenseCategoryLabel, ExpenseSourceLabel } from '@core/models';
import { Icon } from '@shared/ui/icon';
import { DateShortPipe, SarPipe } from '@shared/pipes/format.pipes';
import { EmptyState, LoadingRow, Modal } from '@shared/ui/ui';

@Component({
  selector: 'app-expenses',
  standalone: true,
  imports: [FormsModule, Icon, DateShortPipe, SarPipe, EmptyState, LoadingRow, Modal, NgIf, NgFor],
  templateUrl: './expenses.html',
  styleUrl: './expenses.scss'
})
export class Expenses implements OnInit {
  private api = inject(ApiService);
  private branches = inject(BranchContext);
  readonly auth = inject(AuthService);
  private toast = inject(ToastService);

  readonly expenses = signal<ExpenseRow[]>([]);
  readonly total = signal(0);
  readonly loading = signal(true);
  readonly showForm = signal(false);
  readonly canCreate = signal(false);
  readonly canVoid = signal(false);
  page = 1;
  readonly pageSize = 20;

  readonly categories: { value: ExpenseCategory; label: string }[] =
    (Object.keys(ExpenseCategoryLabel) as ExpenseCategory[]).map(k => ({ value: k, label: ExpenseCategoryLabel[k] }));
  readonly sources: { value: ExpenseSource; label: string }[] =
    (Object.keys(ExpenseSourceLabel) as ExpenseSource[]).map(k => ({ value: k, label: ExpenseSourceLabel[k] }));

  form = { category: 'Supplies' as ExpenseCategory, amount: 0, expenseDate: '', description: '', source: 'CashDrawer' as ExpenseSource };
  formError = '';
  voidTarget: ExpenseRow | null = null;
  voidReason = '';

  ngOnInit(): void {
    this.form.expenseDate = this.auth.today();
    this.canCreate.set(this.auth.can('expense.create'));
    this.canVoid.set(this.auth.can('expense.void'));
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.get<any>('/expenses', { branchId: this.branches.scopeId(), page: this.page, pageSize: this.pageSize }).subscribe({
      next: (res: any) => { this.expenses.set(res.items); this.total.set(res.totalCount); this.loading.set(false); },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل المصروفات'); this.loading.set(false); }
    });
  }

  openAdd(): void {
    this.form = { category: 'Supplies', amount: 0, expenseDate: this.auth.today(), description: '', source: 'CashDrawer' };
    this.formError = '';
    this.showForm.set(true);
  }

  save(): void {
    if (!this.form.amount || this.form.amount <= 0) { this.formError = 'أدخل مبلغًا أكبر من صفر'; return; }
    if (!this.form.expenseDate) { this.formError = 'اختر تاريخ المصروف'; return; }
    this.api.post<any>('/expenses', {
      branchId: this.branches.scopeId(),
      category: this.form.category,
      amount: this.form.amount,
      expenseDate: this.form.expenseDate,
      description: this.form.description?.trim() || null,
      source: this.form.source
    }).subscribe({
      next: () => { this.toast.success('تم تسجيل المصروف بنجاح'); this.showForm.set(false); this.load(); },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر تسجيل المصروف')
    });
  }

  askVoid(e: ExpenseRow): void { this.voidTarget = e; this.voidReason = ''; }

  confirmVoid(): void {
    if (!this.voidTarget) return;
    if (!this.voidReason.trim()) { this.toast.error('اكتب سبب الإلغاء'); return; }
    this.api.post(`/expenses/${this.voidTarget.id}/void`, { reason: this.voidReason.trim() }).subscribe({
      next: () => { this.toast.success('تم إلغاء المصروف وحفظ التصحيح في سجل التدقيق'); this.voidTarget = null; this.load(); },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر إلغاء المصروف')
    });
  }

  get pages(): number { return Math.max(1, Math.ceil(this.total() / this.pageSize)); }
  nextPage(): void { if (this.page < this.pages) { this.page++; this.load(); } }
  prevPage(): void { if (this.page > 1) { this.page--; this.load(); } }
}
