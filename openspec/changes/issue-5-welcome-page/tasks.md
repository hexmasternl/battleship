# Tasks

## 1. Routing shell and design system foundation

- [x] 1.1 Replace `src/Frontend/src/app/app.html` with `<router-outlet />`, remove the `title` signal from `app.ts` and the inline placeholder styles, rewrite `app.spec.ts` to assert the outlet renders, and verify `ng test` passes with the "Hello, battleship" test gone
- [x] 1.2 If `src/Frontend/src/styles/_tokens.scss` does not exist yet, create it with the `--bs-*` token block copied verbatim from `docs/design-system.md` onto `:root` and `@use` it from `src/styles.scss`; verify with the token-diff command from design system section 11.3 (adapted to the scss file) that the token name sets are identical
- [x] 1.3 Create `src/Frontend/src/styles/_components.scss` with the `.bs-btn` (primary, secondary), `.bs-card`, `.bs-panel` and `.bs-panel--info` recipes from design system sections 7.1 and 7.3 including hover, active, `:focus-visible` ring and the reduced-motion transition rule, `@use` it from `styles.scss`, and verify `ng build --configuration development` compiles without Sass errors
- [x] 1.4 Add the three lazy routes (`''` to the welcome page, `games/new` and `games/join` to the coming-soon placeholder with `data.feature`) and a wildcard redirect in `app.routes.ts`, write a routes spec that asserts the paths, `loadComponent` presence and the redirect, and verify it passes

## 2. Coming-soon placeholder

- [x] 2.1 Create `src/Frontend/src/app/games/coming-soon.ts` rendering a `.bs-panel--info` that names the feature from route data ("Create a new game" or "Join an existing game") and a ghost link back to `/`, with a spec that renders both variants, and verify the spec passes

## 3. Bridge console

- [x] 3.1 Create `src/Frontend/src/app/welcome/console-messages.ts` exporting `CONSOLE_MESSAGES` with at least 100 distinct marine-themed messages (sonar, radar, torpedo tubes, bearings, GPS coordinates with four decimals, weather, crew chatter) and a spec asserting length >= 100 and no duplicates after trimming; verify it passes
- [x] 3.2 Create `console-timestamp.ts` with `formatConsoleTimestamp(date)` producing `[ hh:mm:ss AM ] - ` using `en-US` and `hour12: true`, with a spec for 10:18:37 (`[ 10:18:37 AM ] - `) and 15:05:02 (`[ 03:05:02 PM ] - `); verify it passes
- [x] 3.3 Create `console-scheduler.ts` (injection tokens `CONSOLE_RANDOM`, `CONSOLE_CLOCK`, `REDUCED_MOTION`; `start()`, `stop()`, `lines` signal, per-line `typedLength`, ring of 12, first delay 0 to 5s, next delay 5 to 15s inclusive, no immediate repeat, 30ms typing step, full line at once under reduced motion) and a spec using Vitest fake timers that covers interval bounds over 1000 draws, no immediate repeat, first line within 5s, ring size, typing progression, reduced-motion short circuit and zero pending timers after `stop()`; verify it passes
- [x] 3.4 Create `bridge-console.ts` rendering the lines from the scheduler inside a `.bs-panel` styled as a terminal (body typeface, `--bs-color-accent` text with `--bs-glow-text-primary`, CSS caret, `aria-live="polite"`, overflow hidden, fixed max height), starting the scheduler on init and stopping it through `DestroyRef`, with a spec that renders one line with `REDUCED_MOTION` true and asserts the full text and the live region; verify it passes

## 4. Landing page

- [x] 4.1 Create `welcome/hero-section.ts` with the full-viewport hero (layered gradient and sonar-ring background from design system tokens, optional `aria-hidden` radar SVG that stops under reduced motion), the two-column jumbotron grid (stacked below 1024px), the welcome `h1`, intro paragraph and the two `routerLink` calls to action with the exact labels and `.bs-btn--primary` / `.bs-btn--secondary` classes, and the `<battle-bridge-console>`; verify a spec asserts the `h1`, both links with `href="/games/new"` and `href="/games/join"`, and the console element
- [x] 4.2 Create `welcome/info-section.ts` with at least four `.bs-card` panels ("How a game works", "The fleet" listing Carrier 5, Battleship 4, Destroyer 3, Submarine 3, Patrol Boat 2 with proportional ship bars, "The grid" describing 12x12 with A to L and 1 to 12, "Turns and hits" showing the miss, hit and sunk markers) in a grid of one column at 360px, two at 768px and four at 1280px; verify a spec asserts the four headings and the five ship names
- [ ] 4.3 Create `welcome/welcome-page.ts` composing the hero and info sections as the `''` route component, run `ng serve`, and verify in a browser at 360px, 768px, 1024px and 1920px that the hero fills the viewport, the jumbotron stacks below 1024px and sits side by side above, no horizontal scrolling occurs, console lines type in with timestamps and scroll up, and both calls to action navigate to the placeholder routes by click and by keyboard
- [ ] 4.4 Emulate `prefers-reduced-motion: reduce` in devtools and verify console lines appear whole, the caret does not blink and the radar motif is static

## 5. Accessibility and verification

- [ ] 5.1 Add `axe-core` and `vitest-axe` as dev dependencies, write `welcome-page.spec.ts` that runs axe on the rendered landing page and asserts no violations, and verify it passes
- [ ] 5.2 Compute WCAG contrast with a throwaway Node script for the token pairs the page uses (text on bg, text-strong on surface, text on surface-raised, text-dim on surface, text-on-accent on accent, text-on-accent on secondary, accent on surface for console text), verify each meets 4.5:1 (or 3:1 for large headings only), and record the command and results in a "Welcome page" section of `src/Frontend/README.md`
- [ ] 5.3 Run `ng build` in the production configuration and `ng test`, verify both succeed with no budget error or warning, and verify in the build output that the welcome page is emitted as a separate lazy chunk
