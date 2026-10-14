import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { ToastService } from '../../core/toast.service';
import { Sale } from '../../core/models';
import { Icon } from '../../shared/icon';
import { DateTimeArPipe, SarPipe } from '../../core/format.pipes';
import { LoadingRow } from '../../shared/ui';

@Component({
  selector: 'app-sale-detail',
  standalone: true,
  imports: [Icon, DateTimeArPipe, SarPipe, LoadingRow, NgIf, NgFor],
  templateUrl: './sale-detail.html',
  styleUrl: './sale-detail.scss'
})
export class SaleDetail implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private toast = inject(ToastService);
  readonly router = inject(Router);

  readonly sale = signal<Sale | null>(null);
  readonly loading = signal(true);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.api.get<Sale>(`/sales/${id}`).subscribe({
      next: s => { this.sale.set(s); this.loading.set(false); },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل العملية'); this.loading.set(false); }
    });
  }

  print(): void { window.print(); }
  back(): void { this.router.navigate(['/sales']); }
}
