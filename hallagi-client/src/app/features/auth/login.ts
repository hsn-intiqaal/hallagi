import { Component, inject, signal } from '@angular/core';
import { NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { BranchContext } from '@core/state/branch-context';
import { ToastService } from '@core/state/toast.service';
import { Icon } from '@shared/ui/icon';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [NgIf, FormsModule, Icon],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class Login {
  private auth = inject(AuthService);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);
  private router = inject(Router);

  readonly loading = signal(false);
  readonly showPassword = signal(false);

  email = '';
  password = '';

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

    this.auth.login(this.email.trim(), this.password).subscribe({ next: onDone, error: onError });
  }
}
