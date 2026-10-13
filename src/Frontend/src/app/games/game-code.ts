/** The number of characters in a game code. */
export const GAME_CODE_LENGTH = 8;

/** A game code as the Games service issues it: eight upper-case letters or digits. */
export const GAME_CODE_PATTERN = /^[A-Z0-9]{8}$/;

/**
 * What a typed or pasted game code becomes: upper case, only `A`-`Z` and `0`-`9`, at most eight
 * characters. Stripping comes before the limit, so a pasted `k7q2-m9xa` still yields `K7Q2M9XA`.
 */
export function normalizeGameCode(raw: string): string {
  return raw
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, '')
    .slice(0, GAME_CODE_LENGTH);
}

/** Whether `code` is a complete, well-formed game code. */
export function isGameCode(code: string): boolean {
  return GAME_CODE_PATTERN.test(code);
}
