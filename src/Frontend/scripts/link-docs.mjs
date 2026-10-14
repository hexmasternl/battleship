// Links the repository's `docs/` folder into the frontend project (npm `prestart`, `prebuild`,
// `pretest` and `prestart:aspire`). The help section publishes its markdown documents and
// diagrams from `docs/`, but Angular only accepts asset inputs inside the workspace root
// (src/Frontend). The link `docs-link` makes `docs/` reachable from there without a second copy.
// It is created on every run rather than committed, because git does not create symlinks on
// every Windows checkout; `docs-link` is gitignored.
import { lstatSync, realpathSync, rmdirSync, symlinkSync, unlinkSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

/** The link inside the project and the folder it points at. */
export const DOCS_LINK = join(root, 'docs-link');
export const DOCS_TARGET = resolve(root, '..', '..', 'docs');

function existingEntry(path) {
  try {
    return lstatSync(path);
  } catch {
    return null;
  }
}

function pointsAt(link, target) {
  try {
    return realpathSync(link) === realpathSync(target);
  } catch {
    return false; // A broken link cannot be resolved.
  }
}

function removeLink(link) {
  try {
    unlinkSync(link);
  } catch {
    rmdirSync(link); // A Windows junction is removed as a directory.
  }
}

/**
 * Makes `link` a directory link to `target`: a junction on Windows, a directory symlink elsewhere.
 * Leaves a correct link alone and replaces a wrong or broken one. Returns what it did:
 * `'kept'`, `'created'` or `'replaced'`. Throws when the target is missing or when `link` is a
 * real file or folder, which it never deletes.
 */
export function linkDocs(link = DOCS_LINK, target = DOCS_TARGET, platform = process.platform) {
  if (!existingEntry(target)?.isDirectory()) {
    throw new Error(`link-docs: the docs folder ${target} does not exist.`);
  }
  const entry = existingEntry(link);
  if (entry !== null && !entry.isSymbolicLink()) {
    throw new Error(
      `link-docs: ${link} exists and is not a link. Remove it so it can be linked to ${target}.`,
    );
  }
  if (entry !== null && pointsAt(link, target)) return 'kept';
  if (entry !== null) removeLink(link);
  symlinkSync(target, link, platform === 'win32' ? 'junction' : 'dir');
  return entry === null ? 'created' : 'replaced';
}

function main() {
  try {
    const result = linkDocs();
    if (result !== 'kept') console.log(`link-docs: ${result} ${DOCS_LINK} -> ${DOCS_TARGET}`);
  } catch (error) {
    console.error(error instanceof Error ? error.message : error);
    process.exit(1);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
