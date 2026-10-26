const ACCESS_KEY = 'hallagi.token';
const REFRESH_KEY = 'hallagi.refresh';

/** Single place that knows where session tokens live. */
export const tokenStorage = {
  access(): string | null { return localStorage.getItem(ACCESS_KEY); },
  refresh(): string | null { return localStorage.getItem(REFRESH_KEY); },
  save(access: string, refresh?: string | null): void {
    localStorage.setItem(ACCESS_KEY, access);
    if (refresh) localStorage.setItem(REFRESH_KEY, refresh);
  },
  clear(): void {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
  }
};
