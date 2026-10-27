import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { NgIf, NgFor } from '@angular/common';
import { Icon } from '@shared/ui/icon';
import { SarPipe } from '@shared/pipes/format.pipes';
import { ToastService } from '@core/state/toast.service';
/** Big financial value card for dashboards. */
@Component({
  selector: 'app-stat-card',
  standalone: true,
  imports: [NgIf, Icon, SarPipe],
  template: `
    <div class="stat" [class]="'stat-' + tone">
      <div class="stat-head">
        <span class="stat-label">{{ label }}</span>
        <app-icon [name]="icon" [size]="19"></app-icon>
      </div>
      <div class="stat-value money" [class.negative]="value < 0">{{ format === 'number' ? value : (value | sar) }}</div>
      <div class="stat-hint">{{ hint }}</div>
      <div class="stat-sub" *ngIf="sub">
        <span [class]="subTone === 'up' ? 'text-green' : subTone === 'down' ? 'text-red' : 'text-2'">{{ sub }}</span>
      </div>
    </div>
  `,
  styles: [`
    .stat { background: var(--card); border: 1px solid var(--border); border-radius: var(--radius); padding: 16px; box-shadow: var(--shadow); }
    .stat-head { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-bottom: 10px; }
    .stat-label { font-size: .92rem; font-weight: 600; color: var(--text-2); }
    .stat-icon { color: var(--text-2); }
    .stat-value { font-size: 1.75rem; font-weight: 800; color: var(--text); line-height: 1.25; }
    .stat-value.negative { color: var(--red); }
    .stat-hint { font-size: .8rem; color: var(--text-2); margin-top: 4px; min-height: 20px; }
    .stat-sub { margin-top: 6px; font-size: .85rem; font-weight: 600; }
    .stat-blue .stat-icon { color: var(--blue); }
    .stat-green .stat-icon { color: var(--green); }
    .stat-orange .stat-icon { color: var(--orange); }
    .stat-red .stat-icon { color: var(--red); }
    .stat-navy .stat-icon { color: var(--navy); }
    @media (max-width: 720px) {
      .stat { padding: 12px; }
      .stat-value { font-size: 1.2rem; }
      .stat-hint { display: none; }
      .stat-label { font-size: .8rem; }
    }
  `]
})
export class StatCard {
  @Input() label = '';
  @Input() value = 0;
  @Input() hint = '';
  @Input() icon = 'chart';
  @Input() tone: 'blue' | 'green' | 'orange' | 'red' | 'navy' = 'blue';
  @Input() sub?: string;
  @Input() subTone?: 'up' | 'down' | 'flat';
  @Input() format: 'money' | 'number' = 'money';
}

/** Empty state block with a call to action. */
@Component({
  selector: 'app-empty',
  standalone: true,
  imports: [NgIf, Icon],
  template: `
    <div class="empty">
      <div class="empty-icon"><app-icon [name]="icon" [size]="30"></app-icon></div>
      <div class="empty-title">{{ title }}</div>
      <p class="text-2">{{ message }}</p>
      <div class="empty-action" *ngIf="actionLabel">
        <button class="btn btn-primary" (click)="action.emit()">{{ actionLabel }}</button>
      </div>
    </div>
  `
})
export class EmptyState {
  @Input() icon = 'alert';
  @Input() title = '';
  @Input() message = '';
  @Input() actionLabel = '';
  @Output() action = new EventEmitter<void>();
}

/** Modal dialog. */
@Component({
  selector: 'app-modal',
  standalone: true,
  imports: [NgIf, Icon],
  template: `
    <div class="modal-backdrop" (click)="onBackdrop($event)">
      <div class="modal" role="dialog" aria-modal="true">
        <div class="modal-head">
          <h2>{{ title }}</h2>
          <button class="icon-btn" style="color: var(--text-2)" (click)="close.emit()">
            <app-icon name="x" [size]="20"></app-icon>
          </button>
        </div>
        <div class="modal-body"><ng-content></ng-content></div>
        <div class="modal-foot" *ngIf="showFooter"><ng-content select="[footer]"></ng-content></div>
      </div>
    </div>
  `
})
export class Modal {
  @Input() title = '';
  @Input() showFooter = true;
  @Output() close = new EventEmitter<void>();

  onBackdrop(e: MouseEvent): void {
    if ((e.target as HTMLElement).classList.contains('modal-backdrop')) this.close.emit();
  }
}

/** Inline loading row. */
@Component({
  selector: 'app-loading',
  standalone: true,
  template: `<div class="loading-row"><span class="spinner spinner-dark"></span><span>{{ message }}</span></div>`
})
export class LoadingRow {
  @Input() message = 'جارٍ التحميل…';
}

/** Toast host — rendered once in the app root. */
@Component({
  selector: 'app-toasts',
  standalone: true,
  imports: [NgFor, Icon],
  template: `
    <div class="toast-wrap">
      <div *ngFor="let t of toasts().slice(-3)" class="toast" [class]="'toast-' + t.kind">
        <app-icon [name]="t.kind === 'success' ? 'checkCircle' : t.kind === 'error' ? 'alert' : 'info'" [size]="19"></app-icon>
        <span class="grow">{{ t.message }}</span>
        <button class="icon-btn" style="width: 28px; height: 28px; color: #fff" (click)="dismiss(t.id)">
          <app-icon name="x" [size]="15"></app-icon>
        </button>
      </div>
    </div>
  `
})
export class Toasts {
  private service = inject(ToastService);
  readonly toasts = this.service.toasts;
  dismiss(id: number): void { this.service.dismiss(id); }
}
