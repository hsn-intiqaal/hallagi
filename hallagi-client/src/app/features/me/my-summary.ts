import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ApiService } from '@core/http/api.service';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/state/toast.service';
import { SarPipe, DateShortPipe, PctPipe } from '@shared/pipes/format.pipes';
import { Icon } from '@shared/ui/icon';
import { LoadingRow, EmptyState } from '@shared/ui/ui';

@Component({
  selector: 'app-my-summary',
  standalone: true,
  imports: [Icon, SarPipe, DateShortPipe, PctPipe, LoadingRow, EmptyState, NgIf, NgFor],
  templateUrl: './my-summary.html',
  styleUrl: './my-summary.scss'
})
export class MySummary implements OnInit {
  private api = inject(ApiService);
  readonly auth = inject(AuthService);
  private toast = inject(ToastService);

  readonly summary = signal<any>(null);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.api.get<any>('/me/summary').subscribe({
      next: s => { this.summary.set(s); this.loading.set(false); },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل بياناتك'); this.loading.set(false); }
    });
  }
}
