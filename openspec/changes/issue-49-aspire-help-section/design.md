# Design

## Context

The Angular app (`src/Frontend`, Angular 22, zoneless, signals, Angular Material 22.2.2) has three routes: the lazy `WelcomePage` at `''`, the guarded `games/:gameCode`, and a `**` redirect. `app.html` is a bare `<router-outlet />`; there is no navigation shell. `WelcomePage` composes `HeroSection` and `InfoSection`, and `InfoSection` renders `bs-card` panels about the game rules. Design tokens live in `src/styles/_tokens.scss` with the `--bs-` prefix. Material Symbols Outlined is already loaded in `index.html` and is used through `MatIcon`. Unit tests run on Vitest with `vitest-axe`.

The facts the help content describes are taken from:

- `src/Aspire/.../AppHost/AppHost.cs`, `DaprPrerequisites.cs`, `GatewayRoutes.cs`
- `src/Aspire/.../AppHost/DevBot/` (`BattleshipBotResourceBuilderExtensions.cs`, `BotCommands.cs`, `BotStateMirror.cs`)
- `README.md`

## Goals / Non-Goals

**Goals:**
- One topic registry drives the overview, the routes, the titles, the previous and next links, and the landing tiles, so a topic is added in one place.
- The help section is a self-contained lazy feature folder with no backend calls.

**Non-Goals:**
- A site-wide header or navigation shell.
- Search.
- Rendering `README.md` or any markdown at runtime.
- Localisation.
- Generating content from the AppHost automatically.

## Decisions

### 1. A typed topic registry
`src/app/help/help-topics.ts` exports a readonly array of topics. Each entry has:
- `slug`
- `title`
- `summary`
- `icon` (a Material Symbol name)
- `loadComponent`

Array order is the reading order.

The routes are generated from this array:

```
help
├── '' → HelpOverview
├── <slug>, one per topic → lazy topic component, title from the registry
└── ** → redirect to '' (back to /help)
```

`help-routes.ts` is loaded with `loadChildren` from `app.routes.ts`. The previous and next links and the landing tiles read the same array.

Alternative considered: a single `:topic` route with a component that switches on the slug. It was rejected because it loses per-topic lazy loading and per-topic route titles, and it needs a guard for unknown slugs.

### 2. Content authored as standalone Angular components
There is one component per topic, under `src/app/help/topics/`, with an HTML template. The components are static and have no state store; each has a single `topic` input or reads nothing at all.

Alternative considered: markdown files rendered with a library such as `ngx-markdown`. It was rejected because it adds a dependency, an extra sanitising surface, and styling that would sit outside the design system. Templates keep the content type-checked and let it use design-system classes and `MatIcon`.

### 3. A shared `HelpLayout` component
Every topic is wrapped in the same `HelpLayout` component. It provides:
- a breadcrumb back to `/` and `/help`
- the content slot
- the previous and next footer

`HelpLayout` takes the current slug as an input, set through route `data` and bound with `withComponentInputBinding`.

The layout also provides the shared presentational pieces:
- a `help-code` block for commands, which scrolls horizontally within itself
- a `help-callout`
- a definition-list style for the resource tables

This keeps every topic page consistent and keeps the "way back" requirement in one place.

### 4. The resource relations flow is drawn as inline SVG with a text equivalent
The resource-relations flow is drawn as inline SVG that uses token colours, and it is followed by an ordered list that states the same flow in text. The SVG is marked `aria-hidden`, so assistive technology reads the list.

Alternative considered: reusing `docs/assets/architecture.svg`. It was rejected because the file is outside the Angular assets, has fixed colours, and is not theme-aware.

### 5. Landing tiles as a new `HelpTilesSection`
A new `src/app/welcome/help-tiles-section.ts` is placed after `InfoSection` in `WelcomePage`. It renders six tiles, one per registry topic, plus a "Browse all help" link to `/help`.

Each tile is a single `<a routerLink>`. It shows a large Material Symbol, the title and the summary, on a raised surface. A subtle accent border-glow and lift animate on hover and focus. All motion sits under `@media (prefers-reduced-motion: no-preference)`.

The tiles use their own class, not `bs-card`, so they look distinct from the rule panels. Every colour, size and shadow comes from tokens, following the centralized styling rule.

### 6. No state stores
The help pages and tiles are pure presentation over a constant registry, so the local immutable store convention has nothing to hold here. Components expose the registry as a constant, and the current-topic neighbours as `computed` signals derived from the slug input.

## Risks / Trade-offs

- [Content drifts from the AppHost or `README.md`] → Every topic names the source files it describes, in an HTML comment at the top of the template. The tasks include a final cross-check against the AppHost.
- [The landing page grows longer and the help chunk grows] → The tiles section is small. The help topics are lazy and stay out of the initial bundle. The production build budget check is part of the verification.
- [Hand-written SVG is hard to keep accessible] → The text list is authoritative, and the SVG is decorative (`aria-hidden`).
