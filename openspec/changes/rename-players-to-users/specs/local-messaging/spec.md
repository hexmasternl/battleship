# Spec Delta

## MODIFIED Requirements

### Requirement: Each API runs with a Dapr sidecar that loads the component
The Games API and the Users API SHALL each run with a Dapr sidecar that loads the `pubsub` component, and each API SHALL reach its sidecar through the endpoint configuration the AppHost injects.

#### Scenario: Sidecars are present for both APIs
- **WHEN** the AppHost is running
- **THEN** the dashboard shows a Dapr sidecar for the Games API and one for the Users API, each with the `pubsub` component loaded

#### Scenario: API locates its sidecar from configuration
- **WHEN** an API process starts under the AppHost
- **THEN** its configuration contains the Dapr HTTP and gRPC endpoints and the Dapr client in the process targets those endpoints

### Requirement: Services publish and subscribe through the component
Any service with a sidecar SHALL be able to publish an event to a topic on `pubsub` and SHALL be able to declare a subscription to a topic on `pubsub` that delivers each published event to a handler in that service.

#### Scenario: Event crosses from one API to the other
- **WHEN** the Users API publishes an integration event to the `integration-events` topic on `pubsub`, to which the Events API subscribes
- **THEN** the Events API handler receives the event within five seconds on a local run

#### Scenario: Subscription is discoverable by the sidecar
- **WHEN** the sidecar of a subscribing API asks the API for its subscriptions
- **THEN** the API returns the topics it subscribes to on `pubsub` and the route that handles each

#### Scenario: Handler acknowledges or retries
- **WHEN** a subscription handler throws
- **THEN** the event is not acknowledged and the sidecar redelivers it according to the component's retry behaviour
