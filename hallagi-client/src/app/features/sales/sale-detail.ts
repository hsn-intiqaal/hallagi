import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiFailure } from '@core/http/api.service';
import { SalesApi } from '@core/api/sales.api';
import { ToastService } from '@core/state/toast.service';
import { Sale } from '@core/models';
import { Icon } from '@shared/ui/icon';
import { DateTimeArPipe, SarPipe } from '@shared/pipes/format.pipes';
import { LoadingRow } from '@shared/ui/ui';
import { QrCode } from '@shared/ui/qr-code';

@Component({
  selector: 'app-sale-detail',
  standalone: true,
  imports: [Icon, DateTimeArPipe, SarPipe, LoadingRow, QrCode, NgIf, NgFor],
  templateUrl: './sale-detail.html',
  styleUrl: './sale-detail.scss'
})
export class SaleDetail implements OnInit {
  private salesApi = inject(SalesApi);
  private route = inject(ActivatedRoute);
  private toast = inject(ToastService);
  readonly router = inject(Router);

  readonly sale = signal<Sale | null>(null);
  readonly loading = signal(true);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.salesApi.get(id).subscribe({
      next: s => { this.sale.set(s); this.loading.set(false); },
      error: (e: ApiFailure) => { this.toast.error(e?.message ?? 'تعذّر تحميل العملية'); this.loading.set(false); }
    });
  }

  print(): void { window.print(); }
  back(): void { this.router.navigate(['/sales']); }
}
