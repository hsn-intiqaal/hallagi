import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '@core/http/api.service';
import { BranchContext } from '@core/state/branch-context';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/state/toast.service';
import { Branch, UserRow, UserRole, UserRoleLabel } from '@core/models';
import { Icon } from '@shared/ui/icon';
import { LoadingRow, Modal } from '@shared/ui/ui';

const PERMISSION_LABELS: Record<string, string> = {
  'sale.create': 'تسجيل المبيعات',
  'sale.view': 'عرض المبيعات',
  'sale.void': 'إلغاء عمليات البيع',
  'customer.manage': 'إدارة العملاء',
  'service.view': 'عرض الخدمات',
  'service.manage': 'إدارة الخدمات والأسعار',
  'barber.view': 'عرض الحلاقين',
  'barber.manage': 'إدارة الحلاقين',
  'barber.dues.view': 'عرض مستحقات الحلاقين',
  'expense.create': 'تسجيل المصروفات',
  'expense.view': 'عرض المصروفات',
  'expense.void': 'إلغاء المصروفات',
  'shift.open': 'فتح الوردية',
  'shift.close': 'إقفال الوردية',
  'shift.view': 'عرض الخزينة',
  'report.view': 'عرض التقارير',
  'branch.manage': 'إدارة الفروع',
  'user.manage': 'إدارة المستخدمين',
  'settings.manage': 'إدارة الإعدادات'
};

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [FormsModule, Icon, LoadingRow, Modal, NgIf, NgFor],
  templateUrl: './users.html',
  styleUrl: './users.scss'
})
export class Users implements OnInit {
  private api = inject(ApiService);
  private branches = inject(BranchContext);
  readonly auth = inject(AuthService);
  private toast = inject(ToastService);

  readonly users = signal<UserRow[]>([]);
  readonly branchesList = signal<Branch[]>([]);
  readonly loading = signal(true);
  readonly roles: { value: UserRole; label: string }[] =
    (Object.keys(UserRoleLabel) as UserRole[]).map(k => ({ value: k, label: UserRoleLabel[k] }));
  readonly permissionLabels = PERMISSION_LABELS;

  readonly showInvite = signal(false);
  readonly editing = signal<UserRow | null>(null);

  inviteForm = { fullName: '', email: '', phone: '', password: '', role: 'Cashier' as UserRole, branchIds: [] as string[], permissions: {} as Record<string, boolean | null> };
  editForm = { fullName: '', role: 'Cashier' as UserRole, status: 'Active' as 'Active' | 'Disabled', branchIds: [] as string[], permissions: {} as Record<string, boolean | null> };
  resetTarget: UserRow | null = null;
  resetPassword = '';

  ngOnInit(): void {
    this.branches.init();
    this.api.get<any>('/branches').subscribe({ next: (r: any) => this.branchesList.set(r.items) });
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.get<{ items: UserRow[] }>('/users').subscribe({
      next: res => { this.users.set(res.items); this.loading.set(false); },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل المستخدمين'); this.loading.set(false); }
    });
  }

  openInvite(): void {
    this.inviteForm = {
      fullName: '', email: '', phone: '', password: '', role: 'Cashier',
      branchIds: [], permissions: {}
    };
    this.showInvite.set(true);
  }

  invite(): void {
    if (!this.inviteForm.fullName.trim() || !this.inviteForm.email.trim() || this.inviteForm.password.length < 6) {
      this.toast.error('أكمل الاسم والبريد وكلمة مرور من 6 أحرف على الأقل');
      return;
    }
    const body = {
      fullName: this.inviteForm.fullName.trim(),
      email: this.inviteForm.email.trim(),
      phone: this.inviteForm.phone.trim() || null,
      password: this.inviteForm.password,
      role: this.inviteForm.role,
      branchIds: this.inviteForm.branchIds,
      permissions: cleanPermissions(this.inviteForm.permissions)
    };
    this.api.post('/users', body).subscribe({
      next: () => { this.toast.success('تم إنشاء المستخدم بنجاح'); this.showInvite.set(false); this.load(); },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر إنشاء المستخدم')
    });
  }

  openEdit(u: UserRow): void {
    this.editing.set(u);
    this.editForm = {
      fullName: u.fullName,
      role: u.role,
      status: u.status,
      branchIds: [...u.branchIds],
      permissions: { ...(u.permissions ?? {}) }
    };
  }

  saveEdit(): void {
    const u = this.editing();
    if (!u) return;
    this.api.put(`/users/${u.id}`, {
      fullName: this.editForm.fullName.trim() || null,
      role: this.editForm.role,
      status: this.editForm.status,
      branchIds: this.editForm.branchIds,
      permissions: cleanPermissions(this.editForm.permissions)
    }).subscribe({
      next: () => { this.toast.success('تم تحديث المستخدم'); this.editing.set(null); this.load(); },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر تحديث المستخدم')
    });
  }

  toggleBranch(list: string[], id: string): string[] {
    return list.includes(id) ? list.filter(b => b !== id) : [...list, id];
  }

  setPerm(map: Record<string, boolean | null>, key: string, value: boolean | null): Record<string, boolean | null> {
    const copy = { ...map };
    if (value === null) delete copy[key]; else copy[key] = value;
    return copy;
  }

  branchNames(ids: string[]): string {
    return ids.map(id => this.branchesList().find(b => b.id === id)?.name ?? '—').join(' · ') || 'بدون فرع';
  }

  objectKeys(obj: Record<string, any> | null | undefined): string[] {
    return obj ? Object.keys(obj) : [];
  }

  confirmResetPassword(): void {
    const target = this.resetTarget;
    if (!target) return;
    const pwd = this.resetPassword.trim();
    if (pwd.length < 6) { this.toast.error('كلمة المرور يجب أن تكون 6 أحرف على الأقل'); return; }
    this.api.post(`/users/${target.id}/reset-password`, { newPassword: pwd }).subscribe({
      next: () => { this.toast.success('تمت إعادة تعيين كلمة المرور'); this.resetTarget = null; this.resetPassword = ''; },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر تعيين كلمة المرور')
    });
  }
}

function cleanPermissions(map: Record<string, boolean | null>): Record<string, boolean> | null {
  const out: Record<string, boolean> = {};
  for (const [k, v] of Object.entries(map)) {
    if (v === true || v === false) out[k] = v;
  }
  return Object.keys(out).length ? out : null;
}
