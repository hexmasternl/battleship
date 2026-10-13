# Design

## Context

See proposal.md for motivation. Observed state of `src/Frontend/`: Angular 22.2 with `@angular/material` 22.2.2 and `@angular/cdk`, SCSS, Vitest with jsdom for unit tests (`ng test`), prefix `battle`, production budgets of 500kB warning / 1MB error on the initial bundle and 4kB / 8kB on any component style. `app.routes.ts` is `[]`; `app.ts` is a standalone root component with a `title` signal and a `RouterOutlet`; `app.html` is the CLI placeholder with an inline `<style>` block; `app.spec.ts` asserts "Hello, battleship". `src/Frontend/CLAUDE.md` fixes the house rules: standalone components, signals and `computed()`, `inject()`, lazy feature routes, native control flow, `host` bindings instead of decorators, `NgOptimizedImage` for static images, axe and WCAG AA.

The design system (`docs/design-system.md`, merged with `issue-1-design-system`) provides the `--bs-*` tokens: colour roles (`--bs-color-bg`, `--bs-color-surface`, `--bs-color-accent`, `--bs-color-secondary`, `--bs-color-text`, `--bs-color-text-strong`, `--bs-color-text-dim`, `--bs-color-grid-line`), the three typefaces (`--bs-font-heading` Quantico, `--bs-font-body` Courier Prime, `--bs-font-panel` Bitcount Single at weight 200), the spacing, radius, breakpoint, shadow and glow scales, the motion tokens (`--bs-duration-*`, `--bs-ease-*`) and the reduced-motion rule, the `.bs-btn--primary` / `.bs-btn--secondary` button recipes, `.bs-card`, `.bs-panel` and `.bs-panel--info`, and the four instrument motifs (radar, sonar, compass, gauge). Breakpoints are literal values in media queries: `sm` 480px, `md` 768px, `lg` 1024px, `xl` 1280px. `--bs-container` is 1200px.

`issue-2-material-theme` (proposed, not merged) will put the token block on `:root` in `src/Frontend/src/styles/_tokens.scss` and theme Material. Its order relative to this change is not fixed.

## Goals / Non-Goals

**Goals:**
- A landing page that is built entirely from design system tokens and recipes, so it is the first proof of the design system inside Angular.
- A console whose timing, selection and animation logic is a plain service with injected clock and random sources, so it is unit tested without timers or DOM.
- A page that works and reads well with no web fonts, no JavaScript animation under reduced motion, and no Material theme yet.

**Non-Goals:**
- The create-game and join-game flows; their routes only render a placeholder.
- Fetching anything from a server: messages and copy are static.
- Theming Material (owned by `issue-2-material-theme`).
- A shared layout shell (top bar, footer) for the whole application; the landing page owns its own chrome for now.

## Decisions

**Route and folder layout.** `app.routes.ts` gets three lazy routes: `''` loads `welcome/welcome-page` (`loadComponent`), `games/new` and `games/join` both load `games/coming-soon` with a route `data.feature` of `create` or `join` so one placeholder serves both. A wildcard redirect to `''` keeps unknown paths on the landing page. The root component becomes `<router-outlet />` only; its spec checks that the outlet is present. Feature folder `src/Frontend/src/app/welcome/` holds `welcome-page.ts` (route component, inline template kept short by delegating to `hero-section.ts` and `info-section.ts`), `bridge-console.ts` (the console component), `console-scheduler.ts` (the service), `console-messages.ts` (the pool) and a spec per file. Alternative considered: one large component; rejected because the 4kB component style budget forces the hero, console and info section styles apart anyway, and the pieces test better in isolation.

**Design system tokens in the app.** The page needs `--bs-*` on `:root`. If `issue-2-material-theme` is merged first, nothing to do. Otherwise task 1.2 creates `src/Frontend/src/styles/_tokens.scss` with the verbatim token block from `docs/design-system.md` and a `@use` from `styles.scss`, exactly as the Material theme change specifies, so that change later finds the file already in place and its token-diff check passes. The buttons, cards and panels are implemented as SCSS classes (`.bs-btn`, `.bs-card`, `.bs-panel`, `.bs-panel--info`) in a shared partial `src/Frontend/src/styles/_components.scss`, following the recipes in sections 7.1 and 7.3 of the design system, rather than as Material components. Alternative considered: `mat-button` for the calls to action; rejected because without the Material theme they would render azure, and the design system already prescribes the exact button recipe. When the theme lands, the buttons can be swapped for Material buttons in a later change without touching the specs.

**Hero background.** A layered CSS background on the hero: a vertical gradient from `--bs-color-bg` through `--bs-color-surface` to `--bs-color-bg`, a repeating radial "sonar ring" pattern in `--bs-color-grid-line` at low opacity, and a subtle horizon line in `--bs-color-accent-subtle`. Optionally one static inline SVG radar motif from the design system in the corner, which animates its sweep with `--bs-duration-sweep` and `--bs-ease-linear` and stops under reduced motion. No raster images, so nothing to license and nothing for `NgOptimizedImage` to manage. Alternative considered: a stock ocean photograph; rejected because of licensing and because it would not use design system colours.

**Layout.** Hero: `min-height: 100dvh`, content centred, side gutter `--bs-gutter`, max width `--bs-container`. Jumbotron: CSS grid with one column below 1024px and `1fr 1fr` at `@media (min-width: 1024px)`; welcome panel first in DOM order so it is read first and stacks first. Panels use `.bs-panel` with `--bs-radius-lg` and `--bs-shadow-3`. Info section: grid of `.bs-card` elements, one column at 360px, two at 768px, four at 1280px; each card's heading in `--bs-font-heading`, copy in `--bs-font-body`. The four panels: "How a game works", "The fleet" (five ships with lengths rendered as `.bs-ship`-style bars), "The grid" (12x12, A to L, 1 to 12), "Turns and hits" (cell states with the miss/hit/sunk markers from section 8.3).

**Console scheduler as a pure service.** `ConsoleScheduler` (`providedIn: 'root'` is not needed; provided at the console component so each console gets its own instance) exposes `start()`, `stop()` and a `lines` signal. It takes two injected collaborators through small injection tokens with defaults: `CONSOLE_RANDOM` (`() => number` in [0, 1), default `Math.random`) and `CONSOLE_CLOCK` (`() => Date`, default `() => new Date()`), plus the message pool through `CONSOLE_MESSAGES`. Timing: `delayMs = 5000 + floor(random() * 10001)` so both 5s and 15s are reachable. Selection: `index = floor(random() * pool.length)`; if the index equals the previous index, use `(index + 1) % pool.length`. The first line is scheduled with `delayMs = floor(random() * 5001)`. Timers use `setTimeout` captured at construction so tests can install Vitest fake timers; `stop()` clears the pending timer and is called from the component's `DestroyRef.onDestroy`. Lines are kept in a ring of at most 12 entries. The timestamp is formatted by a pure function `formatConsoleTimestamp(date)` with `Intl.DateTimeFormat('en-US', { hour12: true, hour: '2-digit', minute: '2-digit', second: '2-digit' })` plus the `[ ` and ` ] - ` framing, which yields `[ 10:18:37 AM ] - `. Alternative considered: RxJS `timer`/`interval` with `switchMap`; rejected because the random re-arm per message reads more clearly as a signal plus a single pending timeout, and the house rules prefer signals.

**Typing animation.** The component renders each line as the timestamp prefix plus a `<span>` whose visible text grows. The typed effect is driven by the scheduler as well: when a line is added, the service emits a `typedLength` signal per line that advances one character every 30ms (so the longest messages of about 80 characters finish within 2.4 seconds, well inside the 5 second minimum gap) and the template slices the text to that length. Under reduced motion, detected with `matchMedia('(prefers-reduced-motion: reduce)')` injected behind a `REDUCED_MOTION` token for testability, `typedLength` jumps to the full length immediately. A blinking caret is pure CSS and is disabled under the same media query. Alternative considered: CSS `steps()` width animation on a monospace span; rejected because it needs the exact character count in CSS per line and breaks on wrapping.

**Accessibility.** The console's scrolling region has `aria-live="polite"` and `aria-atomic="false"`, so a new line is announced once and never interrupts. The caret span is `aria-hidden`. Calls to action are `<a routerLink>` elements styled as buttons (navigation, not actions), with the design system `:focus-visible` ring. The hero's decorative SVG is `aria-hidden`. Headings follow a single `h1` then `h2` per section. The axe check runs in Vitest through `axe-core` on the rendered `WelcomePage` fixture with the colour-contrast rule enabled; jsdom cannot compute real contrast from CSS custom properties, so the contrast scenario is additionally verified by a throwaway Node script over the token pairs the page uses (text on bg, text-strong on surface, text-on-accent on accent, text-on-accent on secondary, text-dim on surface), recorded in the README like the theme change does.

**Tests.** Vitest with fake timers for the scheduler (interval bounds over 1000 draws with a stubbed random, no immediate repeat, first message within 5s, stop clears the timer, ring size), the timestamp formatter (fixed `Date` instances for 10:18:37 and 15:05:02), the console component (reduced-motion token true renders the full line at once), the route table (three lazy routes and the wildcard) and the landing page structure (one `h1`, two links with the exact labels and `href`s, at least four info cards, the five ship names). `axe-core` and `vitest-axe` are added as dev dependencies.

## Risks / Trade-offs

- [`issue-2-material-theme` merges before this change and already created `_tokens.scss`] -> task 1.2 is conditional: check for the file first and only create it when missing; the content is identical by construction (verbatim copy).
- [Component style budget 4kB warning / 8kB error] -> styles are split per component (hero, console, info section) and shared recipes live in the global `_components.scss`; the production build task checks for budget warnings.
- [jsdom has no `matchMedia`] -> reduced motion is read through an injection token whose default implementation guards `typeof matchMedia === 'function'`; tests provide the token explicitly.
- [Timers leak across route changes] -> `stop()` is bound to `DestroyRef.onDestroy` and the scheduler spec asserts no pending timers after destroy with `vi.getTimerCount()`.
- [100 messages in a TypeScript array add to the lazy chunk] -> at roughly 60 bytes each the pool is about 6kB uncompressed in the landing chunk only; acceptable, and it stays out of the initial bundle because the route is lazy.
- [`[ hh:mm:ss AM ]` depends on the `en-US` locale formatting] -> the formatter pins `en-US` and `hour12: true` regardless of the browser locale, so the format is stable; the issue's examples are the contract.

## Open Questions

- Whether the hero should keep the radar motif once the game screens use it heavily. It is decorative and optional in the spec; the implementation includes it, and it can be removed without changing requirements.

## Assumptions carried from the issue

- The console messages are a static list in the frontend; nothing is fetched from a server.
- The console shows a timestamp in the visitor's local time with a 12-hour clock and AM/PM as in the issue's examples.
- The two calls to action point at placeholder routes that render a "coming soon" panel until the game flows are built.
- The information panels contain static copy about the rules of the game; no live data.
- The page depends on `issue-2-material-theme` only for Material components to render in the design system colours; it is built with design system tokens and recipes so it does not block on it.
