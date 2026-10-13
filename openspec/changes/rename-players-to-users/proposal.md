# Proposal

## Why

The identity service is called "Players", yet it creates *users*, issues *user* tokens and serves `/users/...`. The frontend keeps a "player session", and the Events service addresses connections by `playerId`. Features mix both words for the same thing, which makes the code, the specs and the docs hard to follow.

The domain language is:
- **Users** authenticate, and the server identifies users. The identity module speaks only of users.
- **Games** refer to users as *players*. A player in a game has an id and a display name, and that id is the user's id. It is the same entity under a different name in a different bounded context.

This change renames the identity side to Users and leaves the game side as Players. The rename is cheapest now: only #7, #13 and the Events service depend on the identity names, and no production data exists.

## What Changes

- **Users module.** `src/Players/HexMaster.Battleship.Players{,.Abstractions,.Api,.Data,.Tests}` becomes `src/Users/HexMaster.Battleship.Users{...}`. This covers:
  - namespaces;
  - `AddUsersModule` and `AddUsersData`;
  - `MapUsersEndpoints`;
  - the `.http` file;
  - `Battleship.slnx`.

  The HTTP API is unchanged (`/users/tokens`, `/users/tokens/refresh`, `/users/me`).
- **Users telemetry.** `PlayersTelemetry`, `PlayersMetrics` and `PlayersLog` become `UsersTelemetry`, `UsersMetrics` and `UsersLog`. The activity source and meter follow the assembly name (`HexMaster.Battleship.Users`), the metrics become `users.tokens.issued`, `users.tokens.refreshed` and `users.tokens.refused`, and the token use-case spans carry `user.id` instead of `player.id`. The Games module keeps `player.id`.
- **AppHost and gateway.**
  - The API resource becomes `hexmaster-battleship-users-api`, and its Dapr app id becomes `users-api`.
  - In the gateway route table, the module entry `players` becomes `users`. Its prefix is already `users`, so no gateway prefix changes.
- **BREAKING (local only): token configuration.** The configuration section `Players:Tokens` becomes `Users:Tokens`, the AppHost parameter `players-signing-key` becomes `users-signing-key`, and the default issuer `battleship-players` becomes `battleship-users`.

  A new signing key is generated on the first run after the change, and tokens with the old issuer are refused. Every browser therefore silently starts over as a new anonymous user once. Users stored in Azurite stay, but become unreachable from the browser that created them.
- **BREAKING (wire): integration events.** `player.created.v1` and `player.updated.v1` (`PlayerCreatedEvent`, `PlayerUpdatedEvent` in namespace `IntegrationEvents.Players`) are replaced by `user.created.v1` and `user.updated.v1` (`UserCreatedEvent(UserId, DisplayName)` and `UserUpdatedEvent(UserId, DisplayName)` in `IntegrationEvents.Users`). Nothing publishes or consumes the old events yet, so they are removed rather than versioned.

  `EventAudience.Players(...)` becomes `EventAudience.Users(...)`. `EventAudience.Game(...)` keeps its name; its ids are the players' user ids. Game event payloads keep `playerId` and `playerName`, because those belong to the game domain.
- **BREAKING (wire): real-time events.**
  - The envelope target kind `player` becomes `user`, and `EventTarget.Player` becomes `EventTarget.User`.
  - The stream identifies the connection with `?userId=` instead of `?playerId=`.
  - Connections, routing and replay speak of users.
- **Frontend identity layer.**
  - `PlayerSession` becomes `UserSession` and `PlayersApi` becomes `UsersApi`.
  - `shared/player-name/` becomes `shared/display-name/` (`DisplayNameDialog`, `DisplayName` component and store), and `requirePlayer` becomes `requireUser`.
  - The events client connects for a user id.
  - **Visible copy does not change.** The visitor still sees "Player name" and "Take command", because in the game UI they are a player.
- **Documentation and agent configuration.**
  - Updated: README, `src/Frontend/README.md`, `src/Shared/HexMaster.Battleship.IntegrationEvents/README.md`, the architecture diagram, CLAUDE.md and `.github/copilot-instructions.md`.
  - The `hexmaster-*` standards skills, the `openspec-github-issues` label catalog (`players` becomes `users`), and the git flow commit scopes, mirrored to `.github/` through `sync-agent-config`.
  - The GitHub label `players` is renamed to `users`.
  - The open change proposals (#16–#19, #24 and later ones) get their identity references updated: "Players service" becomes "Users service", and `playerId` on the event stream becomes `userId`.

## Capabilities

### New Capabilities
- `user-session`: how the frontend keeps the user's identity. Covers token storage, restoring a returning user, the shared display-name component that creates a user, and when the user is known. It takes over every requirement of `player-session` under user naming.

### Modified Capabilities
- `player-session`: retired. All of its requirements move to `user-session`.
- `user-identification`: the Users service replaces the Players service. Configuration lives under `Users:Tokens`, and the default issuer is `battleship-users`.
- `bearer-authentication`: APIs validate access tokens issued by the Users service.
- `local-gateway`: the gateway waits for and routes `/users` to the Users API.
- `local-messaging`: the Users API runs with its own Dapr sidecar.
- `frontend-backend-access`: the rewrite example uses a real Users path (`/api/users/me`).
- `realtime-events`: target kind `user`, target user ids, and the stream identifies a user.
- `realtime-events-client`: the client opens the stream for the current user.
- `integration-events`: the catalog has `user.created.v1` and `user.updated.v1`, and audiences address users.
- `welcome-page`: the calls to action use the user session and the display-name component.
- `authenticated-requests`: requests carry the user's access token, and renewal goes through the user session.
- `game-creation`: the creating player's id is the user id from the Users service's token.
- `backend-observability`: the Users module's metrics are `users.tokens.*`, its spans carry `user.`-prefixed attributes, and metric tags exclude user ids.

## Impact

- **Code**:
  - `src/Players` → `src/Users` (every project);
  - `src/Aspire` (AppHost, `GatewayRoutes`, ServiceDefaults `BearerTokenValidationOptions`, tests);
  - `src/Shared/HexMaster.Battleship.IntegrationEvents` and its tests;
  - `src/Events` (Abstractions, module, Api, tests);
  - `src/Games` (test token issuer and configuration key, `EventAudience.Users`);
  - `src/Frontend/src/app/core/session`, `shared/player-name`, `events`, `welcome` and `games`.
- **Configuration**: `Users:Tokens:*`, the `Parameters:users-signing-key` user secret, and `Users__Tokens__SigningKey` for the Users and Games APIs.
- **Data**: no table or schema change. The tables `users` and `refreshtokens` stay as they are.
- **Depends on**: `issue-13-create-new-game`, which must be merged and archived first. Its specs (`authenticated-requests`, `game-creation`, `welcome-page`) are the versions this change modifies, and its code (Games auth, frontend interceptor and hero flow) is renamed here.
- **Coordination**: other changes in flight on `development`, such as #24 and #28 (integration events moved to a shared project), touch the same names. Whichever change lands second rebases onto the other.
- **Out of scope**:
  - Game domain naming (`Game.PlayerOne`, `IPlayer`, game event payloads).
  - Visible UI copy.
  - Renaming storage tables.
  - Migrating existing local identities.
