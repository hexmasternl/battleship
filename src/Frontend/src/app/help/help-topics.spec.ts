import { HELP_TOPICS, findHelpTopic, helpNeighbours } from './help-topics';

describe('helpNeighbours', () => {
  it('has no previous topic for the first topic', () => {
    const { previous, next } = helpNeighbours('prerequisites');
    expect(previous).toBeNull();
    expect(next?.slug).toBe('resources');
  });

  it('has both neighbours for a middle topic', () => {
    const { previous, next } = helpNeighbours('dapr');
    expect(previous?.slug).toBe('resource-relations');
    expect(next?.slug).toBe('custom-integration');
  });

  it('has no next topic for the last topic', () => {
    const { previous, next } = helpNeighbours('resource-commands');
    expect(previous?.slug).toBe('custom-integration');
    expect(next).toBeNull();
  });

  it('has no neighbours for an unknown slug', () => {
    expect(helpNeighbours('not-a-topic')).toEqual({ previous: null, next: null });
  });
});

describe('HELP_TOPICS', () => {
  it('lists the six topics in reading order', () => {
    expect(HELP_TOPICS.map((topic) => topic.slug)).toEqual([
      'prerequisites',
      'resources',
      'resource-relations',
      'dapr',
      'custom-integration',
      'resource-commands',
    ]);
  });

  it('has unique slugs', () => {
    const slugs = HELP_TOPICS.map((topic) => topic.slug);
    expect(new Set(slugs).size).toBe(slugs.length);
  });

  it.each(HELP_TOPICS.map((topic) => [topic.slug, topic] as const))(
    'gives %s a title, a summary and an icon',
    (_slug, topic) => {
      expect(topic.title.trim().length).toBeGreaterThan(0);
      expect(topic.summary.trim().length).toBeGreaterThan(0);
      expect(topic.icon).toMatch(/^[a-z_]+$/);
    },
  );

  it.each(HELP_TOPICS.map((topic) => [topic.slug, topic] as const))(
    'names the document of %s after its slug',
    (slug, topic) => {
      expect(topic.file).toBe(`${slug}.md`);
    },
  );
});

describe('findHelpTopic', () => {
  it('finds a topic by its slug', () => {
    expect(findHelpTopic('dapr')?.title).toBe('Dapr');
  });

  it('returns undefined for an unknown slug', () => {
    expect(findHelpTopic('not-a-topic')).toBeUndefined();
  });
});
