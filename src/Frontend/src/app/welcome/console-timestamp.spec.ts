import { formatConsoleTimestamp } from './console-timestamp';

describe('formatConsoleTimestamp', () => {
  it('formats a morning time with two-digit fields and the AM marker', () => {
    const date = new Date(2026, 9, 13, 10, 18, 37);
    expect(formatConsoleTimestamp(date)).toBe('[ 10:18:37 AM ] - ');
  });

  it('formats an afternoon time on a 12-hour clock with the PM marker', () => {
    const date = new Date(2026, 9, 13, 15, 5, 2);
    expect(formatConsoleTimestamp(date)).toBe('[ 03:05:02 PM ] - ');
  });

  it('formats midnight as 12 AM', () => {
    const date = new Date(2026, 9, 13, 0, 0, 0);
    expect(formatConsoleTimestamp(date)).toBe('[ 12:00:00 AM ] - ');
  });
});
