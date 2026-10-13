# Proposal

> Tracks GitHub issue [hexmasternl/battleship#1](https://github.com/hexmasternl/battleship/issues/1).

## Why

Battleship has no visual language: `src/Frontend/` is empty, no framework has been chosen, and there are no shared colours, type or component rules. Fixing the design system now, before the first screen is built, means every frontend screen is assembled from the same tokens and the marine/battleship identity (radar, sonar, compass, engine RPM) is designed once instead of improvised per page.

## What Changes

- Add `docs/design-system.md`: the written design system reference covering the six base colours verbatim (background `#011627`, primary `#00c0ff`, secondary `#2f87ff`, text `#b2c2d4`, text dim `#7a8694`, danger `#ff2146`), a tint/shade scale derived from each, semantic colour roles, typography, spacing and radius scales, glow and shadow effects, motion rules, breakpoints, and every game page element.
- Define every colour, scale and effect as a named CSS custom property (design token) so the frontend can adopt the system regardless of the framework chosen later.
- Add `docs/design-system.html`: a self-contained static showcase page (no build step, no framework) that renders every token and element: palette and tints, typography, buttons, inputs, cards and panels, status and danger states, the 12x12 playing grid, the five ships in both orientations, battle-stage cell states, and the radar sweep, sonar ping, compass and engine RPM gauge motifs.
- Make the showcase mobile first and responsive (usable at 360px wide without horizontal scrolling), animate the dynamic motifs with CSS only, and honour `prefers-reduced-motion`.
- Verify and record WCAG AA contrast ratios for text on the background, panel and primary surfaces, and verify that the markdown and the HTML name exactly the same set of tokens.

## Capabilities

### New Capabilities
- `design-system`: the visual contract of the Battleship frontend: design tokens (colour, type, space, radius, effects, motion, breakpoints), the game-specific page elements, their accessibility and responsiveness rules, and the reference document and showcase page that publish them.

### Modified Capabilities
<!-- none: there are no existing specs -->

## Impact

- New files only: `docs/design-system.md` and `docs/design-system.html` at the repository root.
- No code under `src/` changes. The Games, Players and Aspire projects are untouched. `src/Frontend/` stays empty; choosing and scaffolding a framework is a later change that consumes these tokens.
- No new dependencies or build steps. Web fonts are loaded from a public font service with documented system fallbacks, so the page also renders offline.
- Assumptions recorded from the issue triage: five ships per player (the report once says three but lists five types), a dark-only theme, a plain HTML/CSS showcase, and fonts chosen by the design system since the report does not prescribe any.
