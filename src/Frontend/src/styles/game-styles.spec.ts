// Guards the global board recipe in `_game.scss`, which component tests never load.
import { compile } from 'sass';

/** The declarations of the first rule whose selector list contains exactly `selector`. */
function declarationsOf(css: string, selector: string): string {
  for (const match of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    const selectors = match[1].split(',').map((s) => s.trim());
    if (selectors.includes(selector) && selectors.length === 1) return match[2];
  }
  throw new Error(`No rule for ${selector}`);
}

describe('game styles', () => {
  // `ng test` runs from the project root (src/Frontend).
  const css = compile('src/styles/_game.scss').css;

  it('keeps the board inside its container: its padding and border count in its width', () => {
    const board = declarationsOf(css, '.bs-board').replace(/\s+/g, ' ');
    expect(board).toContain('box-sizing: border-box');
    expect(board).toContain('width: min(100%, 520px)');
  });

  it('gives the board and its ship overlay the same tracks and padding', () => {
    const both = css.match(/\.bs-board,\s*\.bs-board__ships\s*\{([^}]*)\}/)?.[1] ?? '';
    expect(both).toContain('grid-template-columns');
    expect(both).toContain('padding');
    const overlay = declarationsOf(css, '.bs-board__ships').replace(/\s+/g, ' ');
    expect(overlay).toContain('position: absolute');
    expect(overlay).toContain('inset: 0');
  });
});
