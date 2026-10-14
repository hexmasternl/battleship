# Tasks

All frontend work is done by the `angular-frontend` agent in `src/Frontend`. Run tests with the project's Vitest command. Run the build in the production configuration.

## 1. Topic registry, routing and doc publishing

- [ ] 1.1 Create `src/app/help/help-topics.ts`. It is the ordered, readonly registry of the six topics, and each entry has `slug`, `title`, `summary`, `icon` (a Material Symbol name) and `file`:
  - `prerequisites`
  - `resources`
  - `resource-relations`
  - `dapr`
  - `custom-integration`
  - `resource-commands`

  Verify with a unit test asserting the order, the unique slugs, the non-empty titles and summaries, and that each `file` equals `<slug>.md`.
- [ ] 1.2 Add placeholder documents `docs/<slug>.md` for all six topics, each with only a `# Title` and one sentence. Add two `angular.json` asset entries from `../../docs` to `docs`: the six topic documents and `assets/help-*.svg`. Verify that `ng build` emits `docs/<slug>.md` and does not emit `design-system.html`, `suggestions.md` or the PNG banners. Add a unit test that every registry `file` exists under the repo's `docs/`.
- [ ] 1.3 Create `src/app/help/help-routes.ts`, generated from the registry:
  - `''` → `HelpOverview`
  - one route per slug → `HelpTopicPage`, with title `<topic> | Help | Battleship` and the slug in route data
  - `**` → redirect to `''`

  Mount it with `loadChildren` at `help` in `app.routes.ts`, before the wildcard and without `userGuard`. Verify with `app.routes.spec.ts` tests:
  - `/help` renders the overview
  - `/help/dapr` renders the topic page with its title
  - `/help/not-a-topic` redirects to `/help`
  - no guard runs

## 2. Markdown rendering

- [ ] 2.1 Add `marked` to `dependencies` with an exact version, and record the reason in the commit message. Implement `help-markdown.ts`: a `marked` instance with GFM and a custom renderer that:
  - rewrites relative image `src` to `/docs/assets/...` and adds `loading="lazy"`
  - rewrites `<slug>.md[#frag]` links to `/help/<slug>[#frag]` with `data-help-link`
  - gives external `http(s)` links `target="_blank"` and `rel="noopener"`
  - points other relative links at the file on GitHub
  - adds slug `id`s to headings

  Verify with unit tests for each rewrite and for a table, a code block and a block quote.
- [ ] 2.2 Implement `HelpDocumentStore`, a local immutable store with `loading | loaded | error` and `retry()`, which fetches `/docs/<file>` as text and renders it with `help-markdown`. Verify with unit tests on `HttpTestingController`:
  - the loading state, then the loaded HTML
  - the error state, then `retry()` sending a second request
  - no request goes to an `/api/` URL
- [ ] 2.3 Implement the `HelpDocument` component:
  - It binds the rendered HTML through `[innerHTML]`, with no `bypassSecurityTrust*`.
  - It handles clicks on `a[data-help-link]` with `router.navigateByUrl`; clicks with a modifier key keep the default behaviour.
  - It shows the loading and error-with-retry states.

  Add `src/styles/_help-prose.scss`, built from `--bs-` tokens only: Quantico headings, Courier Prime body, code blocks that scroll on their own, tables in a scroll wrapper, and images capped at `max-width: 100%`. Verify with unit tests:
  - a `<script>` and an `onclick` in the source are stripped
  - `target` and `rel` survive
  - an in-app link click navigates without reloading the page
  - axe passes on a rendered sample

## 3. Help layout and pages

- [ ] 3.1 Build `HelpLayout` with:
  - a breadcrumb to `/` and `/help`
  - a content slot
  - previous and next links computed from the slug input against the registry, hidden at the ends

  Verify with unit tests on the first, a middle and the last topic, and with an axe check.
- [ ] 3.2 Build `HelpTopicPage`, which resolves the topic from the route-data slug and renders `HelpLayout` around `HelpDocument`. Build `HelpOverview`, with one level-one heading, a link to `/`, and the six topic links in registry order, each with its title, summary and icon. Verify with unit tests:
  - the overview's link order and hrefs
  - a topic page renders exactly one `h1`, which comes from the document
  - axe passes on both pages

## 4. Themed diagrams

Each diagram is a self-contained SVG in `docs/assets/` that follows design decision 6:
- only design system colours
- Quantico and Courier Prime embedded as subset woff2 data URIs, with fallback stacks
- a `<title>`
- a `viewBox` with no fixed width

- [ ] 4.1 Add a unit test that reads every `docs/assets/help-*.svg` and fails on any `fill` or `stroke` colour outside an allow-list taken from the design system tokens, or on a missing `<title>`. Verify that it passes on an empty set and fails on a deliberately wrong sample fixture.
- [ ] 4.2 Draw `help-resource-graph.svg`: every resource grouped as parameters, containers, projects with sidecars, gateway, frontend and devbot. Alternatively, decide to reuse `architecture.svg` after checking it against `AppHost.cs`. Verify with the colour test and by viewing it in the browser and in GitHub's markdown preview.
- [ ] 4.3 Draw `help-request-event-flow.svg` (browser → gateway → Users/Games APIs → pub/sub → Events API → SSE) and `help-startup-order.svg` (the `WaitFor` chain). Verify with the colour test and by viewing both.
- [ ] 4.4 Draw `help-dapr-sidecars.svg`: four sidecars, the RabbitMQ pub/sub component on `integration-events`, and the `game-cleanup` cron binding into the Games API. Verify with the colour test and by viewing it.
- [ ] 4.5 Draw `help-devbot-integration.svg` (`AddBattleshipBot` → resource, sidecar, gateway reference, health check) and `help-resource-command-flow.svg` (dashboard command → AppHost → bot `/admin` → state mirror → dashboard properties). Verify with the colour test and by viewing both.

## 5. Topic documents

Each document is written against its source files and starts with a single `# Title`. Use only relative links and images, and put alt text on every image. Each diagram's information is also stated in text. A shared unit test parses every help document and asserts:
- exactly one level-one heading
- every image has non-empty alt text and its file exists in `docs/assets/`
- every `.md` link targets a registry topic

Extend it with the per-topic content assertions below.

- [ ] 5.1 `docs/prerequisites.md`:
  - .NET 10 SDK, Docker, Dapr CLI plus `dapr init`, Node.js/npm, Aspire CLI, each with an install link
  - the AppHost start command in a code block
  - the fail-fast Dapr prerequisite check

  Source: `README.md` and `DaprPrerequisites.cs`. Verify with the content assertion naming each tool.
- [ ] 5.2 `docs/resources.md`:
  - what a resource is
  - each kind with this app's resources and roles, in a table
  - the resource diagram

  Source: `AppHost.cs`. Verify with the content assertions.
- [ ] 5.3 `docs/resource-relations.md`:
  - references and `WaitFor`, with this app's examples
  - the flow diagram and the start-up diagram
  - an ordered list stating the flow in text

  Verify with the content assertions: the ordered list and at least one `WaitFor` example.
- [ ] 5.4 `docs/dapr.md`:
  - sidecars, the `pubsub.rabbitmq` component and the `integration-events` topic
  - the `game-cleanup` cron binding and its schedule parameter
  - the prerequisite check and the sidecar diagram
  - no claim that state stores or service invocation are used

  Verify with the content assertions, including a negative assertion on those two terms.
- [ ] 5.5 `docs/custom-integration.md`:
  - custom resource type plus builder extensions
  - a short `AddBattleshipBot(...)` code excerpt
  - its own sidecar, the gateway reference, the health check
  - intelligence presets, grace period and shot delay
  - the opponent behaviour and the integration diagram

  Source: `DevBot/BattleshipBotResourceBuilderExtensions.cs` and `src/DevTools/HexMaster.Battleship.DevBot`. Verify with the content assertions.
- [ ] 5.6 `docs/resource-commands.md`:
  - what a resource command is
  - Set intelligence, Enable and Disable auto-join, Join game (8-character code)
  - that the commands are enabled only while the bot is healthy
  - the live `devbot.*` properties and the command-flow diagram

  Source: `DevBot/BotCommands.cs` and `BotStateMirror.cs`. Verify with the content assertions.
- [ ] 5.7 In `README.md`, link the help documents from the matching sections, keeping the README's own summaries short. Verify that the links resolve on GitHub's preview of the branch.

## 6. Landing page help tiles

- [ ] 6.1 Build `HelpTilesSection` in `src/app/welcome/`, rendered after `InfoSection` in `WelcomePage`. It has:
  - a heading
  - six tiles from the registry, each a single `routerLink` anchor with icon, title and summary
  - a "Browse all help" link to `/help`

  The tiles are styled distinctly from `bs-card`, use tokens only, and have a responsive grid (one column at 360px, two or more from 768px). Hover and focus motion sits only under `prefers-reduced-motion: no-preference`. Verify with unit tests:
  - tile count, hrefs and accessible names
  - activating the resource-commands tile navigates to `/help/resource-commands` with no session
  - axe passes

  Update `welcome-page.spec.ts` for the new section.

## 7. Integration checks

- [ ] 7.1 Run the full frontend unit test suite and the production build. Verify:
  - all tests pass
  - there are no build or budget errors
  - `marked` and the help pages are in lazy chunks, absent from the initial bundle
  - only the help documents and `help-*` images are emitted under `docs/`
- [ ] 7.2 Serve the app and open these routes at 360px and 1280px widths:
  - `/`, then a tile
  - `/help`
  - every `/help/<slug>`
  - `/help/unknown`

  Use Playwright headless if the Chrome extension is unavailable. Verify:
  - the documents and diagrams render in theme
  - in-document links navigate without a reload
  - there is no horizontal scroll
  - the reduced-motion emulation shows static tiles
- [ ] 7.3 Cross-check every document and diagram against `AppHost.cs`, `DevBot/` and `README.md`: resource names, command names, Dapr components and prerequisites. Verify that no stated fact contradicts the current code, and fix any drift.
