import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { ToastService } from '../../core/toast.service';
import { CustomerRow } from '../../core/models';
import { Icon } from '../../shared/icon';
import { SarPipe, DateShortPipe } from '../../core/format.pipes';
import { EmptyState, LoadingRow, Modal } from '../../shared/ui';

@Component({
  selector: 'app-customers-list',
  standalone: true,
  imports: [FormsModule, Icon, SarPipe, DateShortPipe, EmptyState, LoadingRow, Modal, NgIf, NgFor],
  templateUrl: './customers-list.html',
  styleUrl: './customers-list.scss'
})
export class CustomersList implements OnInit {
  private api = inject(ApiService);
  private toast = inject(ToastService);
  readonly router = inject(Router);

  readonly customers = signal<CustomerRow[]>([]);
  readonly loading = signal(true);
  search = '';
  page = 1;
  readonly pageSize = 20;
  readonly total = signal(0);

  readonly showAdd = signal(false);
  form = { fullName: '', phone: '', notes: '' };

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.get<any>('/customers', { search: this.search, page: this.page, pageSize: this.pageSize }).subscribe({
      next: (res: any) => { this.customers.set(res.items); this.total.set(res.totalCount); this.loading.set(false); },
      error: (e: any) => { this.toast.error(e?.message ?? 'تعذّر تحميل العملاء'); this.loading.set(false); }
    });
  }

  searchNow(): void { this.page = 1; this.load(); }
  open(id: string): void { this.router.navigate(['/customers', id]); }

  save(): void {
    if (!this.form.fullName.trim()) { this.toast.error('اسم العميل مطلوب'); return; }
    this.api.post<any>('/customers', {
      fullName: this.form.fullName.trim(),
      phone: this.form.phone.trim() || null,
      notes: this.form.notes.trim() || null
    }).subscribe({
      next: (res: any) => {
        this.toast.success('تمت إضافة العميل بنجاح');
        this.showAdd.set(false);
        this.form = { fullName: '', phone: '', notes: '' };
        this.router.navigate(['/customers', res.id]);
      },
      error: (e: any) => this.toast.error(e?.message ?? 'تعذّر إضافة العميل')
    });
  }

  get pages(): number { return Math.max(1, Math.ceil(this.total() / this.pageSize)); }
  nextPage(): void { if (this.page < this.pages) { this.page++; this.load(); } }
  prevPage(): void { if (this.page > 1) { this.page--; this.load(); } }
}
