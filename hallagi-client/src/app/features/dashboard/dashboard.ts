import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DateArPipe, SarPipe } from '../../core/format.pipes';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { BranchContext } from '../../core/branch-context';
import { ToastService } from '../../core/toast.service';
import { Dashboard as DashboardModel } from '../../core/models';
import { Icon } from '../../shared/icon';
import { StatCard, EmptyState, LoadingRow } from '../../shared/ui';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, Icon, StatCard, EmptyState, LoadingRow, DateArPipe, SarPipe, NgIf, NgFor],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard implements OnInit {
  private api = inject(ApiService);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);
  readonly router = inject(Router);

  readonly auth = inject(AuthService);
  readonly today = new Date();
  readonly todayStr = this.today.toISOString().slice(0, 10);
  readonly loading = signal(true);
  readonly data = signal<DashboardModel | null>(null);
  readonly period = signal<'today' | 'week' | 'month'>('today');
  readonly chartBars = computed(() => {
    const items = this.data()?.chart?.items ?? [];
    const max = Math.max(1, ...items.map(i => i.total));
    return items.map(i => ({ ...i, pct: Math.round((i.total / max) * 100) }));
  });

  ngOnInit(): void {
    this.load();
  }

  setPeriod(p: 'today' | 'week' | 'month'): void {
    this.period.set(p);
    this.load();
  }

  load(): void {
    const branchId = this.branches.scopeId();
    this.loading.set(true);
    this.api.get<DashboardModel>('/dashboard', { branchId, period: this.period() }).subscribe({
      next: d => { this.data.set(d); this.loading.set(false); },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل لوحة الرئيسية'); this.loading.set(false); }
    });
  }

  newSale(): void {
    const id = this.branches.scopeId();
    if (!id) {
      this.toast.error('اختر فرعًا محددًا لتسجيل عملية بيع');
      return;
    }
    this.router.navigate(['/pos']);
  }

  periodLabel(): string {
    return this.period() === 'today' ? 'اليوم' : this.period() === 'week' ? 'هذا الأسبوع' : 'هذا الشهر';
  }
}
