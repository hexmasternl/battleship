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
The landing page SHALL open with a hero section at least as tall as the viewport, with a marine-themed background: an inline vector drawing of a submarine control room, 1920 by 1080 units, cropped to cover the hero, composed from design system tokens and animated with CSS only (blinking lamps that glow while lit, moving gauge needles and meters, a radar sweep, sonar pings and steam from relief vents). The hero SHALL NOT embed third-party raster images.

#### Scenario: Hero height follows the viewport
- **WHEN** the page is viewed at any viewport between 360px and 1920px wide
- **THEN** the hero section's height is at least the viewport height and the information section starts below it

#### Scenario: Background uses design system colours
- **WHEN** the hero background is inspected
- **THEN** every colour in it resolves to a design system token and no external image file is requested

#### Scenario: Backdrop is decorative
- **WHEN** the hero renders
- **THEN** the submarine drawing is hidden from assistive technology, sits behind the jumbotron, and the welcome and console panels stay fully readable over it

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
Activating "Create a new game" SHALL, for a known user, open a modal dialog asking to confirm creating a new game. Confirming SHALL create the game and navigate to `/games/{game-code}` of the new game; cancelling SHALL leave the visitor on the landing page and create nothing. Activating "Join an existing game" SHALL, for a known user, open a modal dialog asking for a game code. Joining SHALL navigate to `/games/{game-code}` of the joined game; cancelling SHALL leave the visitor on the landing page and join nothing. The game code input SHALL accept exactly 8 characters `A`-`Z` and `0`-`9` and SHALL show typed letters in upper case. When the game is not found, is full or has already started, the join dialog SHALL stay open and name that reason. When the user is unknown, either call to action SHALL first open a modal dialog containing the shared display-name component; after that component has created the user, "Create a new game" SHALL continue with the confirmation dialog and "Join an existing game" SHALL continue with the join dialog. Dismissing any of these dialogs SHALL leave the visitor on the landing page. When a landing page refresh is still pending, the call to action SHALL wait for it before deciding. Until the game page exists, `/games/{game-code}` SHALL render a placeholder panel naming the game code.

#### Scenario: Create a new game
- **WHEN** a known user activates "Create a new game" with a pointer or the keyboard, confirms in the dialog, and the game is created with code `K7Q2M9XA`
- **THEN** the dialog closes, the router navigates to `/games/K7Q2M9XA` and the placeholder panel naming `K7Q2M9XA` is shown

#### Scenario: Confirmation dialog for a known player
- **WHEN** a known user activates "Create a new game"
- **THEN** the router does not navigate, a modal dialog opens asking to create a new game with a confirm and a cancel action, and focus moves into the dialog

#### Scenario: Creation cancelled
- **WHEN** the user cancels the confirmation dialog or presses Escape
- **THEN** no request to create a game is sent, the router does not navigate, and focus returns to "Create a new game"

#### Scenario: Creation fails
- **WHEN** the user confirms and the create request fails
- **THEN** the dialog stays open, shows an error with a way to try again, and the router does not navigate

#### Scenario: Confirm cannot be sent twice
- **WHEN** the user confirms and the create request is still pending
- **THEN** the confirm action is disabled and a second activation sends no second request

#### Scenario: Join dialog for a known player
- **WHEN** a known user activates "Join an existing game" with a pointer or the keyboard
- **THEN** the router does not navigate, a modal dialog opens with a game code input, a join action and a cancel action, and focus moves to the game code input

#### Scenario: Join an existing game
- **WHEN** a known user enters `K7Q2M9XA` in the join dialog and joins, and the game accepts the user
- **THEN** the dialog closes, the router navigates to `/games/K7Q2M9XA` and the placeholder panel naming `K7Q2M9XA` is shown

#### Scenario: Game code is upper-cased and limited
- **WHEN** the user types `k7q2m9xa` in the game code input
- **THEN** the input shows `K7Q2M9XA`, and the join action stays disabled while the code is not exactly 8 characters `A`-`Z` or `0`-`9`

#### Scenario: Game not found
- **WHEN** the user joins with a code the backend does not know
- **THEN** the dialog stays open, states that no game with that code was found, keeps the entered code for correction, and the router does not navigate

#### Scenario: Game full
- **WHEN** the user joins a game that already has two other players
- **THEN** the dialog stays open, states that the game is full, and the router does not navigate

#### Scenario: Game already started
- **WHEN** the user joins a game that has already started without them
- **THEN** the dialog stays open, states that the game has already started, and the router does not navigate

#### Scenario: Join fails unexpectedly
- **WHEN** the join request fails for another reason, such as a network error
- **THEN** the dialog stays open, shows an error with a way to try again, and the router does not navigate

#### Scenario: Join cannot be sent twice
- **WHEN** the user joins and the join request is still pending
- **THEN** the join action is disabled and a second activation sends no second request

#### Scenario: Join cancelled
- **WHEN** the user cancels the join dialog or presses Escape
- **THEN** no join request is sent, the router does not navigate, and focus returns to "Join an existing game"

#### Scenario: Unknown player is asked for a name
- **WHEN** an unknown visitor activates "Create a new game" or "Join an existing game"
- **THEN** the router does not navigate, a modal dialog opens with the display-name component labelled "Player name", and focus moves into the dialog

#### Scenario: Join dialog after the player is created
- **WHEN** the unknown visitor enters a valid name in the dialog opened by "Join an existing game" and the user is created
- **THEN** the display-name dialog closes and the join dialog opens

#### Scenario: Confirmation after the player is created
- **WHEN** the unknown visitor enters a valid name in the dialog opened by "Create a new game" and the user is created
- **THEN** the display-name dialog closes and the confirmation dialog opens

#### Scenario: Dialog dismissed
- **WHEN** the visitor closes the dialog with its cancel action or the Escape key without creating a user
- **THEN** the router does not navigate, no tokens are stored, and focus returns to the call to action that opened the dialog

#### Scenario: Pending refresh is awaited
- **WHEN** a returning user activates a call to action while the landing page refresh request is still pending and that refresh then succeeds
- **THEN** no display-name dialog opens, and the call to action continues as for a known user

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
