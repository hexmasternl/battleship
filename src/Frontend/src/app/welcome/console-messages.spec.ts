import { CONSOLE_MESSAGES } from './console-messages';

describe('CONSOLE_MESSAGES', () => {
  it('holds at least 100 messages', () => {
    expect(CONSOLE_MESSAGES.length).toBeGreaterThanOrEqual(100);
  });

  it('holds no duplicates after trimming', () => {
    const trimmed = CONSOLE_MESSAGES.map((message) => message.trim());
    expect(new Set(trimmed).size).toBe(trimmed.length);
  });

  it('holds no empty messages', () => {
    expect(CONSOLE_MESSAGES.every((message) => message.trim().length > 0)).toBe(true);
  });

  it('keeps every message short enough to type within the minimum gap', () => {
    // 30ms per character: 120 characters type in 3.6s, inside the 5s minimum gap.
    expect(CONSOLE_MESSAGES.every((message) => message.length <= 120)).toBe(true);
  });
});
