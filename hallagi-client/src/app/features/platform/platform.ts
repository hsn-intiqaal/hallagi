import { Component, OnInit, inject, signal } from '@angular/core';
import { NgIf, NgFor } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';
import { DateShortPipe, SarPipe } from '../../core/format.pipes';
import { Icon } from '../../shared/icon';
import { EmptyState, LoadingRow, StatCard, Modal } from '../../shared/ui';

interface Org {
  id: string; name: string; currency: string; timeZone: string;
  branchesCount: number; usersCount: number; barbersCount: number; salesTotal: number;
  ownerName: string | null; ownerEmail: string | null;
  planName: string | null; planId: string | null; subscriptionStatus: string | null;
  createdAt: string;
}

interface Plan { id: string; name: string; description: string; maxBranches: number; maxUsers: number; maxBarbers: number; monthlyPrice: number; }

@Component({
  selector: 'app-platform',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, Icon, DateShortPipe, SarPipe, EmptyState, LoadingRow, StatCard, Modal],
  templateUrl: './platform.html',
  styleUrl: './platform.scss'
})
export class Platform implements OnInit {
  private api = inject(ApiService);
  readonly auth = inject(AuthService);
  private toast = inject(ToastService);

  readonly stats = signal<any>(null);
  readonly orgs = signal<Org[]>([]);
  readonly plans = signal<Plan[]>([]);
  readonly loading = signal(true);
  search = '';

  readonly showCreate = signal(false);
  readonly creating = signal(false);
  form = {
    name: '', city: 'الرياض', branchName: 'الفرع الرئيسي', address: '',
    ownerName: '', ownerEmail: '', ownerPhone: '', ownerPassword: '', planId: ''
  };

  readonly branchTarget = signal<Org | null>(null);
  branchForm = { name: '', city: 'الرياض' };

  readonly planTarget = signal<Org | null>(null);

  ngOnInit(): void {
    this.load();
    this.api.get<any>('/platform/plans').subscribe({
      next: (res: any) => this.plans.set(res),
      error: () => this.plans.set([])
    });
  }

  load(): void {
    this.loading.set(true);
    this.api.get<any>('/platform/stats').subscribe({
      next: s => this.stats.set(s),
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر تحميل الإحصائيات')
    });
    this.api.get<{ items: Org[] }>('/platform/organizations', { search: this.search }).subscribe({
      next: res => { this.orgs.set(res.items); this.loading.set(false); },
      error: (e: any) => { this.loading.set(false); this.toast.error(e?.message ?? 'تعذّر تحميل المنشآت'); }
    });
  }

  openCreate(): void {
    this.form = {
      name: '', city: 'الرياض', branchName: 'الفرع الرئيسي', address: '',
      ownerName: '', ownerEmail: '', ownerPhone: '', ownerPassword: '', planId: this.plans()[0]?.id ?? ''
    };
    this.showCreate.set(true);
  }

  create(): void {
    if (this.creating()) return;
    if (!this.form.name.trim() || !this.form.ownerEmail.trim() || this.form.ownerPassword.length < 6) {
      this.toast.error('أكمل اسم المنشأة وبريد المالك وكلمة مرور (6 أحرف على الأقل)');
      return;
    }
    this.creating.set(true);
    this.api.post<any>('/platform/organizations', {
      name: this.form.name.trim(),
      city: this.form.city.trim(),
      branchName: this.form.branchName.trim() || 'الفرع الرئيسي',
      address: this.form.address.trim() || null,
      ownerName: this.form.ownerName.trim() || 'مالك المنشأة',
      ownerEmail: this.form.ownerEmail.trim(),
      ownerPhone: this.form.ownerPhone.trim() || null,
      ownerPassword: this.form.ownerPassword,
      planId: this.form.planId || null
    }).subscribe({
      next: () => {
        this.creating.set(false);
        this.showCreate.set(false);
        this.toast.success('تم إنشاء المنشأة وحساب مالكها');
        this.load();
      },
      error: (e: any) => { this.creating.set(false); this.toast.error(e?.message ?? 'تعذّر إنشاء المنشأة'); }
    });
  }

  askAddBranch(org: Org): void { this.branchTarget.set(org); this.branchForm = { name: '', city: org.name.includes('الرياض') ? 'الرياض' : 'الرياض' }; }

  confirmAddBranch(): void {
    const org = this.branchTarget();
    if (!org) return;
    if (!this.branchForm.name.trim()) { this.toast.error('اسم الفرع مطلوب'); return; }
    this.api.post(`/platform/organizations/${org.id}/branches`, { name: this.branchForm.name.trim(), city: this.branchForm.city.trim() }).subscribe({
      next: () => { this.toast.success('تمت إضافة الفرع'); this.branchTarget.set(null); this.load(); },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر إضافة الفرع')
    });
  }

  askChangePlan(org: Org): void { this.planTarget.set(org); }

  confirmChangePlan(planId: string): void {
    const org = this.planTarget();
    if (!org) return;
    this.api.post(`/platform/organizations/${org.id}/plan`, { planId }).subscribe({
      next: () => { this.toast.success('تم تغيير الخطة'); this.planTarget.set(null); this.load(); },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر تغيير الخطة')
    });
  }

  setStatus(org: Org, status: string): void {
    this.api.post(`/platform/organizations/${org.id}/subscription-status`, { status }).subscribe({
      next: () => { this.toast.success('تم تحديث حالة الاشتراك'); this.load(); },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر تحديث الحالة')
    });
  }

  planClass(planName: string | null): string {
    if (!planName) return 'plan-trial';
    if (planName.includes('احترافي')) return 'plan-pro';
    if (planName.includes('أساسي')) return 'plan-basic';
    return 'plan-trial';
  }

  logout(): void { this.auth.logout(); }
}
