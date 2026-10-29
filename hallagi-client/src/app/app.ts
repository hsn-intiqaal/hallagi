import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Toasts } from '@shared/ui/ui';
import { AppShellService } from '@core/platform/app-shell.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Toasts],
  template: `
    @if (!shell.online()) {
      <div class="net-banner" role="status">لا يوجد اتصال بالإنترنت — ستعود البيانات للتحديث تلقائيًا عند عودة الاتصال</div>
    }
    <router-outlet />
    @if (shell.updateReady()) {
      <div class="update-bar" role="alert">
        <span>يتوفر إصدار جديد من التطبيق</span>
        <button class="btn btn-sm btn-primary" (click)="shell.applyUpdate()">تحديث الآن</button>
      </div>
    }
    <app-toasts />
  `,
  styleUrl: './app.scss'
})
export class App implements OnInit {
  readonly shell = inject(AppShellService);

  ngOnInit(): void {
    this.shell.init();
  }
}
