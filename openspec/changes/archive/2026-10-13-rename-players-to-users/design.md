# Design

## Context

See proposal.md (Why). State on `development` once #13 is merged and archived:

- **`src/Players/`** holds five projects (`Players`, `.Abstractions`, `.Api`, `.Data`, `.Tests`). The domain inside already speaks of users (`User`, `IUser`, `UserEndpoints`, `/users/...`). Only the module, the namespaces, the registration methods (`AddPlayersModule`, `AddPlayersData`, `MapPlayersEndpoints`), `TokenOptions.ConfigurationSection = "Players:Tokens"` and the default issuer `battleship-players` carry the old name.
- **AppHost.** The resource is `GatewayRoutes.PlayersApi = "hexmaster-battleship-players-api"`, the Dapr app id is `players-api`, and the parameter is `players-signing-key`. `Players__Tokens__SigningKey` is passed to the Players and Games APIs. The `GatewayRoutes` module entry is `new("players", PlayersApi, ["users"])`, so the prefix is already `users`.
- **ServiceDefaults.** `BearerTokenValidationOptions.ConfigurationSection = "Players:Tokens"`, with issuer `battleship-players`.
- **IntegrationEvents.**
  - `Players/PlayerCreatedEvent` and `Players/PlayerUpdatedEvent` (`player.created.v1`, `player.updated.v1`). No service publishes them.
  - `EventAudience.Players(...)` maps to `EventTarget.Player`.
- **Events.**
  - `EventTarget.Player` travels as `"player"`.
  - `QueryStringIdentityResolver.PlayerIdParameter = "playerId"`.
  - `EventConnection.PlayerId`, `ConnectionRegistry._byPlayer`, `IEventRouter.Connect(playerId, ...)`, `IReplayBuffer.Since(..., playerId)` and the log templates all use the player term.
- **Games** (from #13).
  - Tests sign tokens with `Players:Tokens:SigningKey` and issuer `battleship-players`.
  - The handler sends `EventAudience.Players(creator)`.
  - The game domain (`IPlayer`, `PlayerOne`, `playerId` in game events) stays as it is.
- **Frontend.**
  - `core/session/player-session.ts` (`PlayerSession`, `SESSION_CLOCK`, `isExpired`), `players-api.ts` (`PlayersApi`, `CREATE_TOKENS_URL`, `REFRESH_TOKENS_URL`) and the auth interceptor.
  - `shared/player-name/` (`PlayerName`, `PlayerNameStore`, `PlayerNameDialog`, `openPlayerNameDialog`, `requirePlayer`).
  - `events/events.service.ts` (`connect(playerId)`, `?playerId=`) and `realtime-event-types.ts` (`player.created.v1`, `player.updated.v1`).
  - The hero and welcome page use these names.

## Goals / Non-Goals

**Goals:**
- One word per bounded context. Identity, authentication, sessions and event delivery say *user*. Games say *player*. Where they meet, the mapping is stated: player id = user id.
- Behaviour after the rename is the same as before, apart from the listed wire and config names.
- Every reference in code, docs, specs and agent configuration is updated in one change, so no half-renamed state lands on `development`.

**Non-Goals:**
- Renaming game-domain types, game event payload fields or visible UI copy.
- Migrating existing local tokens or users.
- Renaming the `users` and `refreshtokens` tables.

## Decisions

### D1. Move the projects with `git mv`, then rename inside
`git mv src/Players src/Users`, then rename each project directory and `.csproj` (`HexMaster.Battleship.Players*` becomes `HexMaster.Battleship.Users*`), and fix:
- every `ProjectReference`, `InternalsVisibleTo` and `Battleship.slnx` entry (keeping the existing `Id` GUIDs);
- the AppHost's `Projects.HexMaster_Battleship_Players_Api`, which becomes `Projects.HexMaster_Battleship_Users_Api`;
- the `.http` file name.

Namespaces follow the folders. `git mv` keeps file history.

The registrations become `AddUsersModule`, `AddUsersData` (`UsersDataRegistration`) and `MapUsersEndpoints` (`UsersEndpoints`). The Api project's `UserSecretsId` stays, so a developer's API-level secrets survive. The only key in it that changes is `Players:Tokens:SigningKey`, which becomes `Users:Tokens:SigningKey`; the README tells developers to set it again.

The module's telemetry follows (decided during apply): `UsersTelemetry`, `UsersMetrics` and `UsersLog`, source and meter `HexMaster.Battleship.Users`, metrics `users.tokens.*` and span attribute `user.id`. The `backend-observability` spec records the metric and attribute names.

- *Alternative: keep the folder and rename only namespaces.* Rejected. The user asked for the projects to be renamed, and a `Players` folder holding `Users` namespaces would be the same confusion again.

### D2. Configuration and token names
- `TokenOptions.ConfigurationSection` and `BearerTokenValidationOptions.ConfigurationSection` become `"Users:Tokens"`.
- The default `Issuer` becomes `"battleship-users"`. `Audience` stays `"battleship"`.
- The AppHost parameter becomes `users-signing-key`, and the environment variable passed to both APIs becomes `Users__Tokens__SigningKey`.

The old parameter is not read as a fallback. The persisted key under `Parameters:players-signing-key` stays in the AppHost's user secrets, and the README says it can be deleted.

Consequence: access tokens issued before the change are refused (different issuer and key). Refresh tokens are opaque values stored in table storage and are not tied to the issuer, so while Azurite keeps its data a returning browser refreshes once and continues as the same user (verified during apply: the refresh handler only looks up the token's hash). Only a browser whose refresh token is unknown, for example after the Azurite container is removed, gets `401`; #7's session rules then clear the stored tokens and the next call to action asks for a name. The `user-session` spec states that case as a scenario.

- *Alternative: accept both issuers for a transition period.* Rejected. This is development only, with no users to protect, and a dual-issuer validator would be code written to be deleted.

### D3. Integration events
- `IntegrationEvents/Players/` becomes `IntegrationEvents/Users/`, holding `UserCreatedEvent(Guid UserId, string DisplayName)` (`user.created.v1`) and `UserUpdatedEvent(Guid UserId, string DisplayName)` (`user.updated.v1`).
- The old events are deleted, not versioned. The versioning rule protects published contracts, and these two were never published by any service or consumed by any client. The proposal records the break.
- `EventAudience.Players(params IEnumerable<Guid> userIds)` becomes `EventAudience.Users(...)`. `EventAudience.Game(params IEnumerable<Guid> userIds)` keeps its name; its parameter documents that the ids are the players' user ids.
- The frontend's `REALTIME_EVENT_TYPES` follows the catalog.

### D4. Events service
- `EventTarget.Player = 1` becomes `EventTarget.User = 1`, so the numeric value is unchanged and the JSON name is now `"user"`. The envelope validator rejects `"player"` as an unknown target kind; no special case is needed.
- `QueryStringIdentityResolver.UserIdParameter = "userId"`, and `IConnectionIdentityResolver.ResolveUserId`.
- `EventConnection.UserId`, `ConnectionRegistry` by user, `IEventRouter.Connect(userId, ...)` and `IReplayBuffer.Since(..., userId)` are renamed. Log templates use `{UserId}`.
- The problem title becomes "User identity required".

Events is not behind the gateway (the browser opens the stream directly), so no gateway route changes.

### D5. Frontend
| Before | After |
|---|---|
| `core/session/player-session.ts` `PlayerSession` | `core/session/user-session.ts` `UserSession` |
| `core/session/players-api.ts` `PlayersApi` | `core/session/users-api.ts` `UsersApi` |
| `shared/player-name/` `PlayerName`, `PlayerNameStore`, `PlayerNameDialog`, `openPlayerNameDialog` | `shared/display-name/` `DisplayName`, `DisplayNameStore`, `DisplayNameDialog`, `openDisplayNameDialog` |
| `shared/player-name/require-player.ts` `requirePlayer` | `shared/display-name/require-user.ts` `requireUser` |
| `EventsService.connect(playerId)`, `?playerId=` | `connect(userId)`, `?userId=` |

Session states keep their values (`unknown`, `restoring`, `known`). The store field `playerName` becomes `displayName`. The visible label "Player name", the button "Take command" and every other piece of copy stay exactly as they are; specs assert the label. Selectors and component tags change with the class names (`app-player-name` becomes `app-display-name`).

The work goes to the `angular-frontend` agent, as CLAUDE.md requires.

### D6. Gateway route table
`GatewayRoutes.PlayersApi` becomes `UsersApi = "hexmaster-battleship-users-api"`, and the module entry becomes `new("users", UsersApi, ["users"])`. The prefix set is unchanged (`users`, `games`), so the `yarp-gateway-routes` skill's approval for new or renamed prefixes is not triggered. The skill is still loaded to check that the table and the APIs agree after the rename.

### D7. Specs
- **`player-session` is retired.** All of its requirements are removed (`retire_capabilities: true`) and re-added under `user-session`. OpenSpec has no capability rename.
- **Renamed scenarios.** Where a requirement's scenario names carry the old word, the requirement is REMOVED and re-ADDED under a slightly re-worded name, because a MODIFIED block cannot rename scenarios. This applies to:
  - `frontend-backend-access`: "...forwarded to the gateway";
  - `local-gateway`: "...with the path unchanged";
  - `integration-events`: "...which users an event is for";
  - `realtime-events`: the subscribe, routing and replay requirements.
- **Kept scenario names.** `welcome-page` keeps its requirement and scenario names, because the visitor on that page is a player in the UI and #16 modifies the same requirement by name. Only the requirement and scenario bodies change.
- **#13's specs.** `authenticated-requests` and `game-creation` modify #13's specs, so #13 must be archived before this change is archived. `openspec validate` reports them as INFO until then.

### D8. Documentation and agent configuration
- README:
  - the "Players" section becomes "Users: anonymous identity";
  - the config table moves to `Users:Tokens:*`;
  - the AppHost resources table, the repository layout and troubleshooting are updated;
  - the `curl` examples use `?userId=` and `"target":"user"`;
  - a note explains the one-time token reset.
- `docs/assets/architecture.svg`: `players-api` becomes `users-api`.
- `src/Frontend/README.md`: the examples use `/api/users/me` and `?userId=`.
- The IntegrationEvents README follows the new catalog.
- CLAUDE.md and `copilot-instructions.md` list `Users`.
- The `hexmaster-*` skills name `Users` and `Games` as the modules. `hexmaster-resolve-subject-id-to-internal-user-id` names the Users module as the owner of user ids, and Games as referring to them as players.
- The `openspec-github-issues` labels catalog maps `users` to `src/Users`. The git flow commit scope `players` becomes `users`.
- `.claude/` edits are mirrored to `.github/` with the `sync-agent-config` skill.
- The GitHub label `players` is renamed with `gh label edit players --name users --description "Touches the Users service"`. Existing issues keep the label under its new name.

### D9. Other open proposals
#16–#19, #24 and any other unarchived change on `development` at apply time are planning documents. In each, these are replaced:
- "Players service" / "Players API" / `src/Players` with the Users equivalents;
- `?playerId=` on the event stream and target kind `player` with `?userId=` and `user`;
- `EventAudience.Players` with `EventAudience.Users`;
- `PlayerSession` / `player-name` with `UserSession` / `display-name`.

Game-domain "player" stays. These edits are committed in the change branch like everything else. Changes that another session is drafting but has not committed are left alone, and the overlap is reported in the pull request.

## Risks / Trade-offs

- [Merge conflicts with changes in flight (#16–#19, #24, #28, #29) that touch the same files] → Apply the rename right after #13 is archived and before the next feature branch is cut. Report every open change it touched in the pull request.
- [Old access tokens refused] → Accepted (D2). A refresh restores the session while the stored refresh token is known; a lost refresh token means a new user. Documented in the README and the pull request, and specified as a scenario.
- [Persisted user secrets under old names (`Parameters:players-signing-key`, `Players:Tokens:SigningKey`)] → They become unused, not harmful. The README lists the `dotnet user-secrets remove` commands.
- [Missed references] → The final task greps the repository for `Players`, `players-`, `playerId` on the stream, `PlayerSession` and `player-name` outside the game domain, and every hit is either renamed or listed as game domain.

## Migration Plan

Development only. After merging, run the AppHost once: it generates `users-signing-key`. Browsers with old access tokens refresh once and keep their user; a browser whose refresh token is unknown creates a new user. Rollback is reverting the merge commit; the old parameter is still in the user secrets, so the old tokens work again.
