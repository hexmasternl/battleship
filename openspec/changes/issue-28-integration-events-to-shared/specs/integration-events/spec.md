# Spec Delta

## MODIFIED Requirements

### Requirement: Every backend service can publish without its own wiring
Every backend service that publishes catalog events SHALL be able to do so after one opt-in registration call, with no further registration or configuration. The shared service defaults SHALL NOT register the event sender themselves, so a service that does not publish carries no sender. The pub/sub component name and topic name SHALL come from one shared definition, the same one the application host uses to declare the component.

#### Scenario: Service publishes after applying the defaults
- **WHEN** a backend service applies the shared service defaults, makes the integration-events registration call, and requests the event sender
- **THEN** a sender is available that publishes to `integration-events` on `pubsub`

#### Scenario: Service defaults alone do not register a sender
- **WHEN** a backend service applies only the shared service defaults and requests the event sender
- **THEN** no sender is registered

#### Scenario: Component name has one source
- **WHEN** the pub/sub component name used by the application host, by the sender and by the Events service subscription is compared
- **THEN** all three come from the same shared constant
