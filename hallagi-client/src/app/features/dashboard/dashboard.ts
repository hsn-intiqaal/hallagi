import { DecimalPipe, NgIf, NgFor } from '@angular/common';
import { Component, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DateArPipe, SarPipe } from '@shared/pipes/format.pipes';
import { ApiService } from '@core/http/api.service';
import { AuthService } from '@core/auth/auth.service';
import { BranchContext } from '@core/state/branch-context';
import { ToastService } from '@core/state/toast.service';
import { Dashboard as DashboardModel } from '@core/models';
import { Icon } from '@shared/ui/icon';
import { StatCard, EmptyState, LoadingRow } from '@shared/ui/ui';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, Icon, StatCard, EmptyState, LoadingRow, DateArPipe, SarPipe, DecimalPipe, NgIf, NgFor],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard implements OnInit {
  private api = inject(ApiService);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);
  readonly router = inject(Router);

  readonly auth = inject(AuthService);
  /** The salon's calendar day (not the browser's UTC date). */
  readonly today = this.auth.today();
  readonly loading = signal(true);
  readonly data = signal<DashboardModel | null>(null);
  readonly period = signal<'today' | 'week' | 'month'>('today');
  readonly chartBars = computed(() => {
    const items = this.data()?.chart?.items ?? [];
    const max = Math.max(1, ...items.map(i => i.total));
    return items.map(i => ({ ...i, pct: Math.round((i.total / max) * 100) }));
  });

  constructor() {
    // reload when the branch selector in the top bar changes
    effect(() => {
      this.branches.currentBranchId();
      untracked(() => this.load());
    });
  }

  ngOnInit(): void {}

  changeTone(): 'up' | 'down' | 'flat' {
    const p = this.data()?.comparison.changePercent;
    return p === null || p === undefined || p === 0 ? 'flat' : p > 0 ? 'up' : 'down';
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
      error: (e: { message: string }) => { this.toast.error(e.message ?? 'تعذّر تحميل لوحة الرئيسية'); this.loading.set(false); }
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
