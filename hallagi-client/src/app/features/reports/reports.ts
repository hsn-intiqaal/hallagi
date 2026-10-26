import { NgIf, NgFor } from '@angular/common';
import { Component, ElementRef, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '@core/http/api.service';
import { BranchContext } from '@core/state/branch-context';
import { ToastService } from '@core/state/toast.service';
import { SarPipe, PctPipe } from '@shared/pipes/format.pipes';
import { Icon } from '@shared/ui/icon';
import { LoadingRow, StatCard, EmptyState } from '@shared/ui/ui';
import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [FormsModule, Icon, SarPipe, PctPipe, LoadingRow, StatCard, EmptyState, NgIf, NgFor],
  templateUrl: './reports.html',
  styleUrl: './reports.scss'
})
export class Reports implements OnInit {
  private api = inject(ApiService);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);

  readonly report = signal<any>(null);
  readonly expensesReport = signal<any>(null);
  readonly duesReport = signal<any>(null);
  readonly branchesReport = signal<any>(null);
  readonly loading = signal(true);
  from = '';
  to = '';
  @ViewChild('chartCanvas', { static: false }) canvasRef?: ElementRef<HTMLCanvasElement>;
  private chart?: Chart;

  svcGroups(r: any): { label: string; total: number; count: number }[] {
    return (r?.byService ?? []) as { label: string; total: number; count: number }[];
  }

  readonly maxSvc = computed(() => Math.max(1, ...this.svcGroups(this.report()).map(g => g.total)));

  ngOnInit(): void {
    const to = new Date();
    const from = new Date();
    from.setDate(from.getDate() - 6);
    this.from = from.toISOString().slice(0, 10);
    this.to = to.toISOString().slice(0, 10);
    this.load();
  }

  load(): void {
    const branchId = this.branches.scopeId();
    const from = `${this.from}T00:00:00`;
    const to = `${this.to}T23:59:59`;
    this.loading.set(true);
    this.api.get<any>('/reports/sales', { branchId, from, to, groupBy: 'day' }).subscribe({
      next: sales => {
        this.report.set(sales);
        this.api.get<any>('/reports/expenses', { branchId, from, to }).subscribe({ next: e => this.expensesReport.set(e) });
        this.api.get<any>('/reports/barber-dues', { branchId, from, to }).subscribe({
          next: d => this.duesReport.set(d),
          error: () => this.duesReport.set(null)
        });
        this.api.get<any>('/reports/branches', { from, to }).subscribe({
          next: b => this.branchesReport.set(b),
          error: () => this.branchesReport.set(null)
        });
        this.loading.set(false);
        setTimeout(() => this.renderChart(), 50);
      },
      error: (e: any) => { this.loading.set(false); this.toast.error(e?.message ?? 'تعذّر تحميل التقارير'); }
    });
  }

  private renderChart(): void {
    const canvas = this.canvasRef?.nativeElement;
    const items: { label: string; total: number }[] = this.report()?.groups ?? [];
    if (!canvas || !items.length) return;
    this.chart?.destroy();
    this.chart = new Chart(canvas, {
      type: 'bar',
      data: {
        labels: items.map(i => i.label),
        datasets: [{
          label: 'المبيعات',
          data: items.map(i => i.total),
          backgroundColor: '#2563EB',
          borderRadius: 6
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
          y: { beginAtZero: true, ticks: { callback: (v: any) => `${v} ر.س` } },
          x: { grid: { display: false } }
        }
      }
    });
  }
}
