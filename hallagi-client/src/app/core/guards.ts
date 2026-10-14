import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, Observable, of } from 'rxjs';
import { AuthService } from './auth.service';
import { ToastService } from './toast.service';

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
  return ensureLoaded(auth);
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
