import { InjectionToken } from '@angular/core';
import { environment } from '@env/environment';

/**
 * Deployment-time configuration, read from `/config.json` before the app boots.
 * The same compiled build can therefore serve any client, domain or mobile shell:
 * only this file changes (written by the web container's entrypoint, or by
 * `npm run config` before packaging the mobile app).
 */
export interface BrandConfig {
  /** Product name shown in the UI, page title, receipts fallback and the app launcher. */
  name: string;
  shortName: string;
  tagline: string;
  /** Default theme before an organization's own colors load. */
  primaryColor: string;
  secondaryColor: string;
  /** Optional logo (URL or data URL) replacing the built-in mark on the sign-in screen. */
  logoUrl?: string | null;
  supportEmail?: string | null;
  website?: string | null;
}

export interface AppConfig {
  /** API base, e.g. "/api" behind the same origin or "https://api.example.com/api" for mobile. */
  apiUrl: string;
  /** Default IANA time zone until the organization's own zone is known. */
  defaultTimeZone: string;
  brand: BrandConfig;
}

export const DEFAULT_CONFIG: AppConfig = {
  apiUrl: environment.apiUrl,
  defaultTimeZone: 'Asia/Riyadh',
  brand: {
    name: 'حلاقي',
    shortName: 'حلاقي',
    tagline: 'محلك في يدك',
    primaryColor: '#2563EB',
    secondaryColor: '#173653',
    logoUrl: null,
    supportEmail: null,
    website: null
  }
};

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');

/** Fetches `/config.json` (never cached) and merges it over the built-in defaults. */
export async function loadAppConfig(): Promise<AppConfig> {
  try {
    const res = await fetch('config.json', { cache: 'no-store' });
    if (!res.ok) return DEFAULT_CONFIG;
    const remote = (await res.json()) as Partial<AppConfig>;
    return {
      ...DEFAULT_CONFIG,
      ...remote,
      apiUrl: (remote.apiUrl || DEFAULT_CONFIG.apiUrl).replace(/\/+$/, ''),
      brand: { ...DEFAULT_CONFIG.brand, ...(remote.brand ?? {}) }
    };
  } catch {
    // offline first launch or no file: the built-in defaults still give a working app
    return DEFAULT_CONFIG;
  }
}
