# Proposal

> Tracks GitHub issue [hexmasternl/battleship#2](https://github.com/hexmasternl/battleship/issues/2).

## Why

The Angular frontend under `src/Frontend/` ships with the Angular Material generator defaults: a light Material 3 theme built from `mat.$azure-palette` and `mat.$blue-palette`, Roboto typography and a Roboto web font. None of the Battleship design system defined by `issue-1-design-system` (deep navy background, cyan primary, blue secondary, Quantico and Courier Prime) reaches Material components, so the first real screen would mix stock Material controls with design system game elements. Theming Material now, before any screen is built, makes every Material button, field, card and dialog part of the same visual language as the grid, ships and instruments.

## What Changes

- Add the design system token block (`--bs-*` custom properties from `docs/design-system.md`) to the Angular global styles so every component in the application can use the tokens, and extend the design system's token-diff check so the frontend copy cannot drift from the reference document.
- Replace the default `mat.theme()` call in `src/Frontend/src/styles.scss` with a custom, dark-only Material 3 theme whose primary, tertiary, neutral, neutral-variant and error tonal palettes are hand-authored from the design system colour scales, so the primary `#00c0ff`, secondary `#2f87ff` and danger `#ff2146` appear verbatim in Material components and the application background resolves to `#011627`.
- Pin the Material system colour roles that carry the page (surface family, background, on-surface, on-surface-variant, outline) to the design system's semantic role tokens through `mat.theme-overrides()`, so a later re-assignment of a design system role flows into Material without touching the palettes.
- Set Material typography to Quantico as the brand family (display, headline and large title levels) and Courier Prime as the plain family (body, label and medium title levels), with the design system's fallback stacks, and keep Bitcount Single at weight 200 as the design system panel font token outside the Material type scale.
- Replace the Roboto web font link in `src/Frontend/src/index.html` with Quantico, Courier Prime and Bitcount Single (weight 200 only) from Google Fonts; keep Material Symbols Outlined for icons.
- Switch the application to `color-scheme: dark` so native form controls, scrollbars and the user agent default background match the theme.
- Use a temporary theme preview in the root component for visual verification only; no game screens are built.

## Capabilities

### New Capabilities
- `material-theme`: how the Angular Material component library is themed from the Battleship design system: which design system values drive the Material colour roles and type scale, that the application is dark only, that the frontend carries the design system tokens, and that the frontend token copy agrees with the reference document.

### Modified Capabilities
<!-- none: design-system is still a pending change (issue-1-design-system); its requirements are consumed, not changed -->

## Impact

- `src/Frontend/src/styles.scss`: the theme definition changes completely.
- New Sass partials under `src/Frontend/src/styles/` for the design system tokens and the Material palettes; `angular.json` keeps its single `src/styles.scss` entry.
- `src/Frontend/src/index.html`: web font links change; Roboto is removed.
- `src/Frontend/README.md`: gains a theming section with the verification commands.
- Depends on `issue-1-design-system` being merged into `development` first: `docs/design-system.md` is the source of the token values and the font fallback stacks. The implementation reads the scale values from that document; nothing in this change redefines them.
- No new npm dependencies. `@angular/material` 22.2.2 already provides `mat.theme()` and `mat.theme-overrides()`.
- No backend change; the Games, Players and Aspire projects are untouched. No light theme and no theme switching.
