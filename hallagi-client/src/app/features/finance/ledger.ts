import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NgIf, NgFor } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService, ApiFailure } from '@core/http/api.service';
import { BarberDetail, BarberLedger as LedgerView, LedgerAdvance, LedgerEntry } from '@core/models';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/state/toast.service';
import { DateArPipe, SarPipe } from '@shared/pipes/format.pipes';
import { Icon } from '@shared/ui/icon';
import { EmptyState, LoadingRow, Modal } from '@shared/ui/ui';

@Component({
  selector: 'app-barber-ledger',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, Icon, DateArPipe, SarPipe, EmptyState, LoadingRow, Modal],
  templateUrl: './ledger.html',
  styleUrl: './ledger.scss'
})
export class BarberLedger implements OnInit {
  private api = inject(ApiService);
  readonly auth = inject(AuthService);
  private toast = inject(ToastService);
  readonly router = inject(Router);
  private route = inject(ActivatedRoute);

  readonly barberId = signal<string>('');
  readonly barberName = signal('');
  readonly balance = signal(0);
  readonly entries = signal<LedgerEntry[]>([]);
  readonly advances = signal<LedgerAdvance[]>([]);
  readonly loading = signal(true);

  readonly showAdvance = signal(false);
  advance = { amount: 0, reason: '' };

  // follows the session: correct even when the profile finishes loading after this page
  readonly canManage = computed(() => this.auth.can('ledger.view'));
  readonly reversingId = signal<string | null>(null);
  readonly reverseReason = '';

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.barberId.set(id);
    this.load();
  }

  load(): void {
    const id = this.barberId();
    this.loading.set(true);
    this.api.get<LedgerView>(`/ledger`, { barberId: id }).subscribe({
      next: res => {
        this.balance.set(res.balance);
        this.entries.set(res.entries ?? []);
        this.advances.set(res.advances ?? []);
        this.loading.set(false);
      },
      error: (e: ApiFailure) => { this.loading.set(false); this.toast.error(e?.message ?? 'تعذّر تحميل السجل المالي'); }
    });
    this.api.get<BarberDetail>(`/barbers/${id}`).subscribe({
      next: b => this.barberName.set(b.barber.fullName),
      error: () => this.barberName.set('')
    });
  }

  openAdvance(): void { this.advance = { amount: 0, reason: '' }; this.showAdvance.set(true); }

  saveAdvance(): void {
    if (!this.advance.amount || this.advance.amount <= 0) { this.toast.error('أدخل مبلغًا صحيحًا'); return; }
    this.api.post('/ledger/advances', { barberId: this.barberId(), amount: this.advance.amount, reason: this.advance.reason }).subscribe({
      next: () => { this.toast.success('تم تسجيل السلفة'); this.showAdvance.set(false); this.load(); },
      error: (e: ApiFailure) => this.toast.error(e?.message ?? 'تعذّر تسجيل السلفة')
    });
  }

  repay(advanceId: string): void {
    this.api.post(`/ledger/advances/${advanceId}/repay`, { amount: this.advances().find(a => a.id === advanceId)?.outstanding ?? 0 }).subscribe({
      next: () => { this.toast.success('تم تسجيل سداد السلفة'); this.load(); },
      error: (e: ApiFailure) => this.toast.error(e?.message ?? 'تعذّر تسجيل السداد')
    });
  }

  reverse(entryId: string): void {
    this.reversingId.set(entryId);
  }

  confirmReverse(): void {
    const id = this.reversingId();
    if (!id) return;
    this.api.post(`/ledger/entries/${id}/reverse`, { reason: 'تصحيح بإجراء المالك' }).subscribe({
      next: () => { this.toast.success('تم تسجيل التصحيح'); this.reversingId.set(null); this.load(); },
      error: (e: ApiFailure) => this.toast.error(e?.message ?? 'تعذّر تسجيل التصحيح')
    });
  }
}
