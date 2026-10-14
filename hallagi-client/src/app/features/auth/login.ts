import { NgIf } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { BranchContext } from '../../core/branch-context';
import { ToastService } from '../../core/toast.service';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, Icon, NgIf],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class Login {
  private auth = inject(AuthService);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);
  readonly router = inject(Router);

  readonly loading = signal(false);
  readonly showPassword = signal(false);
  readonly mode = signal<'login' | 'register'>('login');

  email = '';
  password = '';
  fullName = '';
  phone = '';
  organizationName = '';
  city = '';
  branchName = '';

  submit(): void {
    if (this.loading()) return;
    if (!this.email.trim() || !this.password) {
      this.toast.error('أدخل البريد الإلكتروني وكلمة المرور');
      return;
    }
    this.loading.set(true);

    const onDone = () => {
      this.loading.set(false);
      this.auth.loadMe().subscribe({
        next: () => {
          this.branches.init();
          this.router.navigate([this.auth.landingRoute()]);
        },
        error: () => this.toast.error('تعذّر تحميل بيانات الحساب. حاول مرة أخرى')
      });
    };
    const onError = (e: any) => {
      this.loading.set(false);
      this.toast.error(e?.message ?? 'تعذّر تسجيل الدخول. تحقق من البيانات وحاول مرة أخرى');
    };

    if (this.mode() === 'login') {
      this.auth.login(this.email.trim(), this.password).subscribe({
        next: onDone,
        error: onError
      });
    } else {
      if (!this.fullName.trim() || !this.organizationName.trim() || !this.city.trim() || !this.branchName.trim()) {
        this.loading.set(false);
        this.toast.error('أكمل الحقول المطلوبة: الاسم، اسم المحل، المدينة، الفرع');
        return;
      }
      this.auth.registerOwner({
        fullName: this.fullName.trim(),
        email: this.email.trim(),
        password: this.password,
        phone: this.phone.trim() || undefined,
        organizationName: this.organizationName.trim(),
        city: this.city.trim(),
        branchName: this.branchName.trim()
      }).subscribe({ next: onDone, error: onError });
    }
  }

  switchMode(mode: 'login' | 'register'): void {
    this.mode.set(mode);
  }
}
