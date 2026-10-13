# Spec Delta

## MODIFIED Requirements

### Requirement: Services publish and subscribe through the component
Any service with a sidecar SHALL be able to publish an event to a topic on `pubsub` and SHALL be able to declare a subscription to a topic on `pubsub` that delivers each published event to a handler in that service.

#### Scenario: Event crosses from one API to the other
- **WHEN** the Players API publishes an integration event to the `integration-events` topic on `pubsub`, to which the Events API subscribes
- **THEN** the Events API handler receives the event within five seconds on a local run

#### Scenario: Subscription is discoverable by the sidecar
- **WHEN** the sidecar of a subscribing API asks the API for its subscriptions
- **THEN** the API returns the topics it subscribes to on `pubsub` and the route that handles each

#### Scenario: Handler acknowledges or retries
- **WHEN** a subscription handler throws
- **THEN** the event is not acknowledged and the sidecar redelivers it according to the component's retry behaviour
