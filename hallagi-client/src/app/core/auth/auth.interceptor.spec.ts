import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { APP_CONFIG, DEFAULT_CONFIG } from '@core/config/app-config';
import { authInterceptor } from './auth.interceptor';
import { tokenStorage } from './token-storage';

describe('authInterceptor', () => {
  const api = 'https://api.test/api';
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: APP_CONFIG, useValue: { ...DEFAULT_CONFIG, apiUrl: api } },
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting()
      ]
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    tokenStorage.save('access-1', 'refresh-1');
  });

  afterEach(() => {
    backend.verify();
    tokenStorage.clear();
  });

  it('sends the bearer token', () => {
    http.get(`${api}/sales`).subscribe();
    expect(backend.expectOne(`${api}/sales`).request.headers.get('Authorization')).toBe('Bearer access-1');
  });

  it('never attaches a token to anonymous endpoints', () => {
    http.get(`${api}/public/booking/elite`).subscribe();
    expect(backend.expectOne(`${api}/public/booking/elite`).request.headers.has('Authorization')).toBeFalse();
  });

  it('renews an expired session once and replays the request', () => {
    let body: unknown;
    http.get(`${api}/sales`).subscribe(b => (body = b));
    backend.expectOne(`${api}/sales`).flush(null, { status: 401, statusText: 'Unauthorized' });

    const refresh = backend.expectOne(`${api}/auth/refresh`);
    expect(refresh.request.body).toEqual({ refreshToken: 'refresh-1' });
    refresh.flush({ token: 'access-2', refreshToken: 'refresh-2' });

    const retry = backend.expectOne(`${api}/sales`);
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
    retry.flush([1]);
    expect(body).toEqual([1]);
    expect(tokenStorage.refresh()).toBe('refresh-2');
  });

  it('signs out when the refresh token is rejected', () => {
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    http.get(`${api}/sales`).subscribe({ error: () => undefined });
    backend.expectOne(`${api}/sales`).flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne(`${api}/auth/refresh`).flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(navigate).toHaveBeenCalledWith(['/login']);
    expect(tokenStorage.access()).toBeNull();
  });
});
