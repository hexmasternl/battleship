# Spec Delta

## Purpose

Explains, inside the web app, how this demo's .NET Aspire environment is configured, so a visitor can learn about its resources, Dapr usage, custom integration and custom resource commands without leaving the game.

## ADDED Requirements

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
The help routes SHALL be available without a user session and SHALL NOT call any backend API. The help code SHALL be emitted as a separate lazy chunk.

#### Scenario: No session needed
- **WHEN** a visitor without stored tokens opens `/help/prerequisites`
- **THEN** the topic is shown, no redirect to the landing page happens, and no request is sent to the backend gateway

#### Scenario: Lazy chunk
- **WHEN** the application is built in the production configuration
- **THEN** the help pages are emitted in a lazy chunk that is requested only when a help route is activated

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

#### Scenario: Resource kinds covered
- **WHEN** the resources topic renders
- **THEN** it names parameters, containers, projects, the gateway, the frontend and the dev bot, each with at least one resource name from this app

### Requirement: The resource relations topic explains how resources depend on each other
The resource relations topic SHALL explain references (connection information and environment flowing from one resource to another) and `WaitFor` start-up ordering, using this app's actual relations. It SHALL show the request and event flow:
1. browser to gateway
2. gateway to the Users and Games APIs
3. the APIs publishing to pub/sub
4. the Events API pushing events to the browser over server-sent events

#### Scenario: Flow shown
- **WHEN** the resource relations topic renders
- **THEN** it shows the flow from browser through gateway and APIs to pub/sub and back through the Events API, and names at least one `WaitFor` dependency of this app

#### Scenario: Flow is accessible
- **WHEN** the flow is presented as a diagram
- **THEN** the same flow is also available as text to assistive technology

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

### Requirement: The custom resource commands topic explains how the dashboard controls the bot
The custom resource commands topic SHALL explain what a custom resource command is. It SHALL describe each dev bot command:
- Set intelligence, with its preset choice
- Enable auto-join and Disable auto-join
- Join game, with its 8-character game code

It SHALL state that the commands are enabled only while the bot is healthy, and that the bot's intelligence, auto-join state and games are shown as live properties on the dashboard.

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
The help pages SHALL use the design system tokens and typefaces and SHALL pass an automated accessibility check with no violations. Every page SHALL have exactly one level-one heading. The pages SHALL NOT scroll horizontally at a 360px wide viewport.

#### Scenario: Automated check
- **WHEN** the help overview and any topic page are checked with axe
- **THEN** no violations are reported

#### Scenario: Narrow viewport
- **WHEN** a help page is viewed at 360px wide
- **THEN** the content fits without horizontal scrolling and code samples wrap or scroll within their own block
