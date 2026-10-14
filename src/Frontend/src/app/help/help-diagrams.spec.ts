// Guards the theme of the help diagrams in the repository's `docs/assets/`: design system colours
// only, a <title>, and a viewBox without a fixed width (design decision 6).
import { designSystemColours, diagramThemeViolations } from '../../testing/diagram-theme';
import { listHelpDiagrams, readRepoDoc } from '../../testing/repo-docs';

/** Every violation of every help diagram in `diagrams`, prefixed with the diagram's path. */
function violationsOf(diagrams: readonly string[]): string[] {
  const allowed = designSystemColours();
  return diagrams.flatMap((path) =>
    diagramThemeViolations(readRepoDoc(path), allowed).map((violation) => `${path} ${violation}`),
  );
}

// A diagram that breaks every rule: no title, a fixed width, a named colour, an off-palette hex
// value in a style attribute and an rgb() flood colour.
const WRONG_SAMPLE = `<svg xmlns="http://www.w3.org/2000/svg" width="1200" viewBox="0 0 1200 760">
<rect width="10" height="10" fill="red"/>
<rect width="10" height="10" style="stroke:#123456"/>
<feFlood flood-color="rgb(0, 192, 255)"/>
</svg>`;

// A diagram that follows every rule, with a three-digit hex value and non-colour paint.
const THEMED_SAMPLE = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10">
<title>Sample</title>
<defs><style>.a{fill:#00C0FF}</style><pattern id="p"/></defs>
<rect width="10" height="10" fill="#011627" stroke="none"/>
<rect width="10" height="10" fill="url(#p)"/>
</svg>`;

describe('help diagrams', () => {
  it('takes the allow-list from the design tokens', () => {
    const allowed = designSystemColours();
    for (const base of ['#011627', '#00c0ff', '#2f87ff', '#b2c2d4', '#7a8694', '#ff2146']) {
      expect(allowed.has(base)).toBe(true);
    }
    expect(allowed.has('#163953')).toBe(true); // surface-4
    expect(allowed.has('#5ecb7b')).toBe(true); // success-500
    expect(allowed.has('#000000')).toBe(false);
  });

  it('passes on an empty set of diagrams', () => {
    expect(violationsOf([])).toEqual([]);
  });

  it('accepts a sample that follows the theme', () => {
    expect(diagramThemeViolations(THEMED_SAMPLE, designSystemColours())).toEqual([]);
  });

  it('reports every broken rule of a deliberately wrong sample', () => {
    const violations = diagramThemeViolations(WRONG_SAMPLE, designSystemColours());
    expect(violations).toEqual(
      expect.arrayContaining([
        'has no <title> as the first child of <svg>',
        'has a fixed width (1200)',
        'uses fill red, which is not a design system colour',
        'uses stroke #123456, which is not a design system colour',
        'uses flood-color rgb(0, 192, 255), which is not a design system colour',
      ]),
    );
    expect(violations).toHaveLength(5);
  });

  it('reports a diagram that does not parse', () => {
    const broken = THEMED_SAMPLE.replace('</svg>', '');
    expect(diagramThemeViolations(broken, designSystemColours())).toContain(
      'does not parse as XML',
    );
  });

  it('finds only design system colours, a title and a scalable viewBox in every help diagram', () => {
    expect(violationsOf(listHelpDiagrams())).toEqual([]);
  });
});
