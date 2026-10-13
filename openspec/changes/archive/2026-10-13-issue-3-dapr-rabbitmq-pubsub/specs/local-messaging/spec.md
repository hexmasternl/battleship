# Spec Delta

## Purpose

Defines the asynchronous messaging backbone of the local development environment: the broker the AppHost runs, the shared Dapr pub/sub component, how services publish and subscribe through it, and the prerequisites a developer needs for the AppHost to start.

## ADDED Requirements

### Requirement: The AppHost runs a RabbitMQ broker
The AppHost SHALL start a RabbitMQ broker resource named `messaging` with the management plugin enabled as part of every local run, and SHALL report it on the dashboard with a health state.

#### Scenario: Broker starts with the application
- **WHEN** the AppHost is started locally
- **THEN** a resource named `messaging` of type RabbitMQ appears on the Aspire dashboard, reaches the healthy state, and its management UI is reachable on the port the dashboard shows

#### Scenario: Broker credentials are not hand-maintained
- **WHEN** the broker's username and password are inspected
- **THEN** they are parameters owned by the AppHost (generated or user-secret backed) and no project configuration file in the repository contains them

### Requirement: A shared Dapr pub/sub component is backed by the broker
The AppHost SHALL model a Dapr pub/sub component named `pubsub` whose message broker is the `messaging` resource, and the component SHALL obtain the broker connection from that resource rather than from a connection string maintained by hand.

#### Scenario: Component resolves to the running broker
- **WHEN** the AppHost is running and a Dapr sidecar loads the `pubsub` component
- **THEN** the sidecar reports the component as loaded with the RabbitMQ pub/sub type and connects to the `messaging` resource

#### Scenario: Broker port changes do not break the component
- **WHEN** the broker starts on a different host port than on a previous run
- **THEN** the `pubsub` component still connects without any file in the repository being edited

### Requirement: Each API runs with a Dapr sidecar that loads the component
The Games API and the Players API SHALL each run with a Dapr sidecar that loads the `pubsub` component, and each API SHALL reach its sidecar through the endpoint configuration the AppHost injects.

#### Scenario: Sidecars are present for both APIs
- **WHEN** the AppHost is running
- **THEN** the dashboard shows a Dapr sidecar for the Games API and one for the Players API, each with the `pubsub` component loaded

#### Scenario: API locates its sidecar from configuration
- **WHEN** an API process starts under the AppHost
- **THEN** its configuration contains the Dapr HTTP and gRPC endpoints and the Dapr client in the process targets those endpoints

### Requirement: Services publish and subscribe through the component
Any service with a sidecar SHALL be able to publish an event to a topic on `pubsub` and SHALL be able to declare a subscription to a topic on `pubsub` that delivers each published event to a handler in that service.

#### Scenario: Event crosses from one API to the other
- **WHEN** the Games API publishes an event to a topic on `pubsub` that the Players API subscribes to
- **THEN** the Players API handler receives the event payload within five seconds on a local run

#### Scenario: Subscription is discoverable by the sidecar
- **WHEN** the sidecar of a subscribing API asks the API for its subscriptions
- **THEN** the API returns the topics it subscribes to on `pubsub` and the route that handles each

#### Scenario: Handler acknowledges or retries
- **WHEN** a subscription handler throws
- **THEN** the event is not acknowledged and the sidecar redelivers it according to the component's retry behaviour

### Requirement: Local prerequisites are documented and checked
The repository SHALL document the tools a developer needs to run the AppHost with messaging (Docker, the Dapr CLI and an initialised Dapr runtime), and the AppHost SHALL fail with a message naming the missing prerequisite when the Dapr runtime is not initialised.

#### Scenario: Prerequisites are documented
- **WHEN** a developer reads the repository README
- **THEN** it lists Docker, the Dapr CLI and `dapr init` as prerequisites for a local run and names the command to start the application

#### Scenario: Missing Dapr runtime is reported
- **WHEN** the AppHost is started on a machine where `dapr init` has not been run
- **THEN** the run stops with an error that names Dapr as the missing prerequisite instead of leaving the sidecars in a restart loop
