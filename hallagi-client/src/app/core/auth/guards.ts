import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, Observable, of } from 'rxjs';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/state/toast.service';

/** Waits until the authenticated profile (role + permissions) is loaded. */
function ensureLoaded(auth: AuthService): Observable<boolean> {
  if (auth.me()) return of(true);
  if (!auth.isLoggedIn()) return of(false);
  return auth.loadMe().pipe(
    map(() => true),
    catchError(() => of(false))
  );
}

function deny(auth: AuthService, router: Router, toast: ToastService, navigate = true): false {
  toast.error('ليس لديك صلاحية للوصول إلى هذه الصفحة');
  if (navigate) router.navigate([auth.landingRoute()]);
  return false;
}

export const authGuard: CanActivateFn = (): Observable<boolean> | boolean => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isLoggedIn()) {
    router.navigate(['/login']);
    return false;
  }
  // the platform owner uses the admin console, not tenant screens
  if (auth.role() === 'SuperAdmin') {
    router.navigate(['/platform']);
    return false;
  }
  return ensureLoaded(auth);
};

/** Only the platform owner may open the admin console. */
export const superAdminGuard: CanActivateFn = (): Observable<boolean> | boolean => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isLoggedIn()) {
    router.navigate(['/login']);
    return false;
  }
  return ensureLoaded(auth).pipe(map(ready => {
    if (!ready) { router.navigate(['/login']); return false; }
    if (auth.role() === 'SuperAdmin') return true;
    router.navigate([auth.landingRoute()]);
    return false;
  }));
};

export const roleGuard = (allowedRoles: string[]): CanActivateFn => (): Observable<boolean> | boolean => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const toast = inject(ToastService);
  if (!auth.isLoggedIn()) {
    router.navigate(['/login']);
    return false;
  }
  return ensureLoaded(auth).pipe(map(ready => {
    if (!ready) { router.navigate(['/login']); return false; }
    if (allowedRoles.includes(auth.role() ?? '')) return true;
    return deny(auth, router, toast);
  }));
};

export const permissionGuard = (action: string): CanActivateFn => (): Observable<boolean> | boolean => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const toast = inject(ToastService);
  if (!auth.isLoggedIn()) {
    router.navigate(['/login']);
    return false;
  }
  return ensureLoaded(auth).pipe(map(ready => {
    if (!ready) { router.navigate(['/login']); return false; }
    if (auth.can(action)) return true;
    return deny(auth, router, toast);
  }));
};
