# Spec Delta

## ADDED Requirements

### Requirement: The landing page shows help tiles that deep-link into the help section
Below the hero, the landing page SHALL show a help tile section. It SHALL have a heading and at least four tiles. Each tile SHALL show:
- an icon or illustration
- a title
- a one-sentence teaser

Each tile SHALL link to a distinct help topic route under `/help/`. The section SHALL also link to the help overview at `/help`. The tiles SHALL be laid out in a responsive grid and SHALL be styled distinctly from the game information panels.

#### Scenario: Tiles present
- **WHEN** the landing page renders
- **THEN** the help tile section shows a heading, at least four tiles each with a title and teaser, and a link to `/help`

#### Scenario: Tile deep-links to its topic
- **WHEN** the visitor activates the tile for custom resource commands with a pointer or the keyboard
- **THEN** the router navigates to `/help/resource-commands` without requiring a user session

#### Scenario: Tiles adapt to width
- **WHEN** the viewport is 360px wide
- **THEN** the tiles stack in a single column without horizontal scrolling, and at 768px or wider they are arranged in two or more columns

### Requirement: Help tiles are accessible and motion-safe
Each help tile SHALL be a single focusable link with an accessible name equal to its title. Each tile SHALL show the design system focus ring. Any hover or decorative animation on the tiles SHALL be disabled when the visitor requests reduced motion.

#### Scenario: Keyboard reachable
- **WHEN** the visitor tabs through the help tile section
- **THEN** each tile receives focus once, in document order, with a visible focus indicator, and its accessible name is its title

#### Scenario: Reduced motion
- **WHEN** `prefers-reduced-motion: reduce` is active
- **THEN** the help tiles show no animation on hover, focus or load
