# Proposal

> Tracks GitHub issue [hexmasternl/battleship#5](https://github.com/hexmasternl/battleship/issues/5).

## Why

The Angular application under `src/Frontend/` still renders the Angular CLI placeholder: `app.routes.ts` is an empty array, `app.html` is the generator template with its own inline Inter font and purple gradients, and nothing on screen belongs to Battleship. The design system (`issue-1-design-system`, merged) and the Material theme (`issue-2-material-theme`, proposed) give the frontend a visual language, but no screen uses it yet. The landing page is the first screen every visitor sees and the only entry to the two things a player can do, create a game or join one, so building it now turns the application from a scaffold into the game's front door and proves the design system in a real Angular route before the game screens are built.

## What Changes

- Add a lazy-loaded landing page route at `/` and remove the Angular CLI placeholder markup and inline styles from the root component, which becomes a bare `<router-outlet>` shell.
- Build a full-viewport hero section with a marine-themed background composed from the design system surfaces (gradient and pattern; no third-party image assets) and a jumbotron split into two equal panels at the `lg` breakpoint (1024px) and stacked below it.
- Left panel: a welcome heading in the heading typeface, an intro paragraph, and two calls to action, "Create a new game" (primary) and "Join an existing game" (secondary), that navigate to `/games/new` and `/games/join`. Both routes render a "coming soon" placeholder panel until the game flows exist.
- Right panel: a bridge console component that types random marine-themed messages, each prefixed with a local-time timestamp in the `[ hh:mm:ss AM ] - ` form, appending at the bottom, scrolling older lines up, never growing past its panel. The scheduler draws the next message at random from a pool of at least 100 distinct messages and shows it between 5 and 15 seconds after the previous one.
- Honour `prefers-reduced-motion: reduce`: messages appear whole instead of being typed, and no looping animation runs.
- Below the hero, an information section with at least four panels (how a game works, the five ships, the 12x12 grid, how turns and hits are resolved) in a responsive grid built from design system cards and panels, readable at 360px wide without horizontal scrolling.
- Meet the frontend accessibility bar: the page passes an automated axe check with no violations, every text/background pair it introduces meets WCAG AA contrast, both calls to action are keyboard reachable with a visible focus ring.
- Unit tests with Vitest for the console scheduler (interval bounds, random selection, cleanup on destroy), the console component's reduced-motion behaviour and the landing page's structure.

## Capabilities

### New Capabilities
- `welcome-page`: what the landing page of the game shows and does: the hero and its two-panel jumbotron, the calls to action and where they lead, the bridge console's message behaviour and timing, the information panels, and the responsiveness, motion and accessibility rules the page must satisfy.

### Modified Capabilities
<!-- none: design-system and material-theme are consumed, not changed. Neither is in openspec/specs/ yet (both are still pending changes), so no delta applies. -->

## Impact

- `src/Frontend/src/app/app.routes.ts`: gains the `/` landing route and the two placeholder routes for the calls to action, all lazy loaded.
- `src/Frontend/src/app/app.html`, `app.ts`, `app.spec.ts`: placeholder markup, inline styles and the "Hello, battleship" test are removed; the root component only hosts the router outlet.
- New feature folder `src/Frontend/src/app/welcome/` with the landing page component, the hero and information section partials, the console component, the console scheduler service, the message pool and their specs. New `src/Frontend/src/app/games/` folder with the "coming soon" placeholder component.
- Design system tokens: the page relies on the `--bs-*` custom properties being present on `:root`. `issue-2-material-theme` task 1.1 adds them; if this change is applied first, the same `_tokens.scss` partial is created here with the identical verbatim copy, and the Material theme change then finds it in place. The Material buttons render in the design system colours only once `issue-2-material-theme` is merged; until then the calls to action are design system `.bs-btn` styled buttons, so the page does not block on the theme.
- New dev dependency: `axe-core` (plus `vitest-axe` matchers) for the automated accessibility check in the unit tests. No runtime dependencies are added.
- No backend change; the Games, Players and Aspire projects are untouched. No data is fetched: the console messages and the information copy are static client-side content.
