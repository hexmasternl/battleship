// The slice of Node's `fs` module that unit tests use to read the repository's `docs/` folder.
// The project does not depend on `@types/node`, so the few functions the tests call are declared
// here. Vitest runs in Node, where the real module provides them.
declare module 'node:fs' {
  export function existsSync(path: string): boolean;
  export function readFileSync(path: string, encoding: 'utf8'): string;
  export function readdirSync(path: string): string[];
}
