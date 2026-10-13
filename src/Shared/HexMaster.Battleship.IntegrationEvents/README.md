# HexMaster.Battleship.IntegrationEvents

The catalog of integration events the backend services publish, and the sender that publishes them.

Every event is wrapped in the integration event envelope from `HexMaster.Battleship.Events.Abstractions` and published to topic `integration-events` on the shared `pubsub` Dapr component. The Events service routes each envelope to the browsers in its audience. The event's type name is the envelope `type` and the server-sent event name the browser receives.

## Catalog

| Type name | Event | Payload |
| --- | --- | --- |
| `player.created.v1` | `PlayerCreatedEvent` | `playerId`, `playerName` |
| `player.updated.v1` | `PlayerUpdatedEvent` (name changed) | `playerId`, `playerName` |
| `game.created.v1` | `GameCreatedEvent` | `gameId`, `gameCode`, `playerId` (the creator) |
| `game.joined.v1` | `GameJoinedEvent` (second player joined) | `gameId`, `gameCode`, `playerId`, `playerName` |
| `game.started.v1` | `GameStartedEvent` | `gameId`, `gameCode`, `playerWithTurnId` |
| `game.shot-fired.v1` | `GameShotFiredEvent` | `gameId`, `gameCode`, `playerId`, `column`, `row` |
| `game.ended.v1` | `GameEndedEvent` | `gameId`, `gameCode`, `reason` (`completed`, `abandoned-by-player`, `ended-by-server`), optional `winnerId` |

`IntegrationEventCatalog.Events` is the authoritative list. A unit test fails when an `IIntegrationEvent` is missing from it.

## Type names and versioning

- A type name has the form `<subject>.<action>.v<version>`: lowercase, kebab-case segments, a positive integer version. Names are unique.
- A published version never changes incompatibly. Removing or renaming a field, or changing its type or meaning, adds the same event with the next version **alongside** the existing one, for example `PlayerCreatedEventV2` with `player.created.v2`. The old version stays in the catalog until no publisher uses it.
- Adding an optional field needs no new version.
- When a type name is added or removed, update `REALTIME_EVENT_TYPES` in `src/Frontend/src/app/events/realtime-event-types.ts`, so the browser listens for it.

## Publishing

A publishing API opts in with `builder.AddIntegrationEvents()` right after `AddServiceDefaults()`, and can then inject `IIntegrationEventSender`:

```csharp
await sender.SendAsync(
    new GameJoinedEvent(game.Id, game.Code, player.Id, player.Name),
    EventAudience.Game(game.PlayerIds),
    cancellationToken);
```

The publisher always states the audience:

- `EventAudience.Players(ids)`: the listed players.
- `EventAudience.Game(playerIds)`: the players of a game.
- `EventAudience.Everyone`: every connected player.

A players or game audience without any player id is rejected with an `ArgumentException`, and nothing is published.
