# Design

## Context

See proposal.md for motivation. Observed state of `src/Frontend/`: Angular 22 with `@angular/material` 22.2.2, SCSS, a single global style entry `src/styles.scss` that includes `mat.theme()` with `mat.$azure-palette` / `mat.$blue-palette`, `typography: Roboto`, `density: 0`, and `color-scheme: light` on `body`. `index.html` loads Roboto 300/400/500 and Material Symbols Outlined. Budgets: initial bundle 500kB warning / 1MB error, `anyComponentStyle` 4kB / 8kB.

Angular Material 22 theming, as inspected in `node_modules/@angular/material/core/tokens/_system.scss` and `core/tokens/m3/_md-sys-color.scss`:
- `mat.theme((color: (theme-type: dark, primary: <palette>, tertiary: <palette>), typography: (...), density: 0))` emits `--mat-sys-*` variables. A palette is a Sass map of tones `0, 10, 20, 25, 30, 35, 40, 50, 60, 70, 80, 90, 95, 98, 99, 100` with nested `secondary`, `neutral`, `neutral-variant` and `error` maps of the same shape. The nested maps are read from the primary palette only.
- In the dark theme Material picks: `primary` tone 80, `on-primary` 20, `primary-container` 30, `on-primary-container` 90 (same pattern for secondary, tertiary and error); `surface` and `background` neutral 6, `surface-container-lowest` 4, `-low` 10, `-container` 12, `-high` 17, `-highest` 22, `surface-bright` 24; `on-surface` neutral 90; `on-surface-variant` neutral-variant 90, `outline` neutral-variant 60, `outline-variant` neutral-variant 30. Elevation shadows use neutral 0.
- `mat.theme-overrides((<role>: <value>))` emits `--mat-sys-<role>` with any CSS value, including `var()`.
- Typography: `brand-family` feeds display, headline and title-large; `plain-family` feeds body, label, title-medium and title-small. Families are interpolated into `font` shorthands as given, so a fallback stack is passed as a Sass list.

The design system (`issue-1-design-system`, `docs/design-system.md`) defines `--bs-color-<scale>-<step>` scales at steps 50 to 900 with the base at 500 (text at 300), `--bs-color-surface-0..4` with surface-0 equal to the background, semantic roles such as `--bs-color-bg`, `--bs-color-surface`, `--bs-color-border`, `--bs-color-text`, `--bs-color-text-dim`, and font tokens `--bs-font-heading`, `--bs-font-body`, `--bs-font-panel` with documented fallback stacks. All values are precomputed hex.

## Goals / Non-Goals

**Goals:**
- One source of truth for colours and fonts: the design system document. The theme translates it and never invents a value.
- A mechanical check that the frontend token copy equals the reference document, reusing the token-diff approach the design system documents.
- Material roles that carry the page (surfaces, text, outlines) follow design system semantic roles, so a role re-assignment in the design system does not require re-authoring palettes.

**Non-Goals:**
- A light theme, `light-dark()` output, or runtime theme switching.
- Per-component Material overrides (`mat.button-overrides` and friends) beyond what the four verification components need. Component-level polish happens when screens are built.
- A shared theme package or a second copy of the tokens for other frontends.

## Decisions

**Files.** `src/Frontend/src/styles/_tokens.scss` holds the verbatim `--bs-*` block on `:root`, copied from `docs/design-system.md`. `src/Frontend/src/styles/_palettes.scss` holds the Material palette maps and the tone-to-token table as comments. `src/Frontend/src/styles.scss` stays the only `angular.json` entry: it does `@use` of both partials plus `@angular/material`, the `mat.theme()` call, `mat.theme-overrides()` and the `body` defaults. Alternative considered: `ng generate @angular/material:theme-color` with the base colours as seeds; rejected because the schematic derives every tone with Material's own tonal algorithm, so the base colours would not appear verbatim and the surfaces would be neutral greys rather than the navy surface scale.

**Hand-authored palettes from the design system scales.** Each Material palette is a Sass map whose tones are design system hex values, chosen so that the tone Material reads in the dark theme lands on the intended token. Tone-to-step table for the chromatic palettes (primary from the primary scale, tertiary from the secondary scale, error from the danger scale; the nested `secondary` map of the primary palette also uses the secondary scale so Material's secondary role matches tertiary):

| Material tone | Design system step | Dark-theme role it feeds |
|---|---|---|
| 100 | `#ffffff` | none |
| 99, 98 | step 50 | none |
| 95 | step 100 | none |
| 90 | step 300 | on-primary-container, primary-fixed |
| 80 | step 500 (base) | primary / tertiary / error |
| 70 | step 600 | none |
| 60 | step 700 | none |
| 50 | step 800 | none |
| 40 | mix(step 800, step 900, 50%) | inverse-primary |
| 35, 30 | step 900 | primary-container |
| 25, 20 | mix(step 900, surface-0, 50%) | on-primary |
| 10 | mix(step 900, surface-0, 80%) | on-primary-fixed |
| 0 | mix(surface-0, black, 50%) | none |

Mixes are computed with `color.mix()` in Oklab at build time (not OKLCH: an OKLCH mix of the danger shade and the navy background follows the shorter hue arc through purple, while Oklab interpolates in a straight perceptual line), and the token names and ratios are kept as comments next to each tone so the traceability scenario in the spec is satisfied by reading the source. The neutral palette uses the surface scale: tone 4 = surface-0 (the spec requires surface-container-lowest, which reads tone 4, to be a step of the surface scale, so it shares the background rather than a darker mix), 6 = surface-0, 10 = surface-1, 12 = surface-2, 17 = surface-3, 22 and 24 = surface-4, 90 = text (`--bs-color-text-300`), 0 = black, 100 = white, remaining tones interpolated between their neighbours. The neutral-variant palette reuses the neutral tones except 90 = text dim (`#7a8694`), 60 = the border role's step and 30 = a darker border step, so `on-surface-variant`, `outline` and `outline-variant` are design system values. Alternative considered: `mat.theme-overrides` alone on top of a stock palette; rejected because dozens of container, fixed and inverse roles would still show stock colours in menus, chips and sliders.

**Semantic roles pinned through `theme-overrides`.** After `mat.theme()`, `mat.theme-overrides()` sets `background`, `surface`, `surface-dim`, `surface-container-lowest` through `surface-container-highest`, `surface-bright`, `on-surface`, `on-surface-variant`, `outline` and `outline-variant` to `var(--bs-color-...)` semantic role tokens (bg, surface, surface raised, text, text dim, border). The palettes already produce the same hex, so this changes nothing visually today, but it makes the dependency on the design system roles explicit and future role changes automatic. `primary`, `tertiary` and `error` stay on the palettes, because Material derives their container and fixed variants from the same ladder and a `var()` there would not reach those.

**Theme type `dark`, not `color-scheme`.** `theme-type: dark` emits plain hex values rather than `light-dark()` pairs, which keeps the CSS smaller and makes computed values directly comparable in checks. `body` gets `color-scheme: dark` so native controls and scrollbars follow. Alternative considered: `theme-type: color-scheme` with `color-scheme: dark` forced on `html`; rejected because it still emits an unused light half for every role.

**Typography.** `brand-family: (Quantico, 'Segoe UI', system-ui, sans-serif)` and `plain-family: ('Courier Prime', 'Courier New', Courier, monospace)`, weights `regular-weight: 400`, `medium-weight: 700`, `bold-weight: 700` because Quantico and Courier Prime ship only 400 and 700. The exact fallback stacks are copied from `--bs-font-heading` and `--bs-font-body` in the design system. Bitcount Single gets no Material level; `--bs-font-panel` and `--bs-font-weight-panel` from the token block are the only way to use it. `body` keeps `font: var(--mat-sys-body-medium)` so the application default is Courier Prime through Material. Headings are not globally styled here; components choose a Material level or `--bs-font-heading`.

**Web fonts.** `index.html` loads `https://fonts.googleapis.com/css2?family=Quantico:wght@400;700&family=Courier+Prime:ital,wght@0,400;0,700;1,400&family=Bitcount+Single:wght@200&display=swap` and keeps the preconnect links and Material Symbols. The Roboto link is deleted.

**Verification tooling.** Two throwaway Node scripts in the scratchpad during implementation: one extracts `--bs-*` name/value pairs from `docs/design-system.md` and `_tokens.scss` and diffs them; the other computes WCAG contrast for the role pairs the spec lists from the compiled CSS of `ng build`. Both commands are recorded in a "Theming" section of `src/Frontend/README.md` so they can be re-run. Visual verification uses `ng serve` with a temporary theme preview rendered in the root component (a filled button, an outlined form field in valid and invalid state, a card, a dialog trigger). The preview is removed before the pull request unless the user asks to keep it as a dev-only route.

## Risks / Trade-offs

- [`issue-1-design-system` changes token names or steps after this design is written] -> the palettes reference tokens by name in the tone table; the token-diff task fails loudly, and the table is updated in the same session.
- [Material reads tones this table does not pin, for example tone 70 for some hover states] -> every tone is filled with a design system step, so unpinned tones still come from the scale; the visual check covers hover and focus on the four components.
- [Quantico and Courier Prime only have 400 and 700, so Material's default medium weight 500 would be synthesised] -> `medium-weight: 700` is set explicitly.
- [`var()` in `theme-overrides` prevents Sass from computing derived colours] -> overrides are limited to roles Material emits verbatim; chromatic roles stay on hex palettes.
- [The `anyComponentStyle` budget does not cover global styles, but the initial bundle budget does] -> the theme adds a few kB of CSS; the production build task checks the budget output.

## Open Questions

- Whether to keep the theme preview as a permanent dev-only route after verification. It does not change the specs or tasks; the final task removes it unless the user asks to keep it.
