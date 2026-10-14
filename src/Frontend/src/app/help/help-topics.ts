/**
 * One help topic. The registry below is the single source for the help overview, the help
 * routes, the document titles, the previous and next links, the landing page tiles and the
 * mapping from slug to markdown document. It holds metadata only, so importing it does not pull
 * the markdown renderer into the importing chunk.
 */
export interface HelpTopic {
  /** The route segment under `/help/` and the document name without its extension. */
  readonly slug: string;
  readonly title: string;
  /** One sentence shown on the overview and on the landing tile. */
  readonly summary: string;
  /** A Material Symbols Outlined ligature name. */
  readonly icon: string;
  /** The markdown document in the repository's `docs/` folder, published at `/docs/<file>`. */
  readonly file: string;
}

/** The help topics in reading order. */
export const HELP_TOPICS: readonly HelpTopic[] = [
  {
    slug: 'prerequisites',
    title: 'Prerequisites',
    summary: 'The tools you need to run this Aspire demo on your own machine.',
    icon: 'checklist',
    file: 'prerequisites.md',
  },
  {
    slug: 'resources',
    title: 'Resources',
    summary: 'What an Aspire resource is, and every resource this app is built from.',
    icon: 'deployed_code',
    file: 'resources.md',
  },
  {
    slug: 'resource-relations',
    title: 'Resource relations',
    summary: 'How references and start-up order connect the resources to each other.',
    icon: 'account_tree',
    file: 'resource-relations.md',
  },
  {
    slug: 'dapr',
    title: 'Dapr',
    summary: 'The Dapr sidecars, the pub/sub component and the cron binding this app uses.',
    icon: 'hub',
    file: 'dapr.md',
  },
  {
    slug: 'custom-integration',
    title: 'Custom integration',
    summary: 'How the dev bot is added to the AppHost as a custom Aspire hosting integration.',
    icon: 'extension',
    file: 'custom-integration.md',
  },
  {
    slug: 'resource-commands',
    title: 'Custom resource commands',
    summary: 'How the Aspire dashboard controls the dev bot through custom resource commands.',
    icon: 'terminal',
    file: 'resource-commands.md',
  },
];

/** The topics before and after a topic in reading order; `null` at either end or when unknown. */
export interface HelpNeighbours {
  readonly previous: HelpTopic | null;
  readonly next: HelpTopic | null;
}

/** The topics before and after the topic with this slug. */
export function helpNeighbours(slug: string): HelpNeighbours {
  const index = HELP_TOPICS.findIndex((topic) => topic.slug === slug);
  if (index === -1) return { previous: null, next: null };
  return { previous: HELP_TOPICS[index - 1] ?? null, next: HELP_TOPICS[index + 1] ?? null };
}

/** The topic with this slug, or `undefined` when there is none. */
export function findHelpTopic(slug: string): HelpTopic | undefined {
  return HELP_TOPICS.find((topic) => topic.slug === slug);
}
