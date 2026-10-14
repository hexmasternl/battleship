# Spec Delta

## Purpose

Defines what the production environment in Azure consists of and how the services are hosted and configured there, so that a deployed release is playable end to end.

## ADDED Requirements

### Requirement: Infrastructure is declared as code
All production resources except the container registry SHALL be declared in Bicep templates in the repository and SHALL be created or updated by deploying them. Deploying the same templates and version twice SHALL leave the environment unchanged.

#### Scenario: Fresh subscription
- **WHEN** the templates are deployed to a subscription without the environment
- **THEN** the resource group and every resource in it are created and the app is playable

#### Scenario: Redeploy is idempotent
- **WHEN** the same version is deployed again
- **THEN** the deployment succeeds and no resource is recreated

### Requirement: The APIs and gateway run as Container Apps
The Games, Users and Events APIs and the gateway SHALL each run as a Container App in one Container Apps environment. The APIs SHALL pull their images from the existing registry with its credentials. Only the gateway and the Events API SHALL accept traffic from the internet; the Games and Users APIs SHALL be reachable only inside the environment.

#### Scenario: Module APIs are not public
- **WHEN** someone on the internet calls the Games or Users API directly
- **THEN** the request does not reach the API

#### Scenario: Gateway routes to the modules
- **WHEN** the frontend calls `<gateway>/users/...` or `<gateway>/games/...`
- **THEN** the gateway forwards it to the Users or Games API with the path unchanged

### Requirement: The deployed gateway uses the same route table as local runs
The deployed gateway SHALL route exactly the prefixes declared in the shared gateway route table, to the same APIs. A prefix that is not declared, including platform and maintenance routes, SHALL NOT be routed.

#### Scenario: Route tables cannot drift
- **WHEN** a prefix is added to the gateway route table but not to the deployed gateway configuration
- **THEN** a test fails in CI

#### Scenario: Maintenance route is not exposed
- **WHEN** someone calls `<gateway>/maintenance/game-cleanup`
- **THEN** the gateway does not forward the request

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
The APIs SHALL allow cross-origin requests from the Static Web App's origin and SHALL reject cross-origin requests from other origins, both through the gateway and on the Events API stream.

#### Scenario: Hosted frontend can call the backend
- **WHEN** the frontend served by the Static Web App calls the gateway or opens the event stream
- **THEN** the browser accepts the responses

#### Scenario: Other origins are blocked
- **WHEN** a page on another origin calls the gateway from the browser
- **THEN** the browser blocks the response

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
The frontend SHALL be served by an Azure Static Web App. Deep links to app routes SHALL serve the application instead of a 404.

#### Scenario: Deep link loads the app
- **WHEN** a player opens `<static web app>/games/<id>` directly
- **THEN** the application loads and shows that route
