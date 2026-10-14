// Guards the global help document typography in `_help-prose.scss`, which component tests never load.
import { compile } from 'sass';

/** The declarations of every rule whose selector list contains `selector`, joined. */
function declarationsOf(css: string, selector: string): string {
  const found: string[] = [];
  for (const match of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    const selectors = match[1].split(',').map((s) => s.trim());
    if (selectors.includes(selector)) found.push(match[2]);
  }
  if (found.length === 0) throw new Error(`No rule for ${selector}`);
  return found.join(' ').replace(/\s+/g, ' ');
}

describe('help prose styles', () => {
  // `ng test` runs from the project root (src/Frontend).
  const css = compile('src/styles/_help-prose.scss').css;

  it('sets body text in Courier Prime and headings in Quantico', () => {
    expect(declarationsOf(css, '.help-prose')).toContain('font-family: var(--bs-font-body)');
    expect(declarationsOf(css, '.help-prose h1')).toContain('font-family: var(--bs-font-heading)');
  });

  it('lets code blocks and tables scroll on their own', () => {
    expect(declarationsOf(css, '.help-prose pre')).toContain('overflow-x: auto');
    expect(declarationsOf(css, '.help-prose .help-prose__table')).toContain('overflow-x: auto');
  });

  it('caps images at the content width', () => {
    expect(declarationsOf(css, '.help-prose img')).toContain('max-width: 100%');
  });

  it('takes every colour from a design system token', () => {
    expect(css).not.toMatch(/#[\da-f]{3,8}\b|rgba?\(|hsla?\(/i);
    for (const match of css.matchAll(
      /(?:^|[\s;{])(color|background(?:-color)?|border(?:-(?:top|right|bottom|left))?(?:-color)?|outline):([^;]+);/g,
    )) {
      const value = match[2].trim();
      if (/^(0|none|auto|transparent)$/.test(value)) continue;
      expect(value, `${match[1]}: ${value}`).toMatch(/var\(--bs-/);
    }
  });
});
