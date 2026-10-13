# Tasks

## 1. Prerequisites

- [x] 1.1 Confirm `issue-13-create-new-game` is merged into `development` and archived (its `authenticated-requests` and `game-creation` specs exist under `openspec/specs/`); stop and report if not; verify `dotnet build src/Battleship.slnx` and `npm test` in `src/Frontend` pass on the branch before any change, and list every unarchived change under `openspec/changes/` for task 6.2

## 2. Users module

- [x] 2.1 `git mv` `src/Players` to `src/Users` and every `HexMaster.Battleship.Players*` project directory and `.csproj` to `HexMaster.Battleship.Users*` (design D1); rename namespaces, `AddUsersModule`, `UsersDataRegistration.AddUsersData`, `UsersEndpoints.MapUsersEndpoints`, `UsersApiFactory`, the `.http` file, and fix `ProjectReference`, `InternalsVisibleTo` and `Battleship.slnx` (keeping project `Id`s); verify the solution builds and `HexMaster.Battleship.Users.Tests` passes with the same test count as before
- [x] 2.2 Change `TokenOptions` and `BearerTokenValidationOptions` to section `Users:Tokens` and default issuer `battleship-users` (design D2); update the Users, Games and ServiceDefaults tests' configuration keys and test issuer; add tests that startup names `Users:Tokens:SigningKey` and that a token with issuer `battleship-players` is refused; verify all .NET tests pass

## 3. AppHost and gateway

- [x] 3.1 Rename the AppHost resource to `hexmaster-battleship-users-api` (`GatewayRoutes.UsersApi`, module entry `users`), the Dapr app id to `users-api`, the parameter to `users-signing-key` and the environment variable to `Users__Tokens__SigningKey` for the Users and Games APIs (design D2, D6), using the `yarp-gateway-routes` skill to confirm the prefix set is unchanged; verify `GatewayRoutesTests` pass, and that `aspire start --isolated` shows `hexmaster-battleship-users-api` and the Games API healthy and a token from `/users/tokens` is accepted by `POST /games` through the gateway

## 4. Integration events and Events service

- [x] 4.1 Replace `PlayerCreatedEvent`/`PlayerUpdatedEvent` with `Users/UserCreatedEvent(UserId, DisplayName)` (`user.created.v1`) and `UserUpdatedEvent` (`user.updated.v1`) in the catalog, rename `EventAudience.Players` to `EventAudience.Users` and its callers (Games create handler), update the IntegrationEvents README (design D3); verify catalog, serialization and sender tests pass with the new types and `user` target
- [x] 4.2 Rename `EventTarget.Player` to `EventTarget.User` (JSON `user`), the stream parameter to `userId`, and the connection, registry, router, replay and resolver members and log templates to user naming (design D4); verify Events tests pass, including a new test that an envelope with target `player` is rejected and one that `?playerId=` alone gets `400`

## 5. Frontend

- [x] 5.1 With the `angular-frontend` agent: rename `PlayerSession` → `UserSession`, `PlayersApi` → `UsersApi`, `shared/player-name` → `shared/display-name` (`DisplayName`, `DisplayNameStore`, `DisplayNameDialog`, `openDisplayNameDialog`, `requireUser`), `EventsService.connect(userId)` with `?userId=`, and `REALTIME_EVENT_TYPES` `user.created.v1`/`user.updated.v1`, updating the interceptor, hero and welcome page (design D5); keep all visible copy; verify `npm test` passes with the same or higher test count, a spec still asserts the "Player name" label, and `npm run build` succeeds

## 6. Documentation, agent configuration and open proposals

- [x] 6.1 Update README (Users section, `Users:Tokens:*` table, resources, layout, curl examples, one-time reset and old-secret cleanup note), `docs/assets/architecture.svg`, `src/Frontend/README.md`, CLAUDE.md, the `hexmaster-*` skills, the `openspec-github-issues` label catalog and the git flow commit scopes (design D8); mirror `.claude/` to `.github/` with the `sync-agent-config` skill; verify its check reports both sides in sync
- [ ] 6.2 Update identity references in every unarchived change listed in 1.1 that is committed on `development` (design D9), leaving game-domain "player" untouched; verify `openspec validate --all` reports no new errors
- [ ] 6.3 Rename the GitHub label `players` to `users` (`gh label edit`), after confirming with the user that the outward-facing rename may proceed

## 7. Verification

- [ ] 7.1 Grep the repository (excluding `openspec/changes/archive`, `node_modules`, `bin`, `obj`) for `Players`, `players-`, `PlayerSession`, `player-name`, `playerId` on the event stream and `EventTarget.Player`; rename every identity hit and list the remaining game-domain hits in the pull request; run `dotnet test src/Battleship.slnx` and `npm test`; start the AppHost, create a user and a game from the landing page in a browser, confirm the redirect to `/games/{code}`, `game.created.v1` on the `?userId=` stream, and that a browser holding a pre-rename refresh token is asked for a name; verify all pass and record the results in the pull request
