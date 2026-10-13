# welcome-page Specification

## Purpose
Defines the landing page of the game: the first screen a visitor sees, what it shows, where its calls to action lead, how its bridge console behaves, and the responsiveness, motion and accessibility rules it must satisfy.

## Requirements

### Requirement: The application root shows the landing page
The application SHALL render the landing page at the root path `/`, and SHALL NOT render any scaffold or placeholder content from the application generator.

#### Scenario: Root path renders the landing page
- **WHEN** the application is opened at `/`
- **THEN** the landing page is displayed with its hero section and information section, and no generator placeholder text or styling is present

#### Scenario: Landing page is loaded on demand
- **WHEN** the application is built in the production configuration
- **THEN** the landing page code is emitted as a separate lazy chunk that is requested when the root route is activated

### Requirement: The hero fills the viewport and carries a marine theme
The landing page SHALL open with a hero section at least as tall as the viewport, with a marine-themed background composed from the design system surface and accent colours, and SHALL NOT embed third-party raster images.

#### Scenario: Hero height follows the viewport
- **WHEN** the page is viewed at any viewport between 360px and 1920px wide
- **THEN** the hero section's height is at least the viewport height and the information section starts below it

#### Scenario: Background uses design system colours
- **WHEN** the hero background is inspected
- **THEN** every colour in it resolves to a design system token and no external image file is requested

### Requirement: The jumbotron has two equal panels
The hero SHALL contain a jumbotron split into two panels of equal width at and above the large breakpoint (1024px), and stacked vertically below it with the welcome panel first.

#### Scenario: Side by side on a wide viewport
- **WHEN** the viewport is 1024px wide or wider
- **THEN** the welcome panel and the console panel sit side by side with equal widths

#### Scenario: Stacked on a narrow viewport
- **WHEN** the viewport is narrower than 1024px
- **THEN** the welcome panel appears above the console panel, both full width, and the page does not scroll horizontally

### Requirement: The welcome panel invites the visitor to play
The welcome panel SHALL show a welcome heading in the design system heading typeface, an introductory paragraph in the body typeface, and exactly two calls to action labelled "Create a new game" and "Join an existing game".

#### Scenario: Welcome content is present
- **WHEN** the landing page renders
- **THEN** the welcome panel contains one level-one heading, an introductory paragraph and two buttons or links with the labels "Create a new game" and "Join an existing game"

#### Scenario: Calls to action are styled by role
- **WHEN** the two calls to action are rendered
- **THEN** "Create a new game" uses the primary action style and "Join an existing game" uses the secondary action style of the design system

### Requirement: Calls to action navigate to the game routes
Activating "Create a new game" SHALL navigate to `/games/new` and activating "Join an existing game" SHALL navigate to `/games/join`. Until the game flows exist, both routes SHALL render a placeholder panel stating that the feature is coming soon.

#### Scenario: Create a new game
- **WHEN** the visitor activates "Create a new game" with a pointer or the keyboard
- **THEN** the router navigates to `/games/new` and the placeholder panel is shown

#### Scenario: Join an existing game
- **WHEN** the visitor activates "Join an existing game" with a pointer or the keyboard
- **THEN** the router navigates to `/games/join` and the placeholder panel is shown

### Requirement: The console shows marine-themed messages with timestamps
The console panel SHALL display messages as lines prefixed with a timestamp in the visitor's local time, formatted `[ hh:mm:ss AM ] - ` with a 12-hour clock, two-digit hours, minutes and seconds, and the AM or PM marker, followed by the message text.

#### Scenario: Line format
- **WHEN** a message is shown at 10:18:37 in the morning local time
- **THEN** the line reads `[ 10:18:37 AM ] - ` followed by the message text

#### Scenario: Afternoon marker
- **WHEN** a message is shown at 15:05:02 local time
- **THEN** the line starts with `[ 03:05:02 PM ] - `

### Requirement: Console messages are drawn at random from a large pool
The console SHALL draw each message at random from a pool of at least 100 distinct marine-themed messages, and SHALL NOT show the same message twice in a row.

#### Scenario: Pool size
- **WHEN** the message pool is inspected
- **THEN** it contains at least 100 entries and no two entries are equal

#### Scenario: No immediate repeat
- **WHEN** the console selects the next message
- **THEN** the selected message differs from the message shown immediately before it

### Requirement: Console messages appear at random intervals
After a message has been shown, the console SHALL show the next message after a delay chosen at random between 5 and 15 seconds inclusive. The first message SHALL appear within 5 seconds of the console becoming visible.

#### Scenario: Interval bounds
- **WHEN** the delay before the next message is chosen one thousand times
- **THEN** every delay is at least 5 seconds and at most 15 seconds

#### Scenario: First message
- **WHEN** the landing page renders
- **THEN** the first console line appears within 5 seconds

### Requirement: Console lines are typed and scroll upward
Each new line SHALL be appended at the bottom of the console and revealed character by character as if typed; older lines SHALL move up, and the console SHALL keep a bounded number of lines so it never grows beyond its panel.

#### Scenario: Typing animation
- **WHEN** a new message is scheduled and reduced motion is not requested
- **THEN** its text appears progressively from the first character to the last, with the timestamp shown at once

#### Scenario: Bounded height
- **WHEN** more lines have been shown than fit in the panel
- **THEN** the oldest lines are removed or scrolled out of view and the console's rendered height does not exceed its panel

### Requirement: The console stops when the page is left
The console SHALL cancel every pending timer and animation when the landing page is destroyed, and SHALL NOT keep scheduling messages afterwards.

#### Scenario: Navigating away
- **WHEN** the visitor navigates from `/` to another route
- **THEN** no further console messages are scheduled and no timer remains pending

### Requirement: Reduced motion is honoured
When the visitor requests reduced motion, the console SHALL show each line whole instead of typing it, and SHALL NOT run any looping animation anywhere on the page.

#### Scenario: Reduced motion typing
- **WHEN** `prefers-reduced-motion: reduce` is active and a new message is scheduled
- **THEN** the full line appears at once with no per-character animation

#### Scenario: Reduced motion decoration
- **WHEN** `prefers-reduced-motion: reduce` is active
- **THEN** every decorative instrument or background animation on the page is static

### Requirement: The information section describes the game
Below the hero, the page SHALL show at least four information panels, each with a heading and copy, covering at least how a game works, the five ships and their lengths, the 12x12 grid, and how turns and hits are resolved, laid out in a responsive grid.

#### Scenario: Panels present
- **WHEN** the information section renders
- **THEN** at least four panels are present, each with a heading and body copy, and the ships panel names Carrier (5), Battleship (4), Destroyer (3), Submarine (3) and Patrol Boat (2)

#### Scenario: Grid adapts to width
- **WHEN** the viewport is 360px wide
- **THEN** the panels stack in a single column with no horizontal scrolling, and at 768px or wider they are arranged in two or more columns

### Requirement: The landing page is accessible
The landing page SHALL pass an automated accessibility check with no violations, every text and background colour pair it introduces SHALL meet WCAG AA contrast, and every interactive element SHALL be reachable by keyboard with a visible focus indicator that does not rely on colour alone.

#### Scenario: Automated check
- **WHEN** the rendered landing page is checked with axe
- **THEN** no violations are reported

#### Scenario: Keyboard focus
- **WHEN** the visitor tabs through the page
- **THEN** both calls to action receive focus in document order and show the design system focus ring

#### Scenario: Console is announced politely
- **WHEN** a new console line appears
- **THEN** assistive technology is not interrupted: the console region is marked as a polite live region or is hidden from assistive technology as decorative content

### Requirement: The landing page builds and tests cleanly
The frontend SHALL build in the production configuration without errors or budget errors with the landing page included, and the landing page's unit tests SHALL pass.

#### Scenario: Production build
- **WHEN** the application is built in the production configuration
- **THEN** the build succeeds with no error and no budget error

#### Scenario: Unit tests
- **WHEN** the frontend unit tests run
- **THEN** the scheduler, console and landing page tests pass
