// Test helpers for the repository's `docs/` folder, which holds the help documents and diagrams.
// Only unit tests import this file: it reads the file system, so it never reaches a browser.
import { existsSync, readFileSync, readdirSync } from 'node:fs';

/** The `docs/` folder relative to the directory `ng test` runs from (src/Frontend). */
export const REPO_DOCS_DIR = '../../docs';

/** Whether `docs/<path>` exists. */
export function repoDocExists(path: string): boolean {
  return existsSync(`${REPO_DOCS_DIR}/${path}`);
}

/** The text of `docs/<path>`. */
export function readRepoDoc(path: string): string {
  return readFileSync(`${REPO_DOCS_DIR}/${path}`, 'utf8');
}

/** The names of the files directly in `docs/<folder>`. */
export function listRepoDocs(folder = ''): string[] {
  return readdirSync(folder === '' ? REPO_DOCS_DIR : `${REPO_DOCS_DIR}/${folder}`);
}
