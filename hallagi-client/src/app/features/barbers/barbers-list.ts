import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '@core/http/api.service';
import { BranchContext } from '@core/state/branch-context';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/state/toast.service';
import { BarberRow } from '@features/barbers/models-barbers';
import { Icon } from '@shared/ui/icon';
import { SarPipe, PctPipe } from '@shared/pipes/format.pipes';
import { EmptyState, LoadingRow } from '@shared/ui/ui';

@Component({
  selector: 'app-barbers-list',
  standalone: true,
  imports: [Icon, SarPipe, EmptyState, LoadingRow, NgIf, NgFor],
  templateUrl: './barbers-list.html',
  styleUrl: './barbers-list.scss'
})
export class BarbersList implements OnInit {
  private api = inject(ApiService);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);
  readonly auth = inject(AuthService);
  readonly router = inject(Router);

  readonly barbers = signal<BarberRow[]>([]);
  readonly loading = signal(true);
  readonly canManage = signal(false);

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.get<{ items: BarberRow[] }>('/barbers', { branchId: this.branches.scopeId() }).subscribe({
      next: res => {
        this.barbers.set(res.items);
        this.canManage.set(this.auth.can('barber.manage'));
        this.loading.set(false);
      },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل الحلاقين'); this.loading.set(false); }
    });
  }

  open(id: string): void { this.router.navigate(['/barbers', id]); }
  add(): void { this.router.navigate(['/barbers', 'new']); }
}
