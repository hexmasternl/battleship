# design-system Specification

## Purpose
Defines the visual contract of the Battleship frontend: the design tokens, the game-specific page elements, their accessibility and responsiveness rules, and the reference document and showcase page that publish them.

## Requirements

### Requirement: Base colours are used verbatim
The design system SHALL define exactly these base colours, unchanged: background `#011627`, primary `#00c0ff`, secondary `#2f87ff`, text `#b2c2d4`, text dim `#7a8694` and danger `#ff2146`. Every other colour in the system SHALL be derived from one of these six.

#### Scenario: Base colour tokens resolve to the given values
- **WHEN** the base colour tokens of the design system are read
- **THEN** each of the six resolves to the hex value listed above, byte for byte

#### Scenario: Derived colours trace back to a base colour
- **WHEN** any non-base colour token is inspected in the reference document
- **THEN** the document names the base colour and the formula or mix it was derived from

### Requirement: Each base colour has a tint and shade scale
The design system SHALL provide, for the primary, secondary, danger and text base colours, an ordered scale of at least seven steps from lightest tint to darkest shade, with the base colour occupying one named step, and for the background colour a scale of elevated surface steps.

#### Scenario: Scale is ordered and contains the base
- **WHEN** the scale of a base colour is read in order
- **THEN** perceived lightness decreases monotonically from the first to the last step and one step equals the base colour exactly

#### Scenario: Surfaces elevate from the background
- **WHEN** the background surface scale is read
- **THEN** each step is lighter than the previous one and the first step equals the background colour

### Requirement: Semantic colour roles map to tokens
The design system SHALL define semantic roles (page background, surface, surface raised, border, text, text dim, accent, accent hover, accent active, focus ring, danger, danger surface, success, warning, miss, hit, sunk) and each role SHALL reference a scale step rather than a literal hex value.

#### Scenario: Role resolves through a scale token
- **WHEN** a semantic role token is read
- **THEN** its value is a reference to a scale or base token, not a literal colour

### Requirement: Every token is a named CSS custom property
Every colour, typography, spacing, radius, shadow, glow, motion and breakpoint value of the design system SHALL be published as a CSS custom property with a documented name under a single prefix, so a frontend of any framework can consume it without translation.

#### Scenario: Token names share one prefix
- **WHEN** all custom property names in the reference document are collected
- **THEN** every name starts with the documented prefix and follows the documented naming pattern

#### Scenario: Frontend consumes a token without the showcase
- **WHEN** a stylesheet includes only the token block and references a token by name
- **THEN** the token resolves to its documented value without any other part of the showcase present

### Requirement: Typography, spacing, radius and breakpoint scales are defined
The design system SHALL define three typefaces, each with a system fallback stack: Quantico for headings, Courier Prime as the default body typeface, and Bitcount Single at weight 200 for informational panels only. It SHALL also define a modular type scale of at least six sizes with line heights, a spacing scale of at least eight steps, a radius scale of at least four steps, and mobile-first breakpoints of at least four widths.

#### Scenario: Typefaces are assigned by role
- **WHEN** the showcase page renders with web fonts available
- **THEN** every heading uses Quantico, body text and controls use Courier Prime, and informational panels use Bitcount Single at weight 200 and nothing else does

#### Scenario: Typefaces render with fallbacks
- **WHEN** the web fonts cannot be loaded
- **THEN** the page renders with the documented system fallback for each of the three typefaces and remains readable

#### Scenario: Scales are documented with values
- **WHEN** the typography, spacing, radius or breakpoint section of the reference document is read
- **THEN** every step lists its token name and its concrete value

### Requirement: Glow and shadow effects are tokenised
The design system SHALL define at least three elevation shadows and at least three glow effects (primary, secondary and danger) as tokens, and SHALL document which elements use which effect.

#### Scenario: Effect token applied to an element
- **WHEN** an element documented as glowing is rendered in the showcase
- **THEN** it uses the named glow token and no inline literal shadow value

### Requirement: Motion is defined and respects reduced motion
The design system SHALL define duration and easing tokens, SHALL animate the radar sweep, sonar ping and engine RPM gauge using CSS only, and SHALL stop or reduce all non-essential animation when the user prefers reduced motion.

#### Scenario: Reduced motion preference is honoured
- **WHEN** the operating system reports `prefers-reduced-motion: reduce`
- **THEN** the radar sweep, sonar ping and gauge needle are static and no element loops an animation

#### Scenario: Animations run without script
- **WHEN** the showcase page is opened with JavaScript disabled
- **THEN** the radar sweep, sonar ping and gauge still animate

### Requirement: Game page elements are specified
The design system SHALL specify the 12x12 playing grid with row and column coordinates, the five ships (Carrier 5, Battleship 4, Destroyer 3, Submarine 3, Patrol Boat 2) at one cell wide in horizontal and vertical orientation, the placing-stage states (valid placement, invalid placement, placed), and the battle-stage cell states (unknown, miss, hit, sunk).

#### Scenario: Grid has twelve by twelve cells with coordinates
- **WHEN** the playing grid is rendered in the showcase
- **THEN** it shows 144 cells, column labels A to L and row labels 1 to 12

#### Scenario: Ships render at their documented lengths
- **WHEN** the fleet is rendered in the showcase
- **THEN** each of the five ships spans exactly its documented number of cells and appears once horizontally and once vertically

#### Scenario: Every cell state is distinguishable
- **WHEN** the unknown, miss, hit and sunk cell states are rendered side by side
- **THEN** each state is distinguishable by colour and by a non-colour cue (shape, icon or pattern)

### Requirement: Marine instrument motifs are specified
The design system SHALL specify four instrument motifs, radar sweep, sonar ping, compass and engine RPM gauge, as reusable page elements built from the system's tokens.

#### Scenario: All four motifs appear in the showcase
- **WHEN** the showcase page is opened
- **THEN** a radar, a sonar ping, a compass and an RPM gauge are each rendered using the system's colour and glow tokens

### Requirement: Core interface elements are specified
The design system SHALL specify buttons (primary, secondary, ghost, danger) with default, hover, active, focus and disabled states, text inputs with default, focus, error and disabled states, cards and panels, and status messages (info, success, warning, danger).

#### Scenario: Every element state is rendered
- **WHEN** the components section of the showcase is read
- **THEN** every element above is rendered in every state listed for it

#### Scenario: Focus is visible without colour alone
- **WHEN** a button or input receives keyboard focus
- **THEN** a focus ring that is at least 2px wide and offset from the element is visible

### Requirement: Showcase page is self-contained and static
`docs/design-system.html` SHALL be a single file that renders every token and element documented in `docs/design-system.md` without a build step, bundler or framework, with the token block embedded so the page is its own reference implementation.

#### Scenario: Page opens from the file system
- **WHEN** the HTML file is opened directly in a browser from disk
- **THEN** every section renders without a console error and without a network dependency other than optional web fonts

### Requirement: Showcase is mobile first and responsive
The showcase SHALL lay out for a 360px wide viewport first and progressively enhance at each documented breakpoint, and SHALL never produce horizontal page scrolling between 360px and 1920px.

#### Scenario: No horizontal scroll on a narrow phone
- **WHEN** the viewport is 360px wide
- **THEN** the document width equals the viewport width and the grid, gauges and component rows fit without horizontal scrolling

#### Scenario: Layout widens at breakpoints
- **WHEN** the viewport grows past each documented breakpoint
- **THEN** the layout changes according to the documented behaviour for that breakpoint

### Requirement: Text contrast meets WCAG AA
Text colour on the page background, on each surface step, and on the primary and danger accent surfaces SHALL reach a contrast ratio of at least 4.5:1 for body text and 3:1 for large text, and the measured ratios SHALL be recorded in the reference document.

#### Scenario: Ratios are recorded and pass
- **WHEN** the contrast table in the reference document is read
- **THEN** every listed pair shows its measured ratio and every pair meets its AA threshold

### Requirement: Reference document and showcase agree
The set of token names used in `docs/design-system.md` and the set defined in `docs/design-system.html` SHALL be identical.

#### Scenario: Token sets match
- **WHEN** the token names are extracted from both files and compared
- **THEN** no token appears in one file and not the other
