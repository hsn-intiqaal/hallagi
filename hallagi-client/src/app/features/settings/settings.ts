import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';
import { Settings as SettingsModel } from '../../core/models';
import { Icon } from '../../shared/icon';
import { SarPipe } from '../../core/format.pipes';
import { LoadingRow } from '../../shared/ui';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, Icon, SarPipe, LoadingRow],
  templateUrl: './settings.html',
  styleUrl: './settings.scss'
})
export class Settings implements OnInit {
  private api = inject(ApiService);
  readonly auth = inject(AuthService);
  private toast = inject(ToastService);

  readonly loading = signal(true);
  readonly canManage = signal(false);
  readonly subscription = signal<any>(null);
  readonly plans = signal<any[]>([]);
  readonly savingPlan = signal(false);
  form = { organizationName: '', currency: 'SAR', timeZone: 'Asia/Riyadh', enableCash: true, enableCard: true, receiptFooter: '', receiptPhone: '' };
  password = { current: '', next: '' };

  ngOnInit(): void {
    this.api.get<any>('/auth/me').subscribe({
      next: (m: any) => this.canManage.set(m.permissions?.['settings.manage'] === true)
    });
    this.api.get<any>('/subscription').subscribe({
      next: s => this.subscription.set(s),
      error: () => this.subscription.set(null)
    });
    this.api.get<any>('/subscription/plans').subscribe({
      next: (res: any) => this.plans.set(res),
      error: () => this.plans.set([])
    });
    this.api.get<SettingsModel>('/settings').subscribe({
      next: s => {
        this.form = {
          organizationName: s.organization?.name ?? '',
          currency: s.organization?.currency ?? 'SAR',
          timeZone: s.organization?.timeZone ?? 'Asia/Riyadh',
          enableCash: s.settings.enableCash,
          enableCard: s.settings.enableCard,
          receiptFooter: s.settings.receiptFooter ?? '',
          receiptPhone: s.settings.receiptPhone ?? ''
        };
        this.loading.set(false);
      },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل الإعدادات'); this.loading.set(false); }
    });
  }

  save(): void {
    if (!this.form.organizationName.trim()) { this.toast.error('اسم المنشأة مطلوب'); return; }
    this.api.put('/settings', {
      organizationName: this.form.organizationName.trim(),
      currency: this.form.currency,
      timeZone: this.form.timeZone,
      enableCash: this.form.enableCash,
      enableCard: this.form.enableCard,
      receiptFooter: this.form.receiptFooter?.trim() || null,
      receiptPhone: this.form.receiptPhone?.trim() || null
    }).subscribe({
      next: () => { this.toast.success('تم حفظ الإعدادات بنجاح'); this.auth.refreshMe(); },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر حفظ الإعدادات')
    });
  }

  changePassword(): void {
    if (this.password.next.length < 6) { this.toast.error('كلمة المرور الجديدة يجب أن تكون 6 أحرف على الأقل'); return; }
    this.api.post('/auth/change-password', { currentPassword: this.password.current, newPassword: this.password.next }).subscribe({
      next: () => { this.toast.success('تم تغيير كلمة المرور بنجاح'); this.password = { current: '', next: '' }; },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر تغيير كلمة المرور')
    });
  }

  changePlan(planId: string): void {
    this.savingPlan.set(true);
    this.api.post('/subscription/change-plan', { planId }).subscribe({
      next: () => {
        this.savingPlan.set(false);
        this.toast.success('تم تغيير الخطة بنجاح');
        this.api.get<any>('/subscription').subscribe({ next: s => this.subscription.set(s) });
      },
      error: (e: any) => { this.savingPlan.set(false); this.toast.error(e?.message ?? 'تعذّر تغيير الخطة'); }
    });
  }
}
