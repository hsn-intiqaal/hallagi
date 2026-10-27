/**
 * Calendar dates as the salon experiences them. Dates travel to the API as plain `YYYY-MM-DD`
 * strings (the API treats them as the organization's local days). Never use
 * `toISOString().slice(0, 10)` — that is the UTC date, which is "yesterday" after local midnight.
 */
export type IsoDate = string;

const DEFAULT_ZONE = 'Asia/Riyadh';

/** Today's date in the given IANA time zone (defaults to the salon's zone). */
export function todayIn(timeZone: string = DEFAULT_ZONE, now: Date = new Date()): IsoDate {
  // en-CA formats as YYYY-MM-DD
  return new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit' }).format(now);
}

/** Pure calendar arithmetic on YYYY-MM-DD (no time zone involved). */
export function addDays(date: IsoDate, days: number): IsoDate {
  const [y, m, d] = date.split('-').map(Number);
  const t = new Date(Date.UTC(y, m - 1, d + days));
  return t.toISOString().slice(0, 10);
}

export function startOfMonth(date: IsoDate): IsoDate {
  return date.slice(0, 8) + '01';
}

export function endOfMonth(date: IsoDate): IsoDate {
  const [y, m] = date.split('-').map(Number);
  return new Date(Date.UTC(y, m, 0)).toISOString().slice(0, 10);
}

export function previousMonth(date: IsoDate): { from: IsoDate; to: IsoDate } {
  const lastOfPrev = addDays(startOfMonth(date), -1);
  return { from: startOfMonth(lastOfPrev), to: lastOfPrev };
}

export function daysBetween(from: IsoDate, to: IsoDate): number {
  const a = Date.parse(from + 'T00:00:00Z');
  const b = Date.parse(to + 'T00:00:00Z');
  return Math.round((b - a) / 86_400_000) + 1;
}
