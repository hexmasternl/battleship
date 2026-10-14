// Checks the content of the help documents in the repository's `docs/` folder: the rules every
// document follows, and the facts each topic must cover (help-section spec).
import { Lexer, Token, Tokens, marked } from 'marked';
import { listHelpDiagrams, readRepoDoc, repoDocExists } from '../../testing/repo-docs';
import { HELP_TOPICS, HelpTopic, findHelpTopic } from './help-topics';

/** A help document parsed once: its markdown, its tokens and every token inside them. */
interface ParsedDoc {
  readonly markdown: string;
  readonly tokens: Token[];
  readonly all: Token[];
}

function parse(topic: HelpTopic): ParsedDoc {
  const markdown = readRepoDoc(topic.file);
  const tokens = Lexer.lex(markdown, { gfm: true });
  const all: Token[] = [];
  marked.walkTokens(tokens, (token) => {
    all.push(token);
  });
  return { markdown, tokens, all };
}

const docs = new Map(HELP_TOPICS.map((topic) => [topic.slug, parse(topic)] as const));

function doc(slug: string): ParsedDoc {
  const parsed = docs.get(slug);
  if (parsed === undefined) throw new Error(`no help topic ${slug}`);
  return parsed;
}

function ofType<T extends Token>(parsed: ParsedDoc, type: T['type']): T[] {
  return parsed.all.filter((token): token is T => token.type === type);
}

const images = (parsed: ParsedDoc) => ofType<Tokens.Image>(parsed, 'image');
const links = (parsed: ParsedDoc) => ofType<Tokens.Link>(parsed, 'link');
const codeBlocks = (parsed: ParsedDoc) => ofType<Tokens.Code>(parsed, 'code').map((c) => c.text);
const tables = (parsed: ParsedDoc) => ofType<Tokens.Table>(parsed, 'table');

/** The image paths a document uses, without the `assets/` folder. */
const imageFiles = (parsed: ParsedDoc) => images(parsed).map((image) => image.href);

describe('help document content', () => {
  describe.each(HELP_TOPICS.map((topic) => [topic.slug, topic] as const))('%s', (slug, topic) => {
    it('has exactly one level-one heading, the topic title, as its first block', () => {
      const parsed = doc(slug);
      const h1 = parsed.tokens.filter(
        (token): token is Tokens.Heading => token.type === 'heading' && token.depth === 1,
      );
      expect(h1.map((heading) => heading.text)).toEqual([topic.title]);
      expect(parsed.tokens[0]).toBe(h1[0]);
      expect(/^\s*<h1[\s>]/im.test(parsed.markdown)).toBe(false);
    });

    it('gives every image alt text and a relative path to an existing file in docs/assets', () => {
      for (const image of images(doc(slug))) {
        expect(image.text.trim(), `alt text of ${image.href}`).not.toBe('');
        expect(image.href).toMatch(/^assets\/[\w.-]+$/);
        expect(repoDocExists(image.href), `${image.href} exists`).toBe(true);
      }
    });

    it('links to other documents in docs/ only as registry topics', () => {
      for (const link of links(doc(slug))) {
        const local = /^([^/:#?]+)\.md(#[\w-]*)?$/.exec(link.href);
        if (local === null) continue;
        expect(findHelpTopic(local[1]), `${link.href} is a help topic`).toBeDefined();
      }
    });

    it('never links to a help topic or a help image with an absolute URL', () => {
      const hrefs = [...links(doc(slug)), ...images(doc(slug))].map((token) => token.href);
      for (const href of hrefs) {
        expect(href).not.toMatch(/(^\/|:\/\/).*(docs\/|\/help\/)/);
      }
    });
  });

  it('uses every help diagram in at least one document', () => {
    const used = new Set([...docs.values()].flatMap(imageFiles));
    for (const diagram of listHelpDiagrams()) {
      expect(used.has(diagram), `${diagram} is used`).toBe(true);
    }
  });

  describe('prerequisites', () => {
    const parsed = doc('prerequisites');

    it('names each tool with an official install link', () => {
      const hrefs = links(parsed).map((link) => link.href);
      const tools: readonly [string, RegExp][] = [
        ['.NET 10 SDK', /^https:\/\/dotnet\.microsoft\.com\/download\/dotnet\/10\.0/],
        ['Docker', /^https:\/\/www\.docker\.com\//],
        ['Dapr CLI', /^https:\/\/docs\.dapr\.io\/getting-started\/install-dapr-cli\//],
        ['Node.js', /^https:\/\/nodejs\.org\//],
        ['Aspire CLI', /^https:\/\/aspire\.dev\/get-started\/install-cli\//],
      ];
      for (const [name, url] of tools) {
        expect(parsed.markdown, name).toContain(name);
        expect(
          hrefs.some((href) => url.test(href)),
          `${name} install link`,
        ).toBe(true);
      }
      expect(parsed.markdown).toContain('npm');
    });

    it('shows dapr init and the command that starts the AppHost in code blocks', () => {
      const code = codeBlocks(parsed).join('\n');
      expect(code).toContain('dapr init');
      expect(code).toMatch(/^aspire run$/m);
    });

    it('explains the fail-fast Dapr prerequisite check', () => {
      expect(parsed.markdown).toContain('DaprPrerequisites');
      expect(parsed.markdown).toContain('Dapr prerequisite missing');
      expect(parsed.markdown).toMatch(/fail-fast/i);
    });
  });

  describe('resources', () => {
    const parsed = doc('resources');

    it('explains what an Aspire resource is', () => {
      expect(parsed.markdown).toMatch(/a \*\*resource\*\* is/i);
    });

    it('names every kind with this app’s resources in a table', () => {
      const table = tables(parsed)
        .map((t) => t.raw)
        .join('\n');
      const kinds: readonly [string, readonly string[]][] = [
        [
          'Parameter',
          ['rabbitmq-username', 'rabbitmq-password', 'users-signing-key', 'game-cleanup-schedule'],
        ],
        ['Container', ['messaging', 'storage', 'tables', 'Azurite', 'RabbitMQ']],
        [
          'Project',
          [
            'hexmaster-battleship-games-api',
            'hexmaster-battleship-users-api',
            'hexmaster-battleship-events-api',
            'Dapr app id',
          ],
        ],
        ['YARP gateway', ['`gateway`']],
        ['JavaScript app', ['`frontend`']],
        ['Custom resource', ['`devbot`']],
      ];
      for (const [kind, names] of kinds) {
        expect(table, kind).toContain(`| ${kind} |`);
        for (const name of names) expect(table, name).toContain(name);
      }
    });

    it('shows the resource diagram', () => {
      expect(imageFiles(parsed)).toContain('assets/help-resource-graph.svg');
    });
  });

  describe('resource relations', () => {
    const parsed = doc('resource-relations');

    it('explains references and WaitFor with this app’s examples', () => {
      expect(parsed.markdown).toContain('WithReference(tables)');
      expect(parsed.markdown).toMatch(/\.WaitFor\((messaging|tables|gateway)\)/);
      expect(parsed.markdown).toContain('BACKEND_GATEWAY_URL');
    });

    it('states the request and event flow as an ordered list', () => {
      const flow = ofType<Tokens.List>(parsed, 'list').find((list) => list.ordered);
      expect(flow).toBeDefined();
      const steps = flow?.items.map((item) => item.text) ?? [];
      expect(steps.length).toBeGreaterThanOrEqual(4);
      expect(steps[0]).toMatch(/browser to gateway/i);
      expect(steps[1]).toMatch(/users-api/);
      expect(steps[1]).toMatch(/games-api/);
      expect(steps.some((step) => /pub\/sub/i.test(step) && /integration-events/.test(step))).toBe(
        true,
      );
      expect(steps.at(-1)).toMatch(/server-sent events/i);
    });

    it('shows the flow diagram and the start-up diagram', () => {
      expect(imageFiles(parsed)).toEqual(
        expect.arrayContaining([
          'assets/help-request-event-flow.svg',
          'assets/help-startup-order.svg',
        ]),
      );
    });
  });
});
