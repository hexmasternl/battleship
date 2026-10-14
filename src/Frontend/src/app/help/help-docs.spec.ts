// Guards the link between the topic registry, the markdown documents in the repository's `docs/`
// folder and the `angular.json` asset entries that publish them.
import { readFileSync } from 'node:fs';
import { repoDocExists } from '../../testing/repo-docs';
import { HELP_TOPICS } from './help-topics';

interface AssetEntry {
  readonly glob: string;
  readonly input: string;
  readonly output?: string;
}

/** The part of `angular.json` this test reads. */
interface AngularConfig {
  readonly projects: {
    readonly battleship: {
      readonly architect: {
        readonly build: { readonly options: { readonly assets: readonly (AssetEntry | string)[] } };
      };
    };
  };
}

/** The build's asset entries that publish from `docs-link`, the link to the repository's `docs/`. */
function docsAssetEntries(): AssetEntry[] {
  // `ng test` runs from the project root (src/Frontend).
  const config = JSON.parse(readFileSync('angular.json', 'utf8')) as AngularConfig;
  return config.projects.battleship.architect.build.options.assets.filter(
    (entry): entry is AssetEntry => typeof entry !== 'string' && entry.input === 'docs-link',
  );
}

describe('help documents', () => {
  it.each(HELP_TOPICS.map((topic) => [topic.slug, topic.file] as const))(
    'has a document for %s under docs/',
    (_slug, file) => {
      expect(repoDocExists(file)).toBe(true);
    },
  );

  it('publishes exactly the registry documents and the help diagrams to /docs', () => {
    const entries = docsAssetEntries();
    expect(entries.every((entry) => entry.output === 'docs')).toBe(true);
    const slugs = HELP_TOPICS.map((topic) => topic.slug).join(',');
    expect(entries.map((entry) => entry.glob).sort()).toEqual(
      [`{${slugs}}.md`, 'assets/help-*.svg'].sort(),
    );
  });
});
