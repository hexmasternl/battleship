# Design

## Context

**The app today.** The Angular app (`src/Frontend`: Angular 22, zoneless, signals, Angular Material 22.2.2) has three routes:
- the lazy `WelcomePage` at `''`
- the guarded `games/:gameCode`
- a `**` redirect

`app.html` is a bare `<router-outlet />`; there is no navigation shell. The router is set up with `withComponentInputBinding()`.

**Styling.** Design tokens live in `src/styles/_tokens.scss` with the `--bs-` prefix, and `docs/design-system.md` documents every token with its hex value. Material Symbols Outlined is loaded in `index.html`.

**Tests.** Unit tests run on Vitest with `vitest-axe`.

**Assets and HTTP.** `angular.json` publishes only `public/**` as assets today. The HTTP interceptors only touch URLs that start with `/api/`, so a request to `/docs/...` goes to the frontend origin untouched and carries no token.

**Existing docs.** `docs/` already holds:
- `design-system.md` and `.html`
- `suggestions.md`
- `assets/` with `architecture.svg`, `battleship-banner.*` and `battleship-icon.*`

`architecture.svg` already follows the theme: it uses the design system colours and embeds its fonts as base64 `@font-face` data. It is the precedent for new diagrams.

The help content describes facts from:
- the AppHost (`AppHost.cs`, `DaprPrerequisites.cs`, `GatewayRoutes.cs`, `DevBot/`)
- `src/DevTools/HexMaster.Battleship.DevBot`
- `README.md`

## Goals / Non-Goals

**Goals:**
- The help text and diagrams live once, as markdown and SVG in `docs/`. They read well on GitHub and render inside the app from the same files.
- One topic registry drives the overview, the routes, the titles, the previous and next links, the landing tiles, and the mapping from slug to document.
- The markdown renderer and the help pages stay out of the initial bundle.

**Non-Goals:**
- A site-wide navigation shell.
- Search.
- Rendering arbitrary repository markdown such as `README.md` or `design-system.md`.
- Syntax highlighting of code blocks.
- Screenshots of the Aspire dashboard: they cannot follow the theme.
- Localisation.

## Decisions

### 1. Documents and images live directly in `docs/` and `docs/assets/`
There is one document per topic, named after its slug:
- `docs/prerequisites.md`
- `docs/resources.md`
- `docs/resource-relations.md`
- `docs/dapr.md`
- `docs/custom-integration.md`
- `docs/resource-commands.md`

Diagrams are `docs/assets/help-<name>.svg`. The `help-` prefix keeps them apart from the existing branding assets. `architecture.svg` may be reused on the resources page if it still matches the AppHost.

Documents use only relative links:
- `![alt](assets/help-x.svg)` for images
- `[Dapr](dapr.md)` for other topics

GitHub resolves both, and the app rewrites them (decision 4).

Each document starts with one `# Title` heading. The page uses that heading as its level-one heading, so the page has exactly one.

### 2. Publish only the help files through a script-created link to `docs/`
Angular only accepts an asset input inside the workspace root, which is `src/Frontend`. An entry with input `../../docs` fails the build with "asset path must be within the workspace root", and the check has no option to turn it off.

So `scripts/link-docs.mjs` creates `src/Frontend/docs-link`, a directory link to the repository's `docs/`:
- It is a junction on Windows and a directory symlink elsewhere.
- It leaves a correct link alone and replaces a wrong or broken one.
- It fails with a clear message when `docs/` is missing, and never deletes a real folder in the link's place.
- It runs from the npm `prestart`, `prestart:aspire`, `prebuild`, `prewatch` and `pretest` hooks. The Aspire AppHost and CI both go through these npm scripts.
- `docs-link` is gitignored.

Two asset entries with input `docs-link` and output `docs` publish the help files:
- one with glob `{prerequisites,resources,resource-relations,dapr,custom-integration,resource-commands}.md`
- one with glob `assets/help-*.svg`, plus `assets/architecture.svg` if it is reused

The documents are then served at `/docs/<slug>.md` by both `ng serve` (including the Aspire `start:aspire` run) and the production build.

A unit test checks three things:
- every registry entry has a file under `docs/`
- the `docs-link` asset entries publish exactly the registry documents and `assets/help-*.svg`
- every image a document references exists

Alternative considered: input `../../docs`. It is refused by Angular's workspace-root check.

Alternative considered: a committed symlink. It was rejected because git does not create symlinks on every Windows checkout, where it becomes a plain text file.

Alternative considered: publishing all of `docs/**`. It was rejected because it would ship `design-system.html` (around 100 kB), `suggestions.md` and the PNG banners for nothing.

Alternative considered: copying the files into `public/` or another folder with a script. It was rejected because it creates a second copy that can drift from the first, and an edited document would not show under a running `ng serve`.

Alternative considered: moving the help documents under `src/Frontend`. It was rejected because the content belongs in `docs/`, where it reads on GitHub next to the other documentation.

### 3. Render at runtime with `marked`, sanitised by Angular
A `HelpDocumentStore`, a local immutable store following the frontend convention, fetches `/docs/<file>` as text:
- The component hands it the file input once with `connect(file)`.
- The store fetches through `httpResource.text`, keyed by that signal.
- It exposes a `status` slice (`'loading' | 'loaded' | 'error'`) and an `html` slice, both `computed` from the resource.
- Its `retry()` reloads the resource.

`marked` parses the markdown to HTML with GFM enabled, so tables work. The HTML is bound through `[innerHTML]`, so Angular's built-in sanitiser strips scripts and event handlers. No `bypassSecurityTrust*` is used.

The sanitiser keeps only allow-listed attributes. `class`, `target`, `rel`, `title`, `alt` and `tabindex` survive. `id`, `data-*` and `loading` do not, which shapes decision 4.

`marked` is imported only from the help feature, so the bundler keeps it in the help lazy chunk. Its version is pinned exactly in `package.json`.

Alternative considered: `ngx-markdown`. It was rejected because it is a wrapper that brings `marked` plus optional Prism, KaTeX, Mermaid and clipboard integrations and its own module API. `marked` alone is smaller and has no dependencies.

Alternative considered: converting markdown to templates at build time. It was rejected because the request is for templates that render the markdown files. A build step would also add tooling and break the "edit the doc, refresh the page" loop under `ng serve`.

### 4. Rewrite links and images with a `marked` renderer extension
A custom renderer rewrites links and images while the markdown is parsed:
- **Images.** A relative `src` such as `assets/x.svg` becomes `/docs/assets/x.svg`. Images get no `loading="lazy"`: the sanitiser would strip it, and setting it after render is too late to defer the fetch.
- **Links between topics.** A relative link to `<slug>.md`, optionally with `#fragment`, becomes `href="/help/<slug>"` and is marked with the class `help-link`. A class is used rather than a `data-help-link` attribute because the sanitiser strips `data-*`.
- **Other links.** An absolute `http(s)` link gets `target="_blank"` and `rel="noopener"`. Links in `docs/` that are not help documents point to the file on GitHub.
- **Code blocks and tables.** A code block renders as `<pre tabindex="0">`. A table is wrapped in `<div class="help-prose__table" tabindex="0">`. Both scroll on their own, so they take keyboard focus to stay scrollable without a mouse.

The `HelpDocument` component listens for clicks on its host. A plain left-click on an `a.help-link` calls `preventDefault()` and `router.navigateByUrl`, so the navigation stays inside the app. Clicks with a modifier key keep the browser's default behaviour.

Heading `id`s cannot pass the sanitiser, so they are applied after render. `helpHeadingIds()` in `help-markdown.ts` builds GitHub style slugs and numbers repeats. `HelpDocument` sets them on the rendered headings in an `afterRenderEffect`, so `#fragment` deep links work.

### 5. Components
Every topic route renders the same `HelpTopicPage`. It resolves the topic from the slug in route data, so there is no per-topic component. It shows `HelpLayout` around `HelpDocument`. The components are:

- **`help-topics.ts` (registry).** A readonly ordered array of topics. Each topic has `slug`, `title`, `summary`, `icon` (a Material Symbol name) and `file`. `help-routes.ts` generates `''` → `HelpOverview`, one route per slug (with the title and the slug in route data), and `**` → `''`. It is mounted with `loadChildren` at `help` in `app.routes.ts`, with no guard.
- **`HelpLayout`.**
  - It holds a breadcrumb back to `/` and `/help`, and a footer with previous and next links.
  - The previous and next links are `computed` from the slug input against the registry.
- **`HelpDocument`.**
  - It provides `HelpDocumentStore`, connects its `file` input to it, and renders the loading, error-with-retry and content states.
  - It routes clicks on `a.help-link` and applies heading ids after render (decision 4).
  - It scopes a `.help-prose` stylesheet over the rendered HTML: headings in Quantico, body text in Courier Prime, and code blocks that scroll on their own with `overflow-x: auto`.
  - Tables sit in a scroll wrapper, and images use `max-width: 100%`.
  - Every value comes from `--bs-` tokens, following the centralized styling rule.
  - Because the rendered HTML is not part of the template, the styles live in the global partial `src/styles/_help-prose.scss`, scoped by the `.help-prose` class. The project already keeps global partials there, and `::ng-deep` is avoided.
- **`HelpOverview`.** It shows the overview page from the registry; it is not a document.

The topic list reaches the landing tiles through the registry (see decision 7).

### 6. Diagrams as hand-built SVG in the design system theme
Each diagram is a self-contained SVG.

**Colours.**
- Background `#011627`, raised surfaces from the documented surface scale.
- Strokes and accents in primary `#00c0ff` and secondary `#2f87ff`.
- Labels in text `#b2c2d4` or text dim `#7a8694`.
- Danger `#ff2146` only where something fails.
- Any other colour must be a derived value that `docs/design-system.md` documents.

**Typefaces.** Labels use Quantico or Courier Prime, embedded as subset woff2 data URIs as `architecture.svg` does. A font-family stack with the documented fallbacks ensures the SVG still reads well if the embedded font fails.

Each SVG has a `<title>` and a `viewBox` with no fixed width, so it scales.

The set of diagrams:

| Diagram | Topic | Shows |
|---|---|---|
| `help-resource-graph.svg` (or the reused `architecture.svg`) | resources | every resource grouped by kind |
| `help-request-event-flow.svg` | resource relations | browser → gateway → APIs → pub/sub → Events API → SSE |
| `help-startup-order.svg` | resource relations | the `WaitFor` chain from containers to APIs, gateway, frontend and bot |
| `help-dapr-sidecars.svg` | Dapr | apps with sidecars, the RabbitMQ pub/sub component, and the cron binding into Games |
| `help-devbot-integration.svg` | custom integration | `AddBattleshipBot` → resource, sidecar, gateway reference, health check |
| `help-resource-command-flow.svg` | custom resource commands | dashboard command → AppHost → bot `/admin` → state mirror → dashboard properties |

Prerequisites has no diagram; a checklist is clearer there.

A test reads each `help-*.svg` and checks that every `fill` and `stroke` colour is in an allow-list taken from `_tokens.scss` and the design system.

### 7. Landing tiles as a new `HelpTilesSection`
`src/app/welcome/help-tiles-section.ts` is placed after `InfoSection` in `WelcomePage`. It renders one tile per registry topic, plus a "Browse all help" link to `/help`.

Each tile shows a large Material Symbol, the title and the summary, on a raised surface. The tile uses the stretched-link pattern:
- The title is the tile's only `<a routerLink>`, so the link's accessible name is exactly the title.
- The link's `::after` covers the whole tile, so a click anywhere on it navigates, and the focus ring surrounds the whole tile.
- The summary sits outside the link and is attached with `aria-describedby`.

Alternative considered: one anchor wrapping the icon, title and summary, named with `aria-labelledby`. It was rejected because it fails axe's `label-content-name-mismatch` (WCAG 2.5.3) in a real browser: the visible text includes the summary but the name does not. Hiding the summary with `aria-hidden` does not help.

An accent border-glow and lift animate on hover and focus, under `prefers-reduced-motion: no-preference` only. The tiles use their own class, so they look distinct from the `bs-card` rule panels.

The tiles import only the registry, which holds metadata and no `marked`. Importing it keeps the initial bundle unaffected.

## Risks / Trade-offs

- [The new `marked` dependency] → It is pinned exactly, has no dependencies of its own, and is confined to the lazy chunk. The production build's bundle budgets confirm it stays out of the initial bundle.
- [Content drifts from the AppHost] → The documents in `docs/` are now the single source, and `README.md` can link to them. The tasks include a final cross-check against the AppHost.
- [Angular's sanitiser strips something wanted, such as inline SVG or `target` attributes] → Diagrams are `<img>` references, not inline SVG, and survive sanitising. `target` and `rel` are kept by Angular's sanitiser. A unit test asserts that a sample of the rendered output keeps them.
- [An image renders differently on GitHub and in the app] → Fonts are embedded and colours are literal hex values, so the SVG renders the same wherever it is viewed.
- [The asset glob must be updated whenever a topic is added] → The registry-versus-files unit test fails when the two get out of step.
