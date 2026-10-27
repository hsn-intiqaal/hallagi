import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '@core/http/api.service';
import { BranchContext } from '@core/state/branch-context';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/state/toast.service';
import { TreasuryState, ShiftRow } from '@core/models';
import { Icon } from '@shared/ui/icon';
import { DateTimeArPipe, SarPipe, DateShortPipe } from '@shared/pipes/format.pipes';
import { EmptyState, LoadingRow, Modal } from '@shared/ui/ui';

@Component({
  selector: 'app-treasury',
  standalone: true,
  imports: [FormsModule, Icon, DateTimeArPipe, SarPipe, DateShortPipe, LoadingRow, Modal, EmptyState, NgIf, NgFor],
  templateUrl: './treasury.html',
  styleUrl: './treasury.scss'
})
export class Treasury implements OnInit {
  private api = inject(ApiService);
  readonly branches = inject(BranchContext);
  readonly auth = inject(AuthService);
  private toast = inject(ToastService);

  readonly state = signal<TreasuryState | null>(null);
  readonly history = signal<ShiftRow[]>([]);
  readonly loading = signal(true);
  readonly canOpen = signal(false);
  readonly canClose = signal(false);

  readonly showOpen = signal(false);
  readonly showClose = signal(false);
  openCash = 0;
  countedCash: number | null = null;
  closeNote = '';
  readonly closeSaving = signal(false);

  readonly difference = computed(() => {
    const s = this.state();
    if (!s?.current || this.countedCash === null) return null;
    return Math.round((this.countedCash - s.current.expectedCash) * 100) / 100;
  });

  ngOnInit(): void {
    this.canOpen.set(this.auth.can('shift.open'));
    this.canClose.set(this.auth.can('shift.close'));
    this.load();
  }

  load(): void {
    const id = this.branches.scopeId();
    if (!id) {
      // the drawer belongs to one branch: "all branches" has no single treasury
      this.loading.set(false);
      return;
    }
    this.loading.set(true);
    this.api.get<TreasuryState>('/treasury/state', { branchId: id }).subscribe({
      next: s => { this.state.set(s); this.loading.set(false); },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل بيانات الخزينة'); this.loading.set(false); }
    });
    this.api.get<any>('/treasury/shifts', { branchId: id, pageSize: 10 }).subscribe({
      next: (res: any) => this.history.set(res.items),
      error: () => { /* history is secondary */ }
    });
  }

  startOpen(): void { this.openCash = 0; this.showOpen.set(true); }

  confirmOpen(): void {
    if (this.openCash < 0) { this.toast.error('أدخل رصيدًا افتتاحيًا صحيحًا'); return; }
    this.api.post<any>('/treasury/open', { branchId: this.branches.scopeId(), openingCash: this.openCash }).subscribe({
      next: () => { this.toast.success('تم فتح الوردية بنجاح'); this.showOpen.set(false); this.load(); },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر فتح الوردية')
    });
  }

  startClose(): void {
    this.countedCash = null;
    this.closeNote = '';
    this.showClose.set(true);
  }

  confirmClose(): void {
    if (this.countedCash === null || this.countedCash < 0) { this.toast.error('أدخل النقدية التي عدّها الموظف'); return; }
    this.closeSaving.set(true);
    this.api.post<any>('/treasury/close', {
      branchId: this.branches.scopeId(),
      countedCash: this.countedCash,
      note: this.closeNote?.trim() || null
    }).subscribe({
      next: (res: any) => {
        this.closeSaving.set(false);
        this.showClose.set(false);
        this.toast.success(`تم إقفال الوردية — ${res.differenceText}`);
        this.load();
      },
      error: (e: any) => { this.closeSaving.set(false); this.toast.error(e?.message ?? 'تعذّر إقفال الوردية'); }
    });
  }
}
