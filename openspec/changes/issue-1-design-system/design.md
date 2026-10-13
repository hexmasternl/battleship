# Design

## Context

See proposal.md for motivation. The repository has no frontend code, no `docs/` directory, no shared styles and no chosen framework. The showcase must therefore be a plain static page, and the tokens must live in a form any later stack (Angular, React, plain web components) can consume unchanged. The team may adopt a component library later (a PrimeNG documentation server is wired into this workspace), so the tokens must be mappable onto a third-party theme without rewriting them.

## Goals / Non-Goals

**Goals:**
- One authoritative token block, embedded in the showcase and reproduced verbatim in the reference document, with a mechanical check that the two agree.
- Deterministic colour values: every tint, shade and surface step is a precomputed hex value listed in the document, so contrast can be measured and the values do not depend on browser colour-mixing support.
- Motifs and animations in CSS and inline SVG only, no JavaScript, so the page is a faithful reference for what CSS alone can deliver.

**Non-Goals:**
- Component source for any framework, a published npm package, a Sass pipeline, or a light theme. Those are follow-up changes that consume this one.
- Exhaustive component coverage (tables, modals, navigation). This change covers the primitives and the game elements; more components are added when screens need them.

## Decisions

**Token naming: `--bs-<category>-<name>[-<step>]`.** Categories are `color`, `font`, `text` (sizes), `leading`, `space`, `radius`, `shadow`, `glow`, `duration`, `ease`, `bp`. Examples: `--bs-color-primary-500`, `--bs-color-surface-2`, `--bs-space-4`, `--bs-glow-primary`. Semantic roles sit under `color` with role names (`--bs-color-bg`, `--bs-color-text`, `--bs-color-accent`, `--bs-color-hit`) and reference scale tokens with `var()`. Alternative considered: unprefixed names (`--primary`), rejected because they collide with any component library's own variables.

**Colour scales are precomputed in OKLCH, published as hex.** Each of primary, secondary, danger and text gets steps 50, 100, 200, 300, 400, 500, 600, 700, 800, 900 with the base colour at 500 (text at 300 since it is already light). Lighter steps mix toward white, darker steps toward the background colour, in OKLCH so hue stays stable and glow colours look like the same ink. The background gets `surface-0` (the base) through `surface-4`, each a small OKLCH lightness step up. Values are computed once during implementation with a throwaway script in the scratchpad and pasted as hex into both files. Alternative considered: runtime `color-mix()`, rejected because the reference document could not list concrete values and contrast could not be measured from the document alone.

**Semantic roles reference scale tokens, never hex.** This keeps a future light theme or high-contrast mode to a swap of role assignments.

**Typography.** Three typefaces, all prescribed by the issue reporter and loaded from Google Fonts with `font-display: swap`:
- Heading: `Quantico` (https://fonts.google.com/specimen/Quantico), token `--bs-font-heading`, fallback `"Segoe UI", system-ui, sans-serif`. Used for all headings, the top bar, button labels and the compass rose letters.
- Default: `Courier Prime` (https://fonts.google.com/specimen/Courier+Prime), token `--bs-font-body`, fallback `"Courier New", Courier, monospace`. Used for body copy, inputs, grid coordinates and instrument readouts; because it is monospaced there is no separate instrument face.
- Panel: `Bitcount Single` at weight 200 (https://fonts.google.com/specimen/Bitcount+Single), token `--bs-font-panel` with `--bs-font-weight-panel: 200`. Reserved for informational panels that set the stage (intro banner, status strips, mission text), never for body text or controls. Fallback `"Courier New", Courier, monospace`. Only weight 200 is loaded.

The page must stay correct on fallbacks. Type scale uses a 1.25 ratio from a 16px base, with `clamp()` on the two largest sizes for small screens. Line heights are tuned per face: Courier Prime at 1.6 for body, Quantico at 1.15 for headings, Bitcount Single at 1.3.

**Spacing and radius.** Spacing on a 4px base: steps 1 through 12 (4px to 64px, non-linear above 8). Radius: `sm` 4px, `md` 8px, `lg` 16px, `full` 999px. Breakpoints: `sm` 480px, `md` 768px, `lg` 1024px, `xl` 1280px, all `min-width`, written in the document as tokens and in CSS as literal media queries (custom properties cannot be used in media queries; the document states this).

**Effects.** Shadows `--bs-shadow-1..3` are layered dark shadows for elevation on the navy background. Glows `--bs-glow-primary`, `--bs-glow-secondary`, `--bs-glow-danger` are two-layer `box-shadow` values (tight inner, soft outer) using the 400 and 500 steps at reduced alpha. Text glow uses `text-shadow` variants such as `--bs-glow-text-primary`. The document maps each component state to its effect.

**Motion.** Durations `fast` 120ms, `base` 200ms, `slow` 400ms, `sweep` 4s, `ping` 2.4s. Easings `standard`, `emphasised`, `linear`. All loops are `@keyframes` on inline SVG or pseudo-elements. A single `@media (prefers-reduced-motion: reduce)` block sets `animation: none` on the three animated motifs and shortens transitions.

**Game elements.** The grid is a CSS grid of 13 columns and 13 rows (one header row and column for coordinates), each cell `aspect-ratio: 1`, sized so the whole board fits 360px minus gutters. Ships are elements spanning N cells with `grid-column: span N` or `grid-row: span N`, rounded ends, a hull gradient from secondary-600 to secondary-800 and a primary glow when valid, danger glow when invalid. Cell states: unknown (surface-1 with a faint grid line), miss (a small dim ring), hit (danger-500 fill with a cross glyph), sunk (danger-700 fill with a darker hatch pattern). Each state has a non-colour cue to satisfy the spec.

**Motifs.** Radar: inline SVG circle with range rings, a conic-gradient sweep on a rotating group, and two blip dots. Sonar: concentric expanding rings scaled from the centre with fading opacity. Compass: inline SVG rose with cardinal letters in the display face and a needle. RPM gauge: a 240-degree arc with ticks, a needle that oscillates slowly, and a mono readout. All colours come from role tokens.

**Showcase structure.** One `<main>` with sections in this order: Introduction, Colour (bases, scales, roles, contrast table), Typography, Spacing and radius, Effects, Motion, Components, Game elements, Instruments, Responsive notes. A sticky top bar with in-page anchors becomes a horizontally scrolling strip under `md`. Every section opens with the token names it demonstrates so the document-to-page check is visual as well as mechanical.

**Verification tooling.** Contrast ratios are computed with a throwaway Node script in the scratchpad (Node is present because the OpenSpec CLI runs on it) and pasted into the document's contrast table. The token agreement check is a shell one-liner: extract `--bs-[a-z0-9-]+` names from both files, sort unique, and `diff`. Both commands are recorded in the document's "Maintaining this document" section so they can be re-run.

## Risks / Trade-offs

- [Web font service is unreachable or blocked] -> fallback stacks are chosen to be metrically close and the page is checked once with fonts disabled.
- [Precomputed hex drifts from the formula if someone edits one value by hand] -> the document records the generation formula and the script; the contrast table is regenerated whenever a value changes.
- [Animations cost battery on phones] -> loops run only on the three motifs, use `transform` and `opacity` only, and stop under reduced motion.
- [A future component library's theme contract uses different variable names] -> semantic roles are the integration surface; a theme adapter maps library variables onto `--bs-*` roles rather than the reverse.
- [Grid at 360px leaves cells around 24px, small for touch] -> the document states the minimum tap target rule (44px) applies to the game screens, which will enlarge cells by scrolling or zooming the board; the showcase only demonstrates visual states.
