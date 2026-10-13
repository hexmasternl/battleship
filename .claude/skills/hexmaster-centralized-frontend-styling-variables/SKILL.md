---
name: hexmaster-centralized-frontend-styling-variables
description: Applies when writing or reviewing any SCSS or CSS in the Angular frontend, styling a component, adding a colour, font, size, spacing, radius, shadow or transition, or setting up theming and dark mode. Requires every such value to come from one central SASS variables file.
metadata:
  source: hexmaster-coding-standards
  document: centralized-frontend-styling-variables
  status: accepted
---

# HexMaster ADR 0006: Centralized frontend styling variables

## Rules

- All colours, font families, font weights, font sizes, line heights, spacing, border radii,
  border widths, shadows and transitions are declared as named tokens in one central
  `_variables.scss` (or equivalent CSS custom properties file).
- Token groups to provide: brand colours (`$color-primary`, `-light`, `-dark`, secondary,
  accent), semantic colours (success, warning, error, info), neutrals (text primary/secondary/
  disabled, background, background-alt, border), typography (families, a size scale from
  `$font-size-xs` to `-4xl`, weights, line heights), spacing on an 8px base (`$spacing-xs` to
  `-3xl`), radii, shadows, transitions.
- Never hardcode a hex/rgb colour, font name or size value in a component style. Always use
  the token. Pull requests that introduce hardcoded values are rejected in review.
- Angular: expose the file through `stylePreprocessorOptions.includePaths` in `angular.json`
  or import it from `styles.scss`, and consume it in component SCSS with
  `@use 'variables' as *;`.
- Theming and dark mode: define per-theme overrides (`_variables-dark.scss`) and map tokens to
  CSS custom properties under `[data-theme='light']` / `[data-theme='dark']` so themes switch at
  runtime without touching components.
- Design system updates land in the variables file before any component uses them. Configure
  stylelint to flag hardcoded colours and fonts where possible.

## In this repository

The Angular app in `src/Frontend/` uses Angular Material 3 theming in `src/styles.scss` and the
`--mat-sys-*` system variables. Component styles consume either those Material system variables
or tokens from the central variables file; any value Material does not define (brand colours,
spacing scale, radii, shadows, transitions) must become a token there rather than a literal in a
component. Frontend work goes through the `angular-frontend` agent.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `centralized-frontend-styling-variables`.
