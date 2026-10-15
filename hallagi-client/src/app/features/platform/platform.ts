import { Component, OnInit, inject, signal } from '@angular/core';
import { NgIf, NgFor } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';
import { DateShortPipe, SarPipe } from '../../core/format.pipes';
import { Icon } from '../../shared/icon';
import { EmptyState, LoadingRow, StatCard, Modal } from '../../shared/ui';

interface Org {
  id: string; name: string; currency: string; timeZone: string;
  logoUrl: string | null; coverImageUrl: string | null;
  primaryColor: string; secondaryColor: string;
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
  private router = inject(Router);

  readonly stats = signal<any>(null);
  readonly orgs = signal<Org[]>([]);
  readonly plans = signal<Plan[]>([]);
  readonly loading = signal(true);
  search = '';

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

  /** Organization creation lives on its own full screen (branding + images + colors). */
  openCreate(): void {
    this.router.navigate(['/platform/new']);
  }

  askAddBranch(org: Org): void { this.branchTarget.set(org); this.branchForm = { name: '', city: 'الرياض' }; }

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
