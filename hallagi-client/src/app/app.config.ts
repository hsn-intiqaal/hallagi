import { ApplicationConfig, isDevMode, provideZoneChangeDetection } from '@angular/core';
import { provideServiceWorker } from '@angular/service-worker';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from '@core/auth/auth.interceptor';
import { ToastService } from '@core/state/toast.service';
import { APP_CONFIG, AppConfig } from '@core/config/app-config';
import { isNativeApp } from '@core/platform/native';

/** Root providers, parameterized by the deployment config loaded before bootstrap. */
export function createAppConfig(config: AppConfig): ApplicationConfig {
  return {
    providers: [
      { provide: APP_CONFIG, useValue: config },
      provideZoneChangeDetection({ eventCoalescing: true }),
      provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
      provideHttpClient(withInterceptors([authInterceptor])),
      ToastService,
      // offline shell + update prompts for the browser / installed PWA (native shells bundle their files)
      provideServiceWorker('ngsw-worker.js', {
        enabled: !isDevMode() && !isNativeApp(),
        registrationStrategy: 'registerWhenStable:30000'
      })
    ]
  };
}
