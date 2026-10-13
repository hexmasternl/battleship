# Spec Delta

## Purpose

Defines how the Angular Material component library is themed from the Battleship design system, so that Material components and the design system's own page elements share one set of colours and typefaces in the frontend application.

## ADDED Requirements

### Requirement: Material colour roles come from the design system base colours
The frontend application SHALL theme Angular Material so that the Material primary role resolves to the design system primary colour `#00c0ff`, the tertiary role to the secondary colour `#2f87ff`, the error role to the danger colour `#ff2146`, the background and base surface roles to the background colour `#011627`, the on-surface role to the text colour `#b2c2d4` and the on-surface-variant role to the text dim colour `#7a8694`.

#### Scenario: Key Material system variables resolve to base colours
- **WHEN** the computed values of the Material system colour variables for primary, tertiary, error, background, surface, on-surface and on-surface-variant are read on the application root
- **THEN** they equal, respectively, `#00c0ff`, `#2f87ff`, `#ff2146`, `#011627`, `#011627`, `#b2c2d4` and `#7a8694`

#### Scenario: Material components render the base colours
- **WHEN** a filled Material button, an outlined Material form field, a Material card and a Material dialog are rendered
- **THEN** the button background is the primary colour, the focused field outline is the primary colour, the card and dialog surfaces are steps of the design system surface scale, and an invalid field shows the danger colour

### Requirement: Material tonal palettes are built from the design system scales
Every tone of the Material primary, secondary, tertiary, neutral, neutral-variant and error palettes SHALL be a value taken from the design system colour scales (primary, secondary, danger, text and surface) or a documented mix of two such values. No tone SHALL be generated from a seed colour or taken from a stock Material palette.

#### Scenario: Palette tones trace to design system tokens
- **WHEN** a tone of any Material palette used by the theme is inspected in the theme source
- **THEN** the source names the design system token (or the two tokens and the mix ratio) that produced it

#### Scenario: Elevated Material surfaces follow the surface scale
- **WHEN** the Material surface-container roles (lowest, low, default, high, highest) are read
- **THEN** each resolves to a step of the design system surface scale, and they are ordered from darkest to lightest

### Requirement: The Material theme is dark only
The frontend application SHALL apply a single dark Material theme with the `dark` colour scheme declared on the document, and SHALL NOT emit a light variant or switch themes based on the user's system preference.

#### Scenario: System preference does not change the theme
- **WHEN** the application is viewed with the operating system set to a light colour scheme
- **THEN** the application background is still `#011627`, Material components render their dark variants, and native form controls and scrollbars use the dark colour scheme

### Requirement: Material typography uses the design system typefaces
The Material type scale SHALL use Quantico as the brand family for display, headline and large title levels and Courier Prime as the plain family for body, label and the remaining title levels, each with the fallback stack documented in the design system. Roboto SHALL NOT be loaded or referenced. Bitcount Single at weight 200 SHALL remain available through the design system panel font token and SHALL NOT be assigned to any Material type level.

#### Scenario: Headline and body levels use the prescribed families
- **WHEN** the Material system variables for headline-large and body-medium are read
- **THEN** the headline-large font family begins with Quantico and the body-medium font family begins with Courier Prime, each followed by the documented fallbacks

#### Scenario: Web fonts are loaded for all three typefaces
- **WHEN** the application document is loaded with network access
- **THEN** it requests Quantico, Courier Prime and Bitcount Single at weight 200 from the font service, and does not request Roboto

#### Scenario: Fallbacks keep the application readable
- **WHEN** web fonts are blocked
- **THEN** headings render in the documented Quantico fallback, body text in the documented Courier Prime fallback, and no text becomes invisible or overlaps

### Requirement: Design system tokens are available in the application
Every `--bs-*` custom property defined in `docs/design-system.md` SHALL be defined on the application root with the same value, so application components can use design system tokens directly and the Material theme can reference them.

#### Scenario: Frontend token set equals the reference document
- **WHEN** the set of `--bs-*` token names and values in the frontend styles is compared with the set in `docs/design-system.md`
- **THEN** there is no difference in names or values

#### Scenario: A component uses a token without importing the theme
- **WHEN** a component style references a design system token such as the primary glow by name
- **THEN** it resolves to the documented value anywhere in the application

### Requirement: Themed application meets contrast and build constraints
The themed application SHALL build without errors within the configured style budgets. Text in the on-surface and on-surface-variant roles on the background and surface roles, and on-primary text on the primary role, SHALL meet WCAG AA contrast (4.5:1 for body text, 3:1 for large text).

#### Scenario: Production build succeeds
- **WHEN** the application is built in the production configuration
- **THEN** the build completes without error and without a budget error for the global styles

#### Scenario: Contrast of text roles
- **WHEN** the contrast ratio is measured for on-surface on surface, on-surface-variant on surface, on-surface on surface-container-highest, and on-primary on primary
- **THEN** each ratio meets the WCAG AA threshold for the text size it is used at
