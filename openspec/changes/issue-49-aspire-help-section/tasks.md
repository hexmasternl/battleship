# Tasks

All frontend work is done by the `angular-frontend` agent in `src/Frontend`. Run tests with the project's Vitest command. Run the build in the production configuration.

## 1. Help routing and topic registry

- [ ] 1.1 Create `src/app/help/help-topics.ts`. It is the ordered, readonly registry of the six topics, and each entry has a slug, title, summary, Material Symbol icon and lazy `loadComponent`. The slugs are `prerequisites`, `resources`, `resource-relations`, `dapr`, `custom-integration` and `resource-commands`. Add stub topic components. Verify with a unit test asserting the order, the unique slugs and the non-empty titles and summaries.
- [ ] 1.2 Create `src/app/help/help-routes.ts`, generated from the registry:
  - `''` → overview
  - one route per slug, with title `<topic> | Help | Battleship` and the slug in route data
  - `**` → redirect to `''`

  Mount it with `loadChildren` at `help` in `app.routes.ts`, before the wildcard and without `userGuard`. Verify with `app.routes.spec.ts` tests:
  - `/help` renders the overview
  - `/help/dapr` renders the Dapr topic with its title
  - `/help/not-a-topic` redirects to `/help`
  - no guard runs and no gateway request is made

## 2. Help layout and overview

- [ ] 2.1 Build `HelpLayout` with:
  - a breadcrumb to `/` and `/help`
  - a content slot
  - previous and next links computed from the slug input against the registry (hidden at the ends)
  - the shared `help-code`, `help-callout` and resource-list styles, built from `--bs-` tokens only

  Verify with unit tests for the links on the first, a middle and the last topic, and with an axe check.
- [ ] 2.2 Build `HelpOverview` at `/help`: one level-one heading, a link to `/`, and the six topic links in registry order, each with its title and summary. Verify with unit tests for the link order, the hrefs and the axe check, and that the page does not scroll horizontally at 360px (layout CSS uses wrapping grids).

## 3. Topic content

Each topic is written against its source files, named in an HTML comment at the top of the template. Each topic is wrapped in `HelpLayout` and has a spec that asserts the content its requirement demands, plus an axe check.

- [ ] 3.1 Prerequisites topic. Content:
  - .NET 10 SDK, Docker, Dapr CLI plus `dapr init`, Node.js/npm, Aspire CLI
  - the AppHost start command, in `help-code`
  - the fail-fast Dapr prerequisite check

  Source: `README.md` and `DaprPrerequisites.cs`. Verify with the topic spec.
- [ ] 3.2 Resources topic. Content:
  - what a resource is
  - parameters, the `messaging`/`storage`/`tables` containers, the three API projects with Dapr sidecars, `gateway`, `frontend` and `devbot`, each with its role

  Source: `AppHost.cs`. Verify with the topic spec.
- [ ] 3.3 Resource relations topic. Content:
  - references and `WaitFor`, with this app's examples
  - an inline, `aria-hidden` SVG of the flow browser → gateway → APIs → pub/sub → Events API → SSE, drawn with token colours
  - an ordered list stating the same flow in text

  Verify with the topic spec: the list exists, the SVG is `aria-hidden`, and the axe check passes.
- [ ] 3.4 Dapr topic. Content:
  - sidecars for the Games, Users and Events APIs and the dev bot
  - the `pubsub.rabbitmq` component and the `integration-events` topic
  - the `game-cleanup` cron input binding and its schedule parameter
  - the prerequisite check
  - no mention of state stores or service invocation as being used

  Verify with the topic spec, including a negative assertion on those two terms.
- [ ] 3.5 Custom integration topic. Content:
  - custom resource type plus builder extensions
  - `AddBattleshipBot`, its own sidecar, the gateway reference, the health check
  - intelligence presets, join grace period and shot delay
  - the bot's opponent behaviour

  Source: `DevBot/BattleshipBotResourceBuilderExtensions.cs` and `src/DevTools/HexMaster.Battleship.DevBot`. Verify with the topic spec.
- [ ] 3.6 Custom resource commands topic. Content:
  - what a resource command is
  - Set intelligence (preset choice), Enable and Disable auto-join, Join game (8-character code)
  - that the commands are enabled only while the bot is healthy
  - the live `devbot.*` properties mirrored to the dashboard

  Source: `DevBot/BotCommands.cs` and `BotStateMirror.cs`. Verify with the topic spec.

## 4. Landing page help tiles

- [ ] 4.1 Build `HelpTilesSection` in `src/app/welcome/`, rendered after `InfoSection` in `WelcomePage`. It has:
  - a heading
  - six tiles from the registry, each a single `routerLink` anchor with icon, title and summary
  - a "Browse all help" link to `/help`

  The tiles are styled distinctly from `bs-card`, use tokens only, and have a responsive grid (one column at 360px, two or more from 768px). Hover and focus motion sits only under `prefers-reduced-motion: no-preference`. Verify with unit tests:
  - tile count, hrefs and accessible names
  - activating the resource-commands tile navigates to `/help/resource-commands` with no session
  - axe passes

  Update `welcome-page.spec.ts` for the new section.

## 5. Integration checks

- [ ] 5.1 Run the full frontend unit test suite and the production build. Verify:
  - all tests pass
  - there are no build or budget errors
  - the help topics are emitted in lazy chunks, separate from the initial bundle
- [ ] 5.2 Serve the app and open these routes at 360px and 1280px widths:
  - `/`, then a tile
  - `/help`
  - every `/help/<slug>`
  - `/help/unknown`

  Use Playwright headless if the Chrome extension is unavailable. Verify that there is no horizontal scroll, that the navigation links work, and that the reduced-motion emulation shows static tiles.
- [ ] 5.3 Cross-check every topic against `AppHost.cs`, `DevBot/` and `README.md`: resource names, command names, Dapr components and prerequisites. Verify that no stated fact contradicts the current code, and fix any drift.
