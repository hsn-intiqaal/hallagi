import { Injectable } from '@angular/core';

/** Minimal organization branding payload used for theming. */
export interface OrgTheme {
  primaryColor?: string | null;
  secondaryColor?: string | null;
}

/**
 * Applies the tenant organization's brand colors to the whole UI by remapping
 * the design-system CSS variables (buttons, topbar, active links, ...) at the
 * document root. Falls back to the default palette when no colors are set.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly root = document.documentElement;

  /** Apply the organization colors (invalid values keep the current theme). */
  apply(org: OrgTheme | null | undefined): void {
    if (!org) return;
    const primary = org.primaryColor;
    const secondary = org.secondaryColor;

    if (this.isHex(primary)) {
      this.root.style.setProperty('--blue', primary);
      this.root.style.setProperty('--blue-soft', this.tint(primary, 0.9));
      this.root.style.setProperty('--primary', primary);
      this.root.style.setProperty('--primary-dark', this.darken(primary, 0.18));
    }
    if (this.isHex(secondary)) {
      this.root.style.setProperty('--navy', secondary);
      this.root.style.setProperty('--navy-2', this.lighten(secondary, 0.14));
      this.root.style.setProperty('--navy-soft', this.tint(secondary, 0.93));
      this.root.style.setProperty('--secondary', secondary);
    }
  }

  /** Restore the default design-system palette (used on sign-out). */
  reset(): void {
    for (const prop of ['--blue', '--blue-soft', '--navy', '--navy-2', '--navy-soft', '--primary', '--primary-dark', '--secondary']) {
      this.root.style.removeProperty(prop);
    }
  }

  private isHex(value?: string | null): value is string {
    return !!value && /^#([0-9a-fA-F]{6})$/.test(value);
  }

  /** Mix a hex color with white (amount 0..1). */
  tint(hex: string, amount: number): string {
    const [r, g, b] = this.rgb(hex);
    const mix = (c: number) => Math.round(c + (255 - c) * amount);
    return this.hex(mix(r), mix(g), mix(b));
  }

  /** Darken a hex color by a factor (0..1). */
  darken(hex: string, factor: number): string {
    const [r, g, b] = this.rgb(hex);
    const k = 1 - factor;
    return this.hex(Math.round(r * k), Math.round(g * k), Math.round(b * k));
  }

  /** Lighten a hex color by mixing toward white. */
  lighten(hex: string, amount: number): string {
    return this.tint(hex, amount);
  }

  private rgb(hex: string): [number, number, number] {
    const h = hex.replace('#', '');
    return [
      parseInt(h.slice(0, 2), 16),
      parseInt(h.slice(2, 4), 16),
      parseInt(h.slice(4, 6), 16)
    ];
  }

  private hex(r: number, g: number, b: number): string {
    const p = (v: number) => Math.max(0, Math.min(255, v)).toString(16).padStart(2, '0');
    return `#${p(r)}${p(g)}${p(b)}`;
  }
}
