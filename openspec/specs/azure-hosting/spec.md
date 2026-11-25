# azure-hosting Specification

## Purpose

Defines what the production environment in Azure consists of and how the services are hosted and configured there, so that a deployed release is playable end to end.

## Requirements

### Requirement: Infrastructure is declared as code
All production resources except the container registry SHALL be declared in Bicep templates in the repository and SHALL be created or updated by deploying them. Deploying the same templates and version twice SHALL leave the environment unchanged.

#### Scenario: Fresh subscription
- **WHEN** the templates are deployed to a subscription without the environment
- **THEN** the resource group and every resource in it are created and the app is playable

#### Scenario: Redeploy is idempotent
- **WHEN** the same version is deployed again
- **THEN** the deployment succeeds and no resource is recreated

### Requirement: The APIs and gateway run as Container Apps
The Games, Users and Events APIs SHALL each run as a Container App in one Container Apps environment. The APIs SHALL pull their images from the existing registry with its credentials. No API SHALL accept traffic on its own address from the internet; all three SHALL be reachable only inside the environment and through the environment's route configuration. No gateway Container App SHALL run in the environment.

#### Scenario: Module APIs are not public on their own address
- **WHEN** someone on the internet calls the Games, Users or Events API on the app's own address
- **THEN** the request does not reach the API

#### Scenario: Environment routes to the modules
- **WHEN** the frontend calls `<backend>/users/...`, `<backend>/games/...` or `<backend>/events?...` on the environment's route configuration address
- **THEN** the environment forwards it to the Users, Games or Events API with the path unchanged

#### Scenario: Event stream stays open through the route
- **WHEN** the frontend opens the server-sent event stream at `<backend>/events?userId=<id>`
- **THEN** the stream stays open across idle periods, because the heartbeat arrives before the proxy's idle timeout, and events reach the browser

#### Scenario: No gateway app
- **WHEN** the templates are deployed
- **THEN** the environment holds Container Apps for the three APIs only, and no `gateway` Container App exists afterwards

### Requirement: The deployed gateway uses the same route table as local runs
The environment's route configuration SHALL route exactly the prefixes declared in the shared gateway route table, to the Container Apps of the same modules. A prefix that is not declared, including platform and maintenance routes, SHALL NOT be routed. Each prefix SHALL match on a path-segment boundary.

#### Scenario: Route tables cannot drift
- **WHEN** a prefix is added to the gateway route table but not to the deployed route configuration source
- **THEN** a test fails in CI

#### Scenario: Live routes can be checked against the table
- **WHEN** the route sync skill compares the deployed route configuration with the shared route table
- **THEN** it reports every module, prefix or target app that differs, or that they are in sync

#### Scenario: Maintenance route is not exposed
- **WHEN** someone calls `<backend>/maintenance/game-cleanup`
- **THEN** the environment does not forward the request

#### Scenario: Prefix matches whole segments
- **WHEN** someone calls `<backend>/usersx/anything`
- **THEN** the request is not forwarded to the Users API

### Requirement: Messaging runs on Azure Service Bus through Dapr
The environment SHALL have a Dapr pub/sub component named `pubsub` backed by an Azure Service Bus namespace that has a topic for every topic the services publish to, currently `integration-events`. The component SHALL be scoped to the Games, Users and Events APIs, so the services publish and subscribe exactly as they do locally.

#### Scenario: Event reaches the browser
- **WHEN** a player takes a shot in the deployed app
- **THEN** the integration event travels through Service Bus and the opponent's browser receives it on the event stream

### Requirement: Game cleanup is scheduled in production
The environment SHALL have a Dapr cron input binding named `game-cleanup`, scoped to the Games API, that calls the API's maintenance route on the production schedule (daily).

#### Scenario: Cleanup runs daily
- **WHEN** a day has passed since the last run
- **THEN** the Games API receives a call on its game cleanup route

### Requirement: State is stored in Azure Table Storage
The Users and Games APIs SHALL store their tables in an Azure Storage Account of the environment. They SHALL access it with a managed identity rather than an account key.

#### Scenario: Data survives a new release
- **WHEN** a new version is deployed
- **THEN** existing users and games are still there

### Requirement: Secrets are not stored in the repository
The token signing key SHALL be supplied from a secret of the GitHub `production` environment and stored as a Container Apps secret. No secret value SHALL appear in the templates, the parameters files or the workflow logs.

#### Scenario: Tokens stay valid across releases
- **WHEN** a player signed in before a deployment calls the API after it
- **THEN** their token is still accepted, because the signing key did not change

### Requirement: Browsers are allowed only from the hosted frontend
The APIs SHALL allow cross-origin requests from the frontend's custom hostname origin, `https://battleship.hexmaster.nl`, and SHALL reject cross-origin requests from other origins. The Users and Games APIs SHALL answer preflight and actual requests themselves from a configured list of origins in every environment; the Events API SHALL do the same for its stream. No API SHALL allow any origin.

#### Scenario: Hosted frontend can call the backend
- **WHEN** the frontend served on `https://battleship.hexmaster.nl` calls the Users or Games API through the route configuration or opens the event stream
- **THEN** the browser accepts the responses, including the preflight

#### Scenario: Other origins are blocked
- **WHEN** a page on another origin calls the backend from the browser
- **THEN** the browser blocks the response

#### Scenario: No origins configured outside Development
- **WHEN** a Users or Games API runs outside Development with no allowed origins configured
- **THEN** it accepts no cross-origin request

### Requirement: Telemetry is collected in Application Insights
Every API SHALL send its traces, metrics and logs to an Application Insights resource backed by the environment's Log Analytics workspace, and the Container Apps environment SHALL send its logs to the same workspace.

#### Scenario: A request is traceable
- **WHEN** a player creates a game in the deployed app
- **THEN** Application Insights shows the request's trace across the Games API and the published integration event

### Requirement: The Events API runs as a single instance
The Events API SHALL run with exactly one replica, because it keeps the routing state of open streams in memory.

#### Scenario: No scale-out
- **WHEN** load on the Events API increases
- **THEN** it keeps running as one replica

### Requirement: The frontend is hosted on Azure Static Web Apps
The frontend SHALL be served by an Azure Static Web App on the custom hostname `battleship.hexmaster.nl` over HTTPS with a valid certificate. The custom domain SHALL be declared in the templates and validated through a CNAME record that the repository documents. Deep links to app routes SHALL serve the application instead of a 404.

#### Scenario: App loads on the custom hostname
- **WHEN** a player opens `https://battleship.hexmaster.nl`
- **THEN** the application loads over HTTPS without a certificate warning

#### Scenario: Deep link loads the app
- **WHEN** a player opens `https://battleship.hexmaster.nl/games/<id>` directly
- **THEN** the application loads and shows that route

#### Scenario: DNS record is documented
- **WHEN** someone prepares a fresh environment
- **THEN** the infrastructure documentation names the CNAME record that must exist before the deployment can validate the custom domain
