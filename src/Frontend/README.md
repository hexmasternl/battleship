# Battleship

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 22.2.2.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Theming

The application is themed from the Battleship design system (`docs/design-system.md` at the repository root). Three files under `src/` carry it:

- `styles/_tokens.scss` holds the `--bs-*` custom properties, copied verbatim from the design system's token reference.
- `styles/_palettes.scss` builds the Material 3 tonal palettes from the design system colour scales; every tone is commented with the token or mix it comes from.
- `styles.scss` calls `mat.theme()` with those palettes (dark only), pins the surface, text and outline roles to the design system semantic roles with `mat.theme-overrides()`, and sets the `body` defaults.

Run the commands below from the repository root in a Bash shell (Git Bash on Windows).

### Checking that the frontend tokens agree with the design system

Compares every `--bs-*` name and value in `_tokens.scss` with the token reference in `docs/design-system.md`, and every `$bs-*` scale step that `_palettes.scss` repeats as a Sass variable with the resolved document value. Prints each difference; the trailing line reports `0 differences` and the exit code is 0 when the files agree. When the design system changes, copy its `:root` block over the one in `_tokens.scss`, update the Sass variables in `_palettes.scss`, and re-run.

```bash
node -e '
const fs=require("fs");
const block=f=>{const s=fs.readFileSync(f,"utf8"),i=s.indexOf(":root {");return s.slice(i,s.indexOf("\n}",i))};
const pairs=f=>Object.fromEntries([...block(f).matchAll(/(--bs-[a-z0-9-]+)\s*:\s*([^;]+);/g)].map(m=>[m[1],m[2].trim()]));
const doc=pairs("docs/design-system.md"),app=pairs("src/Frontend/src/styles/_tokens.scss");
const resolve=v=>{let g=0;while(v&&/var\(/.test(v)&&g++<20)v=v.replace(/var\((--[a-z0-9-]+)\)/g,(_,n)=>doc[n]);return v};
let bad=0;
for(const k of new Set([...Object.keys(doc),...Object.keys(app)]))if(doc[k]!==app[k]){bad++;console.log(`${k}: document=${doc[k]??"(missing)"} frontend=${app[k]??"(missing)"}`)}
for(const m of fs.readFileSync("src/Frontend/src/styles/_palettes.scss","utf8").matchAll(/^\$(bs-[a-z0-9-]+):\s*(#[0-9a-f]{6});/gm))if(resolve(doc["--"+m[1]])!==m[2]){bad++;console.log(`$${m[1]}: document=${resolve(doc["--"+m[1]])??"(missing)"} palettes=${m[2]}`)}
console.log(`${Object.keys(doc).length} tokens in the document, ${Object.keys(app).length} in the frontend, ${bad} differences`);
process.exit(bad?1:0)'
```

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
