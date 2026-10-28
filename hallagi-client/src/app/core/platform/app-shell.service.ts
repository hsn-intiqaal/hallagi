import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Location } from '@angular/common';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { filter, fromEvent, merge } from 'rxjs';
import { APP_CONFIG } from '@core/config/app-config';
import { ThemeService } from '@core/state/theme.service';
import { initNativeShell } from './native';

/**
 * Application-shell concerns shared by the browser, the installed PWA and the
 * native shells: connectivity, new-version availability and native hooks.
 */
@Injectable({ providedIn: 'root' })
export class AppShellService {
  private readonly updates = inject(SwUpdate);
  private readonly location = inject(Location);
  private readonly config = inject(APP_CONFIG);
  private readonly destroyRef = inject(DestroyRef);
  // constructing the theme applies the deployment palette before the first screen paints
  private readonly theme = inject(ThemeService);

  readonly online = signal(navigator.onLine);
  readonly updateReady = signal(false);

  init(): void {
    merge(fromEvent(window, 'online'), fromEvent(window, 'offline'))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.online.set(navigator.onLine));

    if (this.updates.isEnabled) {
      this.updates.versionUpdates
        .pipe(filter((e): e is VersionReadyEvent => e.type === 'VERSION_READY'), takeUntilDestroyed(this.destroyRef))
        .subscribe(() => this.updateReady.set(true));
      // long-lived tablets at the counter: look for a new release every 30 minutes
      const timer = setInterval(() => this.updates.checkForUpdate().catch(() => undefined), 30 * 60_000);
      this.destroyRef.onDestroy(() => clearInterval(timer));
    }

    void initNativeShell(() => this.location.back(), this.config.brand.secondaryColor);
  }

  async applyUpdate(): Promise<void> {
    await this.updates.activateUpdate().catch(() => undefined);
    document.location.reload();
  }
}
