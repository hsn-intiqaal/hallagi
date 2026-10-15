import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { TOKEN_KEY } from './auth.interceptor';
import { Me, Profile, UserRole } from './models';
import { ThemeService } from './theme.service';

const BRANCH_KEY = 'hallagi.branch';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private api = inject(ApiService);
  private router = inject(Router);
  private theme = inject(ThemeService);

  readonly token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  readonly me = signal<Me | null>(null);
  readonly isLoggedIn = computed(() => !!this.token());
  readonly role = computed<UserRole | null>(() => this.me()?.role ?? null);
  readonly profile = computed<Profile | null>(() => this.me()?.profile ?? null);

  constructor() {
    // Restore session details from token on boot
    if (this.token()) {
      this.api.get<Me>('/auth/me').subscribe({
        next: m => { this.me.set(m); this.theme.apply(m.organization); },
        error: () => this.logout(false)
      });
    }
  }

  login(email: string, password: string): Observable<any> {
    return this.api.post('/auth/login', { email, password }).pipe(
      tap((res: any) => {
        localStorage.setItem(TOKEN_KEY, res.token);
        this.token.set(res.token);
        this.loadMe().subscribe();
      })
    );
  }

  loadMe(): Observable<Me> {
    return this.api.get<Me>('/auth/me').pipe(tap(m => { this.me.set(m); this.theme.apply(m.organization); }));
  }

  refreshMe(): void {
    if (this.token()) this.loadMe().subscribe();
  }

  /** Where each role should land after login. */
  landingRoute(): string {
    return this.role() === 'SuperAdmin' ? '/platform' : this.role() === 'Barber' ? '/me' : '/dashboard';
  }

  can(action: string): boolean {
    return this.me()?.permissions?.[action] ?? false;
  }

  isRole(role: UserRole): boolean {
    return this.role() === role;
  }

  logout(navigate = true): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(BRANCH_KEY);
    this.token.set(null);
    this.me.set(null);
    this.theme.reset();
    if (navigate) this.router.navigate(['/login']);
  }
}

export const branchStorage = {
  get(): string | null { return localStorage.getItem(BRANCH_KEY); },
  set(id: string): void { localStorage.setItem(BRANCH_KEY, id); },
  clear(): void { localStorage.removeItem(BRANCH_KEY); }
};
