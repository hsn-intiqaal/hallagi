import { NgIf, NgFor } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { BranchContext } from '../../core/branch-context';
import { ToastService } from '../../core/toast.service';
import { Icon } from '../../shared/icon';
import { SarPipe } from '../../core/format.pipes';

const SUGGESTED_SERVICES = [
  { name: 'حلاقة شعر', price: 25 },
  { name: 'حلاقة ذقن', price: 15 },
  { name: 'شعر وذقن', price: 35 },
  { name: 'تنظيف بشرة', price: 30 },
  { name: 'صبغة شعر', price: 50 },
  { name: 'حلاقة أطفال', price: 20 }
];

@Component({
  selector: 'app-onboarding',
  standalone: true,
  imports: [FormsModule, Icon, SarPipe, NgIf, NgFor],
  templateUrl: './onboarding.html',
  styleUrl: './onboarding.scss'
})
export class Onboarding {
  private api = inject(ApiService);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);
  readonly router = inject(Router);

  readonly step = signal(1);
  readonly suggested = SUGGESTED_SERVICES;
  selectedServices: string[] = SUGGESTED_SERVICES.map(s => s.name);
  barberName = '';
  customServices = '';

  toggleService(s: string): void {
    this.selectedServices = this.selectedServices.includes(s)
      ? this.selectedServices.filter(x => x !== s)
      : [...this.selectedServices, s];
  }

  saveAndFinish(): void {
    const names = [...this.selectedServices, ...this.customServices.split(/[،,\n]/).map(s => s.trim()).filter(Boolean)];
    this.api.post<any>('/settings/onboarding', {
      serviceNames: names,
      barbers: this.barberName.trim() ? [{
        fullName: this.barberName.trim(),
        phone: null,
        startDate: null,
        active: true,
        branchIds: [this.branches.scopeId() ?? ''],
        compensation: null
      }] : []
    }).subscribe({
      next: () => {
        this.toast.success('تم حفظ بيانات الإعداد');
        this.router.navigate(['/pos']);
      },
      error: () => this.router.navigate(['/pos'])
    });
  }

  skip(): void {
    this.router.navigate(['/pos']);
  }

  next(): void {
    if (this.step() < 3) this.step.set(this.step() + 1);
    else this.saveAndFinish();
  }

  back(): void {
    if (this.step() > 1) this.step.set(this.step() - 1);
    else this.router.navigate(['/dashboard']);
  }
}
