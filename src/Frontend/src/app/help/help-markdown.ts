import { Marked, Renderer, Tokens } from 'marked';
import { findHelpTopic } from './help-topics';

/** Where the build publishes the repository's `docs/` folder. */
export const HELP_DOCS_PATH = '/docs/';

/** Where a document in `docs/` that is not a help topic is read: the repository on GitHub. */
export const GITHUB_DOCS_URL = 'https://github.com/hexmasternl/battleship/blob/main/docs/';

/** The class that marks a link to another help topic; `HelpDocument` routes its clicks. */
export const HELP_LINK_CLASS = 'help-link';

/** The class of the scroll container the renderer puts around every image. */
export const HELP_FIGURE_CLASS = 'help-prose__figure';

// Relative paths are resolved against a placeholder origin so `./`, `../` and fragments behave
// exactly as they do on GitHub, where the documents also render.
const LOCAL_DOCS_BASE = `https://help.invalid${HELP_DOCS_PATH}`;

const SCHEME = /^[a-z][a-z\d+.-]*:/i;
const HTTP_SCHEME = /^https?:/i;
const TOPIC_DOCUMENT = new RegExp(`^${HELP_DOCS_PATH}([a-z\\d-]+)\\.md$`);

/** A link destination after rewriting, with what kind of link it became. */
export interface HelpLink {
  readonly href: string;
  readonly kind: 'topic' | 'external' | 'other';
}

/**
 * Rewrites a link destination from a help document:
 * - a relative link to a topic document (`dapr.md#sidecars`) becomes its route (`/help/dapr#sidecars`)
 * - another relative link (`../README.md`) points at the file on GitHub
 * - an absolute `http(s)` link is external
 * - anything else (`#fragment`, `mailto:`, a root-relative path) is left as it is
 */
export function resolveHelpLink(href: string): HelpLink {
  if (HTTP_SCHEME.test(href)) return { href, kind: 'external' };
  if (SCHEME.test(href) || href.startsWith('#') || href.startsWith('/')) {
    return { href, kind: 'other' };
  }
  const local = new URL(href, LOCAL_DOCS_BASE);
  const slug = TOPIC_DOCUMENT.exec(local.pathname)?.[1];
  if (slug !== undefined && findHelpTopic(slug) !== undefined) {
    return { href: `/help/${slug}${local.hash}`, kind: 'topic' };
  }
  return { href: new URL(href, GITHUB_DOCS_URL).href, kind: 'external' };
}

/** Rewrites a relative image source (`assets/x.svg`) to its published path (`/docs/assets/x.svg`). */
export function resolveHelpImage(src: string): string {
  if (SCHEME.test(src) || src.startsWith('/')) return src;
  const local = new URL(src, LOCAL_DOCS_BASE);
  return `${local.pathname}${local.search}${local.hash}`;
}

/**
 * The fragment id of each heading, from its text, in the way GitHub builds them: lower case,
 * punctuation removed, spaces as hyphens, and `-1`, `-2` appended to repeats.
 */
export function helpHeadingIds(texts: readonly string[]): string[] {
  const seen = new Map<string, number>();
  return texts.map((text) => {
    const base = text
      .trim()
      .toLowerCase()
      .replace(/[^\p{L}\p{N}\s_-]/gu, '')
      .replace(/\s/g, '-');
    const count = seen.get(base) ?? 0;
    seen.set(base, count + 1);
    return count === 0 ? base : `${base}-${count}`;
  });
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function titleAttribute(title: string | null | undefined): string {
  return title ? ` title="${escapeHtml(title)}"` : '';
}

const marked = new Marked({
  gfm: true,
  renderer: {
    link(this: Renderer, token: Tokens.Link): string {
      const text = this.parser.parseInline(token.tokens);
      const link = resolveHelpLink(token.href);
      const href = ` href="${escapeHtml(link.href)}"`;
      const title = titleAttribute(token.title);
      switch (link.kind) {
        case 'topic':
          return `<a class="${HELP_LINK_CLASS}"${href}${title}>${text}</a>`;
        case 'external':
          return `<a${href}${title} target="_blank" rel="noopener">${text}</a>`;
        default:
          return `<a${href}${title}>${text}</a>`;
      }
    },
    // An image sits in its own scroll container: on a phone a wide diagram keeps a legible size
    // and scrolls sideways inside it, so it takes keyboard focus to be scrollable without a mouse.
    image(token: Tokens.Image): string {
      const src = escapeHtml(resolveHelpImage(token.href));
      const img = `<img src="${src}" alt="${escapeHtml(token.text)}"${titleAttribute(token.title)}>`;
      return `<span class="${HELP_FIGURE_CLASS}" tabindex="0">${img}</span>`;
    },
    // A code block scrolls on its own, so it takes keyboard focus to be scrollable without a mouse.
    code(this: Renderer, token: Tokens.Code): string {
      return Renderer.prototype.code.call(this, token).replace(/^<pre>/, '<pre tabindex="0">');
    },
    // A table sits in its own scroll container, so a wide table never widens the page.
    table(this: Renderer, token: Tokens.Table): string {
      const table = Renderer.prototype.table.call(this, token);
      return `<div class="help-prose__table" tabindex="0">${table}</div>\n`;
    },
  },
});

/**
 * Renders a help document to HTML: GitHub flavoured markdown with links and images rewritten for
 * the app. The result is not trusted: it is bound through `[innerHTML]`, so Angular's sanitiser
 * removes scripts, event handlers and any attribute it does not allow.
 */
export function renderHelpMarkdown(markdown: string): string {
  return marked.parse(markdown, { async: false });
}
