import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NgIf, NgFor } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { endOfMonth, startOfMonth } from '@core/time/local-date';
import { ApiService } from '@core/http/api.service';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/state/toast.service';
import { DateShortPipe, SarPipe } from '@shared/pipes/format.pipes';
import { Icon } from '@shared/ui/icon';
import { EmptyState, LoadingRow, Modal } from '@shared/ui/ui';

interface Period { id: string; name: string; startDate: string; endDate: string; status: string; settlementsCount: number; draftCount: number; approvedCount: number; paidCount: number; totalNet: number; }
interface Settlement {
  id: string; barberId: string; barberName: string; status: string; statusText: string;
  salaryEarned: number; commissionEarned: number; bonus: number; reimbursements: number;
  deductions: number; advancesRepaid: number; chairRent: number; netPayable: number;
  alreadyPaid: number; remaining: number; items?:  { component: string; amount: number; explanation: string | null }[];
  payments: { id: string; amount: number; paymentDate: string; method: string | null }[];
}

@Component({
  selector: 'app-settlements',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, Icon, DateShortPipe, SarPipe, EmptyState, LoadingRow, Modal],
  templateUrl: './settlements.html',
  styleUrl: './settlements.scss'
})
export class Settlements implements OnInit {
  private api = inject(ApiService);
  readonly auth = inject(AuthService);
  private toast = inject(ToastService);

  readonly periods = signal<Period[]>([]);
  readonly selectedPeriod = signal<Period | null>(null);
  readonly settlements = signal<Settlement[]>([]);
  readonly loading = signal(true);
  readonly previewing = signal(false);
  readonly workingId = signal<string | null>(null);

  readonly showNewPeriod = signal(false);
  newPeriod = { name: '', startDate: '', endDate: '' };

  readonly payTarget = signal<Settlement | null>(null);
  payAmount: number | null = null;
  payMethod = 'Cash';

  readonly cancelTarget = signal<Settlement | null>(null);
  cancelReason = '';

  readonly canApprove = computed(() => this.auth.can('settlement.approve'));
  readonly canPay = computed(() => this.auth.can('settlement.pay'));

  ngOnInit(): void { this.loadPeriods(); }

  loadPeriods(): void {
    this.loading.set(true);
    this.api.get<{ items: Period[] }>('/settlements/periods').subscribe({
      next: res => {
        this.periods.set(res.items);
        if (res.items.length > 0) this.selectPeriod(res.items[0]);
        else { this.selectedPeriod.set(null); this.settlements.set([]); this.loading.set(false); }
      },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل الفترات المالية'); this.loading.set(false); }
    });
  }

  selectPeriod(p: Period): void {
    this.selectedPeriod.set(p);
    this.api.get<{ items: Settlement[] }>('/settlements', { periodId: p.id }).subscribe({
      next: res => { this.settlements.set(res.items); this.loading.set(false); },
      error: () => { this.settlements.set([]); this.loading.set(false); }
    });
  }

  selectPeriodById(id: string): void {
    const p = this.periods().find(x => x.id === id);
    if (p) this.selectPeriod(p);
  }

  openNewPeriod(): void {
    const today = this.auth.today();
    this.newPeriod = {
      name: new Date(today + 'T12:00:00').toLocaleDateString('ar-SA-u-nu-latn-ca-gregory', { month: 'long', year: 'numeric' }),
      startDate: startOfMonth(today),
      endDate: endOfMonth(today)
    };
    this.showNewPeriod.set(true);
  }

  createPeriod(): void {
    if (!this.newPeriod.name.trim() || !this.newPeriod.startDate || !this.newPeriod.endDate) {
      this.toast.error('أكمل اسم الفترة والتواريخ');
      return;
    }
    this.api.post<any>('/settlements/periods', this.newPeriod).subscribe({
      next: () => { this.toast.success('تم إنشاء الفترة المالية'); this.showNewPeriod.set(false); this.loadPeriods(); },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر إنشاء الفترة')
    });
  }

  preview(): void {
    const p = this.selectedPeriod();
    if (!p) return;
    this.previewing.set(true);
    this.api.post<any>(`/settlements/periods/${p.id}/preview`, {}).subscribe({
      next: (res: any) => {
        this.settlements.set(res.settlements ?? []);
        this.previewing.set(false);
        this.toast.success(`تم حساب ${res.settlements?.length ?? 0} تسوية (مسودات)`);
      },
      error: (e: any) => { this.previewing.set(false); this.toast.error(e?.message ?? 'تعذّر حساب التسويات'); }
    });
  }

  approve(s: Settlement): void {
    this.workingId.set(s.id);
    this.api.post(`/settlements/${s.id}/approve`, {}).subscribe({
      next: () => {
        this.toast.success('تم اعتماد التسوية وقفلها');
        this.workingId.set(null);
        this.reload();
      },
      error: (e: any) => { this.workingId.set(null); this.toast.error(e?.message ?? 'تعذّر اعتماد التسوية'); }
    });
  }

  askPay(s: Settlement): void {
    this.payTarget.set(s);
    this.payAmount = s.remaining;
    this.payMethod = 'Cash';
  }

  confirmPay(): void {
    const s = this.payTarget();
    if (!s || !this.payAmount || this.payAmount <= 0) { this.toast.error('أدخل مبلغًا صحيحًا'); return; }
    this.workingId.set(s.id);
    this.api.post(`/settlements/${s.id}/pay`, { amount: this.payAmount, method: this.payMethod }).subscribe({
      next: () => { this.toast.success('تم تسجيل الدفعة'); this.payTarget.set(null); this.workingId.set(null); this.reload(); },
      error: (e: any) => { this.workingId.set(null); this.toast.error(e?.message ?? 'تعذّر تسجيل الدفعة'); }
    });
  }

  askCancel(s: Settlement): void { this.cancelTarget.set(s); this.cancelReason = ''; }

  confirmCancel(): void {
    const s = this.cancelTarget();
    if (!s) return;
    if (!this.cancelReason.trim()) { this.toast.error('اكتب سبب الإلغاء'); return; }
    this.api.post(`/settlements/${s.id}/cancel`, { reason: this.cancelReason.trim() }).subscribe({
      next: () => { this.toast.success('تم إلغاء التسوية'); this.cancelTarget.set(null); this.reload(); },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر إلغاء التسوية')
    });
  }

  private reload(): void {
    const p = this.selectedPeriod();
    if (p) this.selectPeriod(p);
  }

  statusClass(status: string): string {
    return status === 'Draft' ? 'badge-draft' : status === 'Approved' ? 'badge-approved'
      : status === 'Paid' ? 'badge-paid' : 'badge-cancelled';
  }
}
