import { Pipe, PipeTransform } from '@angular/core';

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
  transform(value: string | Date | null | undefined): string {
    if (!value) return '—';
    const d = typeof value === 'string' ? new Date(value) : value;
    if (Number.isNaN(d.getTime())) return '—';
    return d.toLocaleDateString('ar-SA-u-nu-latn-ca-gregory', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
  }
}

/** Date short: "13 أكتوبر 2026" */
@Pipe({ name: 'dateShort', standalone: true })
export class DateShortPipe implements PipeTransform {
  transform(value: string | Date | null | undefined): string {
    if (!value) return '—';
    const d = typeof value === 'string' ? new Date(value) : value;
    if (Number.isNaN(d.getTime())) return '—';
    return d.toLocaleDateString('ar-SA-u-nu-latn-ca-gregory', { day: 'numeric', month: 'short', year: 'numeric' });
  }
}

/** Time: "09:41 ص" */
@Pipe({ name: 'timeAr', standalone: true })
export class TimeArPipe implements PipeTransform {
  transform(value: string | Date | null | undefined): string {
    if (!value) return '—';
    const d = typeof value === 'string' ? new Date(value) : value;
    if (Number.isNaN(d.getTime())) return '—';
    return d.toLocaleTimeString('ar-SA-u-nu-latn-ca-gregory', { hour: '2-digit', minute: '2-digit' });
  }
}

/** DateTime: "الأحد 13 أكتوبر 2026 - 09:41 ص" */
@Pipe({ name: 'dateTimeAr', standalone: true })
export class DateTimeArPipe implements PipeTransform {
  transform(value: string | Date | null | undefined): string {
    if (!value) return '—';
    const d = typeof value === 'string' ? new Date(value) : value;
    if (Number.isNaN(d.getTime())) return '—';
    return `${new DateArPipe().transform(d)} - ${new TimeArPipe().transform(d)}`;
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
