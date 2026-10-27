import { NgFor, NgIf } from '@angular/common';
import { Component, ElementRef, ViewChild, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Chart, registerables } from 'chart.js';
import { ReportQuery, ReportsApi } from '@core/api/reports.api';
import { AuthService } from '@core/auth/auth.service';
import { ApiFailure } from '@core/http/api.service';
import { BarberDuesReport, BranchesReport, CustomersReport, ExpensesReport, SalesReport, TaxReport } from '@core/models';
import { BranchContext } from '@core/state/branch-context';
import { ToastService } from '@core/state/toast.service';
import { addDays, previousMonth, startOfMonth } from '@core/time/local-date';
import { DateShortPipe, SarPipe } from '@shared/pipes/format.pipes';
import { Icon } from '@shared/ui/icon';
import { EmptyState, LoadingRow, StatCard } from '@shared/ui/ui';

Chart.register(...registerables);

type Tab = 'overview' | 'barbers' | 'branches' | 'customers' | 'tax' | 'expenses';
type Preset = 'today' | 'yesterday' | 'week' | 'month' | 'lastMonth' | 'custom';

const CHART_FONT = { family: "'IBM Plex Sans Arabic', sans-serif" };

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, RouterLink, Icon, SarPipe, DateShortPipe, LoadingRow, StatCard, EmptyState],
  templateUrl: './reports.html',
  styleUrl: './reports.scss'
})
export class Reports {
  private reportsApi = inject(ReportsApi);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);
  readonly auth = inject(AuthService);

  readonly tabs: { id: Tab; label: string; icon: string; permission?: string }[] = [
    { id: 'overview', label: 'نظرة عامة', icon: 'chart' },
    { id: 'barbers', label: 'الحلاقون', icon: 'user', permission: 'barber.dues.view' },
    { id: 'branches', label: 'الفروع', icon: 'building' },
    { id: 'customers', label: 'العملاء', icon: 'users' },
    { id: 'tax', label: 'الضريبة والمنتجات', icon: 'shield' },
    { id: 'expenses', label: 'المصروفات', icon: 'wallet' }
  ];
  readonly presets: { id: Preset; label: string }[] = [
    { id: 'today', label: 'اليوم' },
    { id: 'yesterday', label: 'أمس' },
    { id: 'week', label: 'آخر 7 أيام' },
    { id: 'month', label: 'هذا الشهر' },
    { id: 'lastMonth', label: 'الشهر الماضي' },
    { id: 'custom', label: 'مخصص' }
  ];

  readonly tab = signal<Tab>('overview');
  readonly preset = signal<Preset>('week');
  readonly from = signal(addDays(this.auth.today(), -6));
  readonly to = signal(this.auth.today());
  readonly loading = signal(false);

  readonly sales = signal<SalesReport | null>(null);
  readonly dues = signal<BarberDuesReport | null>(null);
  readonly branchesReport = signal<BranchesReport | null>(null);
  readonly customers = signal<CustomersReport | null>(null);
  readonly tax = signal<TaxReport | null>(null);
  readonly expenses = signal<ExpensesReport | null>(null);

  readonly visibleTabs = computed(() => this.tabs.filter(t => !t.permission || this.auth.can(t.permission)));
  readonly scopeLabel = computed(() => this.branches.currentBranch()?.name ?? 'كل الفروع');
  readonly maxService = computed(() => Math.max(1, ...(this.sales()?.byService ?? []).map(s => s.total)));
  readonly maxBarber = computed(() => Math.max(1, ...(this.sales()?.byBarber ?? []).map(s => s.netSales)));
  readonly peakHour = computed(() => {
    const hours = this.sales()?.byHour ?? [];
    return hours.length ? hours.reduce((a, b) => (b.count > a.count ? b : a)) : null;
  });
  readonly bestWeekday = computed(() => {
    const days = (this.sales()?.byWeekday ?? []).filter(d => d.count > 0);
    return days.length ? days.reduce((a, b) => (b.netSales > a.netSales ? b : a)) : null;
  });

  @ViewChild('dailyChart') private dailyCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('hourChart') private hourCanvas?: ElementRef<HTMLCanvasElement>;
  private charts: Chart[] = [];
  /** Tabs already loaded for the current filters. */
  private loaded = new Set<Tab>();

  constructor() {
    // reload when the branch selector in the top bar changes
    effect(() => {
      this.branches.currentBranchId();
      untracked(() => this.refresh());
    });
  }

  private query(): ReportQuery {
    return { from: this.from(), to: this.to(), branchId: this.branches.scopeId() };
  }

  // ===== filters =====

  applyPreset(p: Preset): void {
    this.preset.set(p);
    const today = this.auth.today();
    const ranges: Record<Exclude<Preset, 'custom'>, [string, string]> = {
      today: [today, today],
      yesterday: [addDays(today, -1), addDays(today, -1)],
      week: [addDays(today, -6), today],
      month: [startOfMonth(today), today],
      lastMonth: [previousMonth(today).from, previousMonth(today).to]
    };
    if (p === 'custom') return;
    const [from, to] = ranges[p];
    this.from.set(from);
    this.to.set(to);
    this.refresh();
  }

  setCustom(from: string, to: string): void {
    if (!from || !to) return;
    this.preset.set('custom');
    this.from.set(from <= to ? from : to);
    this.to.set(from <= to ? to : from);
    this.refresh();
  }

  selectTab(t: Tab): void {
    this.tab.set(t);
    this.loadTab(t);
  }

  refresh(): void {
    this.loaded.clear();
    this.loadTab(this.tab());
  }

  private loadTab(t: Tab): void {
    if (this.loaded.has(t)) {
      if (t === 'overview') setTimeout(() => this.renderCharts());
      return;
    }
    this.loaded.add(t);
    this.loading.set(true);
    const q = this.query();
    const done = () => this.loading.set(false);
    const fail = (e: ApiFailure) => { this.loading.set(false); this.loaded.delete(t); this.toast.error(e.message); };
    switch (t) {
      case 'overview':
        this.reportsApi.sales(q).subscribe({ next: r => { this.sales.set(r); done(); setTimeout(() => this.renderCharts()); }, error: fail });
        break;
      case 'barbers':
        this.reportsApi.barberDues(q).subscribe({ next: r => { this.dues.set(r); done(); }, error: fail });
        if (!this.sales()) this.reportsApi.sales(q).subscribe({ next: r => this.sales.set(r) });
        break;
      case 'branches':
        this.reportsApi.branches(q).subscribe({ next: r => { this.branchesReport.set(r); done(); }, error: fail });
        break;
      case 'customers':
        this.reportsApi.customers(q).subscribe({ next: r => { this.customers.set(r); done(); }, error: fail });
        break;
      case 'tax':
        this.reportsApi.tax(q).subscribe({ next: r => { this.tax.set(r); done(); }, error: fail });
        break;
      case 'expenses':
        this.reportsApi.expenses(q).subscribe({ next: r => { this.expenses.set(r); done(); }, error: fail });
        break;
    }
  }

  exportCsv(): void {
    this.reportsApi.exportSalesCsv(this.query()).subscribe({
      next: () => this.toast.success('تم تصدير ملف المبيعات'),
      error: (e: ApiFailure) => this.toast.error(e.message)
    });
  }

  print(): void { window.print(); }

  // ===== presentation helpers =====

  changeText(): string {
    const c = this.sales()?.comparison;
    if (!c || c.changePercent === null) return 'لا توجد بيانات للفترة السابقة';
    const sign = c.changePercent > 0 ? '▲' : c.changePercent < 0 ? '▼' : '●';
    return `${sign} ${Math.abs(c.changePercent)}% عن الفترة السابقة`;
  }

  changeTone(): 'up' | 'down' | 'flat' {
    const p = this.sales()?.comparison.changePercent;
    return p === null || p === undefined || p === 0 ? 'flat' : p > 0 ? 'up' : 'down';
  }

  hourLabel(h: number): string {
    const suffix = h < 12 ? 'ص' : 'م';
    const hour12 = h % 12 === 0 ? 12 : h % 12;
    return `${hour12} ${suffix}`;
  }

  /** Arabic counting noun: 1 عملية · 2 عمليتان · 3–10 عمليات · 11+ عملية */
  operations(n: number): string {
    if (n === 1) return 'عملية واحدة';
    if (n === 2) return 'عمليتان';
    return n >= 3 && n <= 10 ? `${n} عمليات` : `${n} عملية`;
  }

  pct(value: number, total: number): number {
    return total > 0 ? Math.round((value / total) * 100) : 0;
  }

  // ===== charts =====

  private renderCharts(): void {
    this.charts.forEach(c => c.destroy());
    this.charts = [];
    const r = this.sales();
    if (!r) return;
    const rtl = { reverse: true, grid: { display: false }, ticks: { font: CHART_FONT } };
    const money = { beginAtZero: true, position: 'right' as const, ticks: { font: CHART_FONT, callback: (v: string | number) => `${v}` } };
    const tooltip = {
      rtl: true, titleFont: CHART_FONT, bodyFont: CHART_FONT,
      callbacks: { label: (ctx: { parsed: { y: number | null } }) => ` ${(ctx.parsed.y ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2 })} ر.س` }
    };

    if (this.dailyCanvas) {
      this.charts.push(new Chart(this.dailyCanvas.nativeElement, {
        type: 'bar',
        data: {
          labels: r.daily.map(d => d.label),
          datasets: [{ label: 'صافي المبيعات', data: r.daily.map(d => d.netSales), backgroundColor: '#2563EB', borderRadius: 6, maxBarThickness: 42 }]
        },
        options: { locale: 'en-US', responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false }, tooltip }, scales: { x: rtl, y: money } }
      }));
    }
    if (this.hourCanvas && r.byHour.length) {
      this.charts.push(new Chart(this.hourCanvas.nativeElement, {
        type: 'bar',
        data: {
          labels: r.byHour.map(h => this.hourLabel(h.hour)),
          datasets: [{ label: 'عدد العمليات', data: r.byHour.map(h => h.count), backgroundColor: '#24845D', borderRadius: 6, maxBarThickness: 28 }]
        },
        options: {
          locale: 'en-US', responsive: true, maintainAspectRatio: false,
          plugins: { legend: { display: false }, tooltip: { rtl: true, titleFont: CHART_FONT, bodyFont: CHART_FONT } },
          scales: { x: rtl, y: { beginAtZero: true, position: 'right', ticks: { precision: 0, font: CHART_FONT } } }
        }
      }));
    }
  }
}
