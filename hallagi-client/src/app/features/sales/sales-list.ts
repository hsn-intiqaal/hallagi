import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '@core/http/api.service';
import { BranchContext } from '@core/state/branch-context';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/state/toast.service';
import { Sale } from '@core/models';
import { Icon } from '@shared/ui/icon';
import { DateTimeArPipe, SarPipe } from '@shared/pipes/format.pipes';
import { EmptyState, LoadingRow } from '@shared/ui/ui';

@Component({
  selector: 'app-sales-list',
  standalone: true,
  imports: [FormsModule, RouterLink, Icon, DateTimeArPipe, SarPipe, EmptyState, LoadingRow, NgIf, NgFor],
  templateUrl: './sales-list.html',
  styleUrl: './sales-list.scss'
})
export class SalesList implements OnInit {
  private api = inject(ApiService);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);
  readonly auth = inject(AuthService);
  readonly router = inject(Router);

  readonly sales = signal<Sale[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(true);
  page = 1;
  readonly pageSize = 20;

  search = '';
  from = '';
  to = '';
  cancelling: Sale | null = null;
  cancelReason = '';

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    const branchId = this.branches.scopeId();
    this.api.get<any>('/sales', {
      branchId, search: this.search, from: this.from || undefined, to: this.to || undefined,
      page: this.page, pageSize: this.pageSize
    }).subscribe({
      next: (res: any) => { this.sales.set(res.items); this.totalCount.set(res.totalCount); this.loading.set(false); },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل المبيعات'); this.loading.set(false); }
    });
  }

  searchNow(): void { this.page = 1; this.load(); }

  prevPage(): void { if (this.page > 1) { this.page--; this.load(); } }
  nextPage(): void { if (this.page * this.pageSize < this.totalCount()) { this.page++; this.load(); } }
  get pages(): number { return Math.max(1, Math.ceil(this.totalCount() / this.pageSize)); }

  open(sale: Sale): void { this.router.navigate(['/sales', sale.id]); }

  servicesText(sale: Sale): string {
    if (sale.servicesText) return sale.servicesText;
    return (sale.items ?? []).map(i => i.serviceName + (i.quantity > 1 ? ` ×${i.quantity}` : '')).join(' + ');
  }

  exportCsv(): void {
    this.api.download('/sales/export.csv', { branchId: this.branches.scopeId(), from: this.from, to: this.to }, `sales-${this.from || 'all'}.csv`)
      .subscribe({ error: (e: { message: string }) => this.toast.error(e.message) });
  }

  canCancel(): boolean { return this.auth.can('sale.void'); }

  askCancel(sale: Sale, event: Event): void {
    event.stopPropagation();
    this.cancelling = sale;
    this.cancelReason = '';
  }

  confirmCancel(): void {
    if (!this.cancelling) return;
    if (!this.cancelReason.trim()) { this.toast.error('اكتب سبب الإلغاء'); return; }
    this.api.post(`/sales/${this.cancelling.id}/cancel`, { reason: this.cancelReason.trim() }).subscribe({
      next: () => {
        this.toast.success('تم إلغاء العملية وحفظ سبب الإلغاء');
        this.cancelling = null;
        this.load();
      },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر إلغاء العملية')
    });
  }
}
