import {
  GITHUB_DOCS_URL,
  helpHeadingIds,
  renderHelpMarkdown,
  resolveHelpImage,
  resolveHelpLink,
} from './help-markdown';

/** Renders markdown into a detached element, so assertions read the DOM rather than strings. */
function render(markdown: string): HTMLElement {
  const host = document.createElement('div');
  host.innerHTML = renderHelpMarkdown(markdown);
  return host;
}

describe('resolveHelpLink', () => {
  it.each([
    ['dapr.md', '/help/dapr'],
    ['./dapr.md', '/help/dapr'],
    ['resource-commands.md#join-game', '/help/resource-commands#join-game'],
  ])('routes the topic document %s to %s', (href, route) => {
    expect(resolveHelpLink(href)).toEqual({ href: route, kind: 'topic' });
  });

  it.each([
    ['design-system.md', `${GITHUB_DOCS_URL}design-system.md`],
    ['../README.md', 'https://github.com/hexmasternl/battleship/blob/main/README.md'],
    ['not-a-topic.md', `${GITHUB_DOCS_URL}not-a-topic.md`],
  ])('points the other relative link %s at GitHub', (href, url) => {
    expect(resolveHelpLink(href)).toEqual({ href: url, kind: 'external' });
  });

  it.each(['https://aspire.dev/', 'http://localhost:15000/'])('keeps %s external', (href) => {
    expect(resolveHelpLink(href)).toEqual({ href, kind: 'external' });
  });

  it.each(['#prerequisites', 'mailto:someone@example.com', '/help'])('leaves %s alone', (href) => {
    expect(resolveHelpLink(href)).toEqual({ href, kind: 'other' });
  });
});

describe('resolveHelpImage', () => {
  it.each([
    ['assets/help-resource-graph.svg', '/docs/assets/help-resource-graph.svg'],
    ['./assets/help-dapr-sidecars.svg', '/docs/assets/help-dapr-sidecars.svg'],
  ])('publishes the relative image %s at %s', (src, published) => {
    expect(resolveHelpImage(src)).toBe(published);
  });

  it.each(['https://example.com/x.svg', '/docs/assets/x.svg'])('keeps %s', (src) => {
    expect(resolveHelpImage(src)).toBe(src);
  });
});

describe('helpHeadingIds', () => {
  it('builds GitHub style fragment ids', () => {
    expect(helpHeadingIds(['What is a resource?', 'Dapr sidecars', '  The `devbot`  '])).toEqual([
      'what-is-a-resource',
      'dapr-sidecars',
      'the-devbot',
    ]);
  });

  it('numbers repeated headings', () => {
    expect(helpHeadingIds(['Example', 'Example', 'Example'])).toEqual([
      'example',
      'example-1',
      'example-2',
    ]);
  });
});

describe('renderHelpMarkdown', () => {
  it('renders a link to another topic as an in-app help link', () => {
    const link = render('See [Dapr](dapr.md#sidecars).').querySelector('a');
    expect(link?.getAttribute('href')).toBe('/help/dapr#sidecars');
    expect(link?.classList.contains('help-link')).toBe(true);
    expect(link?.hasAttribute('target')).toBe(false);
    expect(link?.textContent).toBe('Dapr');
  });

  it('opens an external link in a new tab with rel="noopener"', () => {
    const link = render('Read [the Aspire docs](https://aspire.dev/).').querySelector('a');
    expect(link?.getAttribute('href')).toBe('https://aspire.dev/');
    expect(link?.getAttribute('target')).toBe('_blank');
    expect(link?.getAttribute('rel')).toBe('noopener');
    expect(link?.classList.contains('help-link')).toBe(false);
  });

  it('points a link to a document that is not a help topic at GitHub', () => {
    const link = render('[Design system](design-system.md)').querySelector('a');
    expect(link?.getAttribute('href')).toBe(`${GITHUB_DOCS_URL}design-system.md`);
    expect(link?.getAttribute('target')).toBe('_blank');
    expect(link?.getAttribute('rel')).toBe('noopener');
  });

  it('serves a relative image from the published docs assets with its alt text', () => {
    const image = render('![The resources](assets/help-resource-graph.svg "Graph")').querySelector(
      'img',
    );
    expect(image?.getAttribute('src')).toBe('/docs/assets/help-resource-graph.svg');
    expect(image?.getAttribute('alt')).toBe('The resources');
    expect(image?.getAttribute('title')).toBe('Graph');
  });

  it('escapes quotes in link destinations and alt text', () => {
    const host = render('![a "quoted" alt](assets/x.svg)\n\n[x](https://example.com/?q="y")');
    expect(host.querySelector('img')?.getAttribute('alt')).toBe('a "quoted" alt');
    expect(host.querySelectorAll('a').length).toBe(1);
  });

  it('renders headings, paragraphs and lists', () => {
    const host = render('# Title\n\nSome text.\n\n## Part\n\n- one\n- two\n\n1. first\n2. second');
    expect(host.querySelector('h1')?.textContent).toBe('Title');
    expect(host.querySelector('h2')?.textContent).toBe('Part');
    expect(host.querySelector('p')?.textContent).toBe('Some text.');
    expect(host.querySelectorAll('ul li').length).toBe(2);
    expect(host.querySelectorAll('ol li').length).toBe(2);
  });

  it('renders a GFM table inside a focusable scroll wrapper', () => {
    const host = render('| Kind | Name |\n|---|---|\n| Container | messaging |');
    const wrapper = host.querySelector('.help-prose__table');
    expect(wrapper?.getAttribute('tabindex')).toBe('0');
    expect(wrapper?.querySelector('table th')?.textContent).toBe('Kind');
    expect(wrapper?.querySelector('table td')?.textContent).toBe('Container');
  });

  it('renders a fenced code block, escaped, in a focusable pre', () => {
    const host = render('```bash\naspire run --project <AppHost>\n```');
    const pre = host.querySelector('pre');
    expect(pre?.getAttribute('tabindex')).toBe('0');
    expect(pre?.querySelector('code.language-bash')?.textContent).toBe(
      'aspire run --project <AppHost>\n',
    );
    expect(pre?.querySelector('AppHost')).toBeNull();
  });

  it('renders inline code', () => {
    expect(render('Run `dapr init` once.').querySelector('code')?.textContent).toBe('dapr init');
  });

  it('renders a block quote', () => {
    const quote = render('> The AppHost fails fast.').querySelector('blockquote');
    expect(quote?.querySelector('p')?.textContent).toBe('The AppHost fails fast.');
  });
});
