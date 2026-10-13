# Tasks

## 1. Telemetry pipeline in ServiceDefaults

- [ ] 1.1 In `ConfigureOpenTelemetry`, register `AddSource("HexMaster.Battleship.*")` and `AddMeter("HexMaster.Battleship.*")`, set `ParseStateValues = true` on the OpenTelemetry logger, and enable `AddGrpcClientInstrumentation()` with `OpenTelemetry.Instrumentation.GrpcNetClient` pinned to an exact version, with a comment explaining the pre-release (design D1, D6, D8). Verify the solution builds and that a ServiceDefaults test resolves the `TracerProvider` and `MeterProvider` and shows that an `ActivitySource` named `HexMaster.Battleship.Test` is sampled.

## 2. Use-case instrumentation in Core

- [ ] 2.1 Add `HexMaster.Battleship.Core/Observability` with `CoreTelemetry` (source name and attribute constants), `CoreMetrics` (`battleship.use_case.duration` histogram on `IMeterFactory`) and `CoreLog` (design D2, D3, D5). Verify it builds.
- [ ] 2.2 Add `InstrumentedCommandHandler<TCommand, TResult>`, `InstrumentedCommandHandler<TCommand>`, `InstrumentedQueryHandler<TQuery, TResult>` and the `AddCommandHandler` / `AddQueryHandler` registration helpers (design D3). Verify unit tests with an `ActivityListener` and `MetricCollector<T>` pass for each of these:
  - the span name taken from the type name minus the suffix
  - status `Ok` on success
  - status `Error` with an exception event on a throw, with the exception rethrown
  - histogram tags `use_case` and `outcome`
  - a resolved interface returning the decorator that wraps the concrete handler

## 3. Players module

- [ ] 3.1 Add `Players/Observability` (`PlayersTelemetry`, `PlayersMetrics` with issued, refreshed and refused-by-reason counters, `PlayersLog` with event ids in the 1000s), and map the five refusal reasons to snake_case constants (design D2, D4, D5). Verify it builds.
- [ ] 3.2 Switch `PlayersModuleRegistration` to the new registration helpers. Make the handlers record metrics, set `player.id` and `token.refusal_reason` on `Activity.Current`, and log through `PlayersLog`. Replace the inline log in `TokenEndpoints`. Verify all of these pass:
  - handler tests that assert each counter, the refusal-reason tag and the span attributes
  - the existing Players endpoint tests, still green
  - a hygiene test showing that a create plus a refresh emits no tag, attribute or log state value equal to an issued token or the display name

## 4. Integration event publishing

- [ ] 4.1 Add `IntegrationEvents/Observability` (`IntegrationEventsTelemetry`, `IntegrationEventsMetrics`, `IntegrationEventsLog` in the 3000s). In `DaprIntegrationEventSender`, open a `Producer` activity `publish integration-events` with the messaging semantic-convention attributes and `event.type`, count published or failed by `event.type`, and set `Error` and rethrow on a publish exception (design D5, D6). Verify sender tests with a mocked `DaprClient` pass for the span kind, name and attributes, the published counter on success, and the failed counter plus `Error` status on a throw.

## 5. Events module

- [ ] 5.1 Add `Events/Observability` (`EventsTelemetry`, `EventsMetrics` with active connections, delivered, dropped-by-reason and replayed, `EventsLog` in the 2000s) (design D2, D5). Verify it builds.
- [ ] 5.2 Instrument the event pipeline (design D6):
  - `IntegrationEventEndpoints.Receive`: a `Consumer` activity `process integration-events`, with `Error` and `events.dropped{reason=invalid_envelope}` on a validation or parse drop
  - `EventRouter.Route`: a `deliver` child activity with `event.sequence` and `connection.count`, and an activity event per removed connection
  - `Connect` / `Disconnect`: the connection gauge, and the replay counter when a reconnect replays events
  - `EventConnection`: the channel-full drop counter
  - every log call moved to `EventsLog`

  Verify that router, connection and endpoint tests pass and assert each span, counter and gauge transition, and that the existing Events tests are still green.

## 6. Games module baseline

- [ ] 6.1 Add `Games/Observability` (`GamesTelemetry`, `GamesMetrics` with no instruments yet, `GamesLog` in the 4000s), and register `GamesMetrics` in the Games API host so upcoming handlers can use it. Verify the Games API starts under the AppHost. If issue #13 has merged first, align its `games.*` activity and counter with this convention instead of duplicating them.

## 7. Dapr sidecar tracing

- [ ] 7.1 In the AppHost, generate a Dapr `Configuration` file per run with `samplingRate "1"` and an OTLP exporter pointed at the dashboard OTLP endpoint. Pass it to every sidecar through `DaprSidecarOptions.Config`, and log a warning and continue without it when the endpoint variable is absent (design D7). Settle the protocol key against the installed runtime (design, Open Questions). Verify an AppHost test for the generated file content and the fallback, and that a local run shows `daprd` spans in a dashboard trace.

## 8. Documentation

- [ ] 8.1 Add an **Observability** section to the README:
  - the source and meter naming convention
  - the metric catalogue
  - where each signal appears in the dashboard
  - a walkthrough: create a player, then find the use-case span, the token metrics and the correlated log entry

  Also add a short "instrument a new handler or module" checklist. Verify the steps work as written against a local run.

## 9. Integration check

- [ ] 9.1 Run `dotnet test src/Battleship.slnx`. Then start the AppHost, create a player, refresh its token, hold an event stream open and publish an event (README "Try it by hand"). In the dashboard, confirm all of the following:
  - one trace from the publisher through the sidecar to the `deliver` span
  - the `players.tokens.*` and `events.*` metrics moving
  - structured log entries linked to their traces
  - no token or display name in any exported signal

  Record the trace ids and screenshots in the pull request.
