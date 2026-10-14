# help-section Specification

## Purpose
Explains, inside the web app, how this demo's .NET Aspire environment is configured, so a visitor can learn about its resources, Dapr usage, custom integration and custom resource commands without leaving the game. The content is kept as markdown documents and diagrams in the repository's `docs/` folder.

## Requirements

### Requirement: The help overview lists every topic
The application SHALL render a help overview at `/help` with a level-one heading and one link for each help topic. Each link SHALL name the topic and give a one-sentence summary. The topics SHALL be, in this order:
- prerequisites
- resources
- resource relations
- Dapr
- custom integration
- custom resource commands

#### Scenario: Overview content
- **WHEN** the visitor opens `/help`
- **THEN** a level-one heading and six topic links are shown in the listed order, each with a title and a summary

#### Scenario: Topic link opens the topic
- **WHEN** the visitor activates the "Dapr" link on the overview
- **THEN** the router navigates to `/help/dapr` and the Dapr topic page is shown

### Requirement: Every topic has its own deep-linkable route
Each help topic SHALL be reachable directly at `/help/<topic-slug>`. The slugs SHALL be:
- `prerequisites`
- `resources`
- `resource-relations`
- `dapr`
- `custom-integration`
- `resource-commands`

The document title SHALL name the topic. An unknown slug SHALL redirect to `/help`.

#### Scenario: Direct navigation to a topic
- **WHEN** the application is opened at `/help/resource-commands`
- **THEN** the custom resource commands topic is shown and the document title names that topic

#### Scenario: Unknown topic
- **WHEN** the application is opened at `/help/not-a-topic`
- **THEN** the router redirects to `/help` and the overview is shown

### Requirement: The help section is public and loaded on demand
The help routes SHALL be available without a user session. They SHALL NOT send any request to the backend gateway. Help documents and images SHALL be loaded only from the frontend's own origin. The help code, including its markdown rendering, SHALL be emitted as a separate lazy chunk and SHALL NOT enlarge the initial bundle.

#### Scenario: No session needed
- **WHEN** a visitor without stored tokens opens `/help/prerequisites`
- **THEN** the topic is shown, no redirect to the landing page happens, and no request is sent to the backend gateway

#### Scenario: Lazy chunk
- **WHEN** the application is built in the production configuration
- **THEN** the help pages and the markdown renderer are emitted in lazy chunks that are requested only when a help route is activated

### Requirement: Help content is authored as markdown in the docs folder
Each help topic's content SHALL come from one markdown document in the repository's `docs/` folder. Images SHALL be stored in `docs/assets/`. Documents SHALL reference images and other topics with relative links, so they also render correctly when browsed on GitHub. The frontend build SHALL publish exactly the help documents and the images they use.

#### Scenario: One document per topic
- **WHEN** the topic list is compared with the `docs/` folder
- **THEN** every topic has exactly one markdown document there, and every image it references exists in `docs/assets/`

#### Scenario: Readable on GitHub
- **WHEN** a help document is opened on GitHub
- **THEN** its images display and its links to other topics open the corresponding documents

#### Scenario: Published with the app
- **WHEN** the application is built
- **THEN** the help documents and their images are served by the frontend, and unrelated files in `docs/` are not

### Requirement: Topic pages render their markdown document as HTML
A topic page SHALL fetch its markdown document and render it as HTML in the design system's typography and colours. The rendered HTML SHALL support:
- headings
- paragraphs
- lists
- tables
- inline code and code blocks
- block quotes
- images

The rendered HTML SHALL be sanitised. While the document is loading, the page SHALL show a loading state. If the document fails to load, the page SHALL show an error with a retry action.

#### Scenario: Document rendered
- **WHEN** the visitor opens `/help/dapr`
- **THEN** the Dapr document's headings, lists, code blocks and images are shown styled with design system tokens and typefaces

#### Scenario: Load failure
- **WHEN** the topic's document cannot be fetched
- **THEN** an error message with a retry action is shown, and activating retry fetches the document again

#### Scenario: Unsafe markup removed
- **WHEN** a document contains a script element or an inline event handler
- **THEN** neither is present in the rendered page

#### Scenario: Code blocks fit narrow screens
- **WHEN** a topic with a long code line is viewed at 360px wide
- **THEN** the code block scrolls within itself and the page does not scroll horizontally

### Requirement: Links inside help documents behave as in-app navigation
A relative link from one help document to another SHALL navigate to that topic's `/help/<slug>` route without a full page reload. A relative image reference SHALL resolve to the published `docs/assets/` location. An external link SHALL open in a new browsing context, with `rel="noopener"`.

#### Scenario: Link to another topic
- **WHEN** the visitor activates a link to `dapr.md` inside the resources document
- **THEN** the router navigates to `/help/dapr` without reloading the page

#### Scenario: Image path resolved
- **WHEN** a document references `assets/<diagram>.svg`
- **THEN** the image loads from the frontend's published docs assets

#### Scenario: External link
- **WHEN** the visitor activates a link to an external site
- **THEN** it opens in a new tab and the link carries `rel="noopener"`

### Requirement: Diagrams follow the design system theme
Diagrams and pictures in the help documents SHALL be SVG files in `docs/assets/`. They SHALL use only the design system's colours: base colours or documented derived values. They SHALL use only its typefaces, embedded or with the documented fallbacks, so they render in theme both in the app and on GitHub. Every image SHALL have alt text describing what it shows. Every diagram that carries information SHALL be accompanied by the same information in text.

#### Scenario: Theme colours only
- **WHEN** the fill and stroke colours of a help diagram are collected
- **THEN** every colour is a design system base colour or a derived value documented in the design system

#### Scenario: Alt text and text equivalent
- **WHEN** a help document includes a diagram
- **THEN** the image has non-empty alt text, and the document states the diagram's information in prose, a list or a table

### Requirement: The prerequisites topic lists what a developer needs
The prerequisites topic SHALL name the tools needed to run the demo locally:
- the .NET 10 SDK (C#)
- Docker Desktop or another container runtime
- the Dapr CLI, plus the one-time `dapr init`
- Node.js with npm
- the Aspire CLI

It SHALL also explain how to start the AppHost, and that the AppHost checks for the Dapr CLI and fails fast when it is missing or not initialised.

#### Scenario: Tools listed
- **WHEN** the prerequisites topic renders
- **THEN** it names the .NET 10 SDK, Docker, the Dapr CLI with `dapr init`, Node.js with npm and the Aspire CLI, and shows the command that starts the AppHost

### Requirement: The resources topic explains resources using this app
The resources topic SHALL explain what an Aspire resource is. It SHALL describe each kind used by this app with its concrete resources:
- parameters (RabbitMQ credentials, the users signing key, the game cleanup schedule)
- containers (the RabbitMQ `messaging` broker and the Azurite `storage` emulator with its `tables` resource)
- projects (the Games, Users and Events APIs, each with a Dapr sidecar)
- the YARP `gateway`
- the JavaScript `frontend`
- the `devbot`

It SHALL include a diagram of the app's resources.

#### Scenario: Resource kinds covered
- **WHEN** the resources topic renders
- **THEN** it names parameters, containers, projects, the gateway, the frontend and the dev bot, each with at least one resource name from this app, and shows a resource diagram

### Requirement: The resource relations topic explains how resources depend on each other
The resource relations topic SHALL explain references (connection information and environment flowing from one resource to another) and `WaitFor` start-up ordering, using this app's actual relations. It SHALL show the request and event flow in a diagram:
1. browser to gateway
2. gateway to the Users and Games APIs
3. the Games API publishing integration events to pub/sub
4. the Events API, which subscribes to pub/sub and pushes events to the browser over server-sent events on a connection the browser opens directly, not through the gateway

#### Scenario: Flow shown
- **WHEN** the resource relations topic renders
- **THEN** it shows a diagram of the flow from browser through gateway and APIs to pub/sub and back through the Events API, states the same flow as an ordered list, and names at least one `WaitFor` dependency of this app

#### Scenario: Publishers and the event connection are stated accurately
- **WHEN** the resource relations topic text is inspected
- **THEN** it names the Games API as the publisher of integration events and states that the browser connects to the Events API directly rather than through the gateway

### Requirement: The Dapr topic describes Dapr's actual role in this app
The Dapr topic SHALL explain what a Dapr sidecar is. It SHALL state which resources run one:
- the Games, Users and Events APIs
- the dev bot

It SHALL describe:
- the RabbitMQ pub/sub component and the `integration-events` topic
- the cron input binding that triggers the daily game cleanup

It SHALL NOT claim that the app uses Dapr state stores or service invocation.

#### Scenario: Dapr building blocks
- **WHEN** the Dapr topic renders
- **THEN** it describes sidecars, the RabbitMQ pub/sub component with the `integration-events` topic and the cron binding for game cleanup

#### Scenario: No overstated usage
- **WHEN** the Dapr topic text is inspected
- **THEN** it does not describe state stores or service invocation as being used by this app

### Requirement: The custom integration topic explains the dev bot integration
The custom integration topic SHALL explain what a custom Aspire hosting integration is: a custom resource type plus builder extension methods. It SHALL use the dev bot as the example and cover:
- that the bot is added with a single builder call
- that it gets its own Dapr sidecar and a gateway reference
- that it exposes a health check
- that its intelligence, join grace period and shot delay are configurable
- that it plays as a local opponent, joining unclaimed games after the grace period

#### Scenario: Integration explained
- **WHEN** the custom integration topic renders
- **THEN** it explains the custom resource type and builder extension, and describes what the dev bot does and how it is configured

### Requirement: The custom resource commands topic explains how the Aspire dashboard controls the bot
The custom resource commands topic SHALL explain what a custom resource command is. It SHALL describe each dev bot command:
- Set intelligence, with its preset choice
- Enable auto-join and Disable auto-join
- Join game, with its 8-character game code

It SHALL state that the commands are enabled only while the bot is healthy, and that the bot's intelligence, auto-join state and games are shown as live properties on the Aspire dashboard.

#### Scenario: Commands described
- **WHEN** the custom resource commands topic renders
- **THEN** it names Set intelligence, Enable auto-join, Disable auto-join and Join game, and states what each does to the bot

### Requirement: Help pages offer a way back
Every help topic page SHALL offer a link to the help overview and a link to the landing page. It SHALL also offer links to the previous and next topic where they exist. The help overview SHALL link to the landing page.

#### Scenario: Navigating from a topic
- **WHEN** the visitor is on `/help/dapr`
- **THEN** links to `/help`, to `/`, to the previous topic and to the next topic are present and keyboard reachable

#### Scenario: First and last topic
- **WHEN** the visitor is on the first or the last topic
- **THEN** no previous link is shown on the first topic and no next link is shown on the last topic

### Requirement: The help section follows the design system and is accessible
The help pages SHALL use the design system tokens and typefaces and SHALL pass an automated accessibility check with no violations, including the rendered documents. Every page SHALL have exactly one level-one heading. The pages SHALL NOT scroll horizontally at a 360px wide viewport.

#### Scenario: Automated check
- **WHEN** the help overview and any rendered topic page are checked with axe
- **THEN** no violations are reported

#### Scenario: Single level-one heading
- **WHEN** a topic page has rendered its document
- **THEN** the page contains exactly one level-one heading

#### Scenario: Narrow viewport
- **WHEN** a help page is viewed at 360px wide
- **THEN** the content, images and tables fit without the page scrolling horizontally
