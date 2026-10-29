import { addDays, daysBetween, endOfMonth, previousMonth, startOfMonth, todayIn } from './local-date';

describe('local-date', () => {
  it('uses the salon calendar, not UTC, right after local midnight', () => {
    // 2026-10-27 21:30 UTC is already 2026-10-28 00:30 in Riyadh
    const instant = new Date('2026-10-27T21:30:00Z');
    expect(todayIn('Asia/Riyadh', instant)).toBe('2026-10-28');
    expect(todayIn('UTC', instant)).toBe('2026-10-27');
  });

  it('does calendar arithmetic across month and leap-year boundaries', () => {
    expect(addDays('2026-10-31', 1)).toBe('2026-11-01');
    expect(addDays('2028-03-01', -1)).toBe('2028-02-29');
    expect(startOfMonth('2026-10-28')).toBe('2026-10-01');
    expect(endOfMonth('2026-02-10')).toBe('2026-02-28');
    expect(previousMonth('2026-01-15')).toEqual({ from: '2025-12-01', to: '2025-12-31' });
  });

  it('counts both ends of a range', () => {
    expect(daysBetween('2026-10-01', '2026-10-01')).toBe(1);
    expect(daysBetween('2026-10-01', '2026-10-31')).toBe(31);
  });
});
