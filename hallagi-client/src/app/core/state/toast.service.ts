import { inject, Injectable, signal } from '@angular/core';

export interface Toast { id: number; kind: 'success' | 'error' | 'info'; message: string; }

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly toasts = signal<Toast[]>([]);
  private seq = 1;

  success(message: string): void { this.show('success', message); }
  error(message: string): void { this.show('error', message); }
  info(message: string): void { this.show('info', message); }

  private show(kind: Toast['kind'], message: string): void {
    const id = this.seq++;
    this.toasts.update(list => [...list, { id, kind, message }]);
    setTimeout(() => this.dismiss(id), kind === 'error' ? 6500 : 4200);
  }

  dismiss(id: number): void {
    this.toasts.update(list => list.filter(t => t.id !== id));
  }
}
