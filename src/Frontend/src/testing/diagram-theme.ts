// Checks a help diagram against the design system's theme rules (design decision 6 of the
// issue-49 help section change). Only unit tests import this file.
import { readFileSync } from 'node:fs';

/** The design tokens file, relative to the directory `ng test` runs from (src/Frontend). */
const TOKENS_FILE = 'src/styles/_tokens.scss';

/**
 * Every literal colour the design system defines: the base colours, their tints and shades, the
 * surfaces and the derived status hues, all as lower-case six-digit hex values.
 */
export function designSystemColours(): ReadonlySet<string> {
  const tokens = readFileSync(TOKENS_FILE, 'utf8');
  const colours = [...tokens.matchAll(/--bs-color-[\w-]+:\s*(#[0-9a-f]{6})\b/gi)].map((match) =>
    match[1].toLowerCase(),
  );
  return new Set(colours);
}

// Paint values that are not colours: no paint, a gradient or pattern reference, or inheritance.
const NON_COLOUR_PAINT = /^(none|inherit|url\(#[\w-]+\))$/;

// The properties that carry a colour, as an attribute (`fill="#011627"`) or a CSS declaration
// (`fill:#011627` in a `style` attribute or a `<style>` element).
const COLOUR_ATTRIBUTE =
  /\s(fill|stroke|flood-color|stop-color|lighting-color|color)\s*=\s*"([^"]*)"/g;
const COLOUR_DECLARATION =
  /(?<![\w-])(fill|stroke|flood-color|stop-color|lighting-color|color)\s*:\s*([^;}"]+)/g;

/** A six-digit lower-case hex value for a hex colour, or `null` when the value is not hex. */
function normaliseHex(value: string): string | null {
  const hex = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(value)?.[1];
  if (hex === undefined) return null;
  const full = hex.length === 3 ? [...hex].map((digit) => digit + digit).join('') : hex;
  return `#${full.toLowerCase()}`;
}

/**
 * The theme rules a diagram breaks, one message per finding; empty when it follows them all:
 * - it parses as XML
 * - its root `<svg>` starts with a non-empty `<title>`
 * - it has a `viewBox` and no fixed width (no `width`, or `width="100%"`)
 * - every fill, stroke, flood, stop and lighting colour is a design system colour
 */
export function diagramThemeViolations(svg: string, allowed: ReadonlySet<string>): string[] {
  const violations: string[] = [];

  const document = new DOMParser().parseFromString(svg, 'image/svg+xml');
  if (document.getElementsByTagName('parsererror').length > 0) {
    violations.push('does not parse as XML');
  }

  if (!/^\s*(<\?xml[^>]*>\s*)?<svg\b[^>]*>\s*<title>\s*[^<\s][^<]*<\/title>/.test(svg)) {
    violations.push('has no <title> as the first child of <svg>');
  }

  const root = /<svg\b[^>]*>/.exec(svg)?.[0] ?? '';
  if (!/\sviewBox="[^"]+"/.test(root)) violations.push('has no viewBox');
  const width = /\swidth="([^"]*)"/.exec(root)?.[1];
  if (width !== undefined && width !== '100%') violations.push(`has a fixed width (${width})`);

  const values = [...svg.matchAll(COLOUR_ATTRIBUTE), ...svg.matchAll(COLOUR_DECLARATION)].map(
    (match) => [match[1], match[2].trim()] as const,
  );
  for (const [property, value] of values) {
    if (NON_COLOUR_PAINT.test(value.toLowerCase())) continue;
    const hex = normaliseHex(value);
    if (hex === null || !allowed.has(hex)) {
      violations.push(`uses ${property} ${value}, which is not a design system colour`);
    }
  }

  return violations;
}
