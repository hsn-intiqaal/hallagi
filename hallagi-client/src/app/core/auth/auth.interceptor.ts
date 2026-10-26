import { HttpBackend, HttpClient, HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, finalize, map, Observable, shareReplay, switchMap, throwError } from 'rxjs';
import { environment } from '@env/environment';
import { tokenStorage } from './token-storage';

/** Endpoints that must never carry (or try to renew) a session. */
const ANONYMOUS = ['/auth/login', '/auth/refresh', '/auth/logout', '/public/'];

/** One refresh for every request that hit 401 at the same moment. */
let refreshInFlight$: Observable<string> | null = null;

/**
 * Adds the bearer token and transparently renews an expired session once using the
 * rotating refresh token. Deliberately does NOT inject AuthService:
 * AuthService -> ApiService -> HttpClient -> interceptor would be a cycle (NG0200).
 */
export const authInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> => {
  const router = inject(Router);
  // HttpBackend skips interceptors, so the refresh call cannot recurse into this function
  const rawHttp = new HttpClient(inject(HttpBackend));

  if (ANONYMOUS.some(path => req.url.includes(path))) return next(req);

  const withToken = (token: string | null) =>
    token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(withToken(tokenStorage.access())).pipe(
    catchError((e: unknown) => {
      if (!(e instanceof HttpErrorResponse) || e.status !== 401) return throwError(() => e);

      const refreshToken = tokenStorage.refresh();
      if (!refreshToken) return expire(router, e);

      refreshInFlight$ ??= rawHttp
        .post<{ token: string; refreshToken: string }>(`${environment.apiUrl}/auth/refresh`, { refreshToken })
        .pipe(
          map(res => { tokenStorage.save(res.token, res.refreshToken); return res.token; }),
          finalize(() => (refreshInFlight$ = null)),
          shareReplay(1)
        );

      return refreshInFlight$.pipe(
        switchMap(token => next(withToken(token))),
        catchError(() => expire(router, e))
      );
    })
  );
};

function expire(router: Router, error: HttpErrorResponse): Observable<never> {
  if (tokenStorage.access()) {
    tokenStorage.clear();
    router.navigate(['/login']);
  }
  return throwError(() => error);
}
