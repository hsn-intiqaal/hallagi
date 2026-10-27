import { Pipe, PipeTransform, inject } from '@angular/core';
import { AuthService } from '@core/auth/auth.service';

const LOCALE = 'ar-SA-u-nu-latn-ca-gregory';
const CALENDAR_DATE = /^\d{4}-\d{2}-\d{2}$/;

/**
 * Plain calendar dates ("2026-10-27") are shown as-is (formatted in UTC so no zone can shift them);
 * timestamps are shown on the salon's wall clock, whatever the viewer's device zone is.
 */
function parse(value: string | Date | null | undefined, salonZone: string): { date: Date; timeZone: string } | null {
  if (!value) return null;
  if (typeof value === 'string' && CALENDAR_DATE.test(value)) return { date: new Date(value + 'T12:00:00Z'), timeZone: 'UTC' };
  const date = typeof value === 'string' ? new Date(value.endsWith('Z') || /[+-]\d{2}:\d{2}$/.test(value) ? value : value + 'Z') : value;
  return Number.isNaN(date.getTime()) ? null : { date, timeZone: salonZone };
}

function zoneOf(auth: AuthService | null): string {
  return auth?.timeZone() ?? 'Asia/Riyadh';
}

/** Money: 1234.5 -> "1,234.50 ر.س" */
@Pipe({ name: 'sar', standalone: true })
export class SarPipe implements PipeTransform {
  transform(value: number | null | undefined, withSymbol = true): string {
    if (value === null || value === undefined || Number.isNaN(value)) return '—';
    const formatted = Math.abs(value).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const sign = value < 0 ? '-' : '';
    return withSymbol ? `${sign}${formatted} ر.س` : `${sign}${formatted}`;
  }
}

/** Date: "الأحد 13 أكتوبر 2026" */
@Pipe({ name: 'dateAr', standalone: true })
export class DateArPipe implements PipeTransform {
  private auth = inject(AuthService, { optional: true });
  transform(value: string | Date | null | undefined): string {
    const p = parse(value, zoneOf(this.auth));
    return p ? p.date.toLocaleDateString(LOCALE, { timeZone: p.timeZone, weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }) : '—';
  }
}

/** Date short: "13 أكتوبر 2026" */
@Pipe({ name: 'dateShort', standalone: true })
export class DateShortPipe implements PipeTransform {
  private auth = inject(AuthService, { optional: true });
  transform(value: string | Date | null | undefined): string {
    const p = parse(value, zoneOf(this.auth));
    return p ? p.date.toLocaleDateString(LOCALE, { timeZone: p.timeZone, day: 'numeric', month: 'short', year: 'numeric' }) : '—';
  }
}

/** Time: "09:41 ص" */
@Pipe({ name: 'timeAr', standalone: true })
export class TimeArPipe implements PipeTransform {
  private auth = inject(AuthService, { optional: true });
  transform(value: string | Date | null | undefined): string {
    const p = parse(value, zoneOf(this.auth));
    return p ? p.date.toLocaleTimeString(LOCALE, { timeZone: p.timeZone, hour: '2-digit', minute: '2-digit' }) : '—';
  }
}

/** DateTime: "الأحد 13 أكتوبر 2026 - 09:41 ص" */
@Pipe({ name: 'dateTimeAr', standalone: true })
export class DateTimeArPipe implements PipeTransform {
  private auth = inject(AuthService, { optional: true });
  transform(value: string | Date | null | undefined): string {
    const p = parse(value, zoneOf(this.auth));
    if (!p) return '—';
    const date = p.date.toLocaleDateString(LOCALE, { timeZone: p.timeZone, weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
    const time = p.date.toLocaleTimeString(LOCALE, { timeZone: p.timeZone, hour: '2-digit', minute: '2-digit' });
    return `${date} - ${time}`;
  }
}

/** Percentage: 0.3 -> "30%" */
@Pipe({ name: 'pct', standalone: true })
export class PctPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return `${Math.round(value * 100)}%`;
  }
}
