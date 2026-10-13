# Design

## Context

See proposal.md for motivation. Observed state: `src/Players/` holds four projects on .NET 10. `HexMaster.Battleship.Players.Api` is the `webapi` template (`AddServiceDefaults`, `AddOpenApi`, the `/weatherforecast` sample, `UseHttpsRedirection`); `HexMaster.Battleship.Players` and `.Abstractions` each contain only `Class1.cs`; `.Tests` is the xUnit template (`xunit` 2.9.3, `coverlet.collector`, one empty fact). `HexMaster.Battleship.Aspire.ServiceDefaults` wires OpenTelemetry, health checks and service discovery and has no authentication. The AppHost (`Aspire.AppHost.Sdk/13.6.1`) registers the Games and Players APIs and no other resource. There is no `Core` project, so the handler interfaces and domain model base classes the house standards expect (ADR 0004, ADR 0009, ADR 0013) do not exist yet.

Platform facts the design relies on:
- Aspire Azure Storage hosting: `builder.AddAzureStorage("storage").RunAsEmulator(...)` models an Azurite container locally; `storage.AddTables("tables")` is the table sub-resource a project references with `WithReference(tables)`. The client integration (`Aspire.Azure.Data.Tables`) registers a `TableServiceClient` with `builder.AddAzureTableServiceClient("tables")` (the pre-9.4 name `AddAzureTableClient` is obsolete). The implementer verifies these two method names against the package versions that match Aspire 13.6.1 before writing code.
- ASP.NET Core 10 JWT bearer validation comes from `Microsoft.AspNetCore.Authentication.JwtBearer`; token creation uses `JsonWebTokenHandler` from `Microsoft.IdentityModel.JsonWebTokens`, which that package already brings in.
- Minimal API validation (`AddValidation` plus data annotations on records) is built into ASP.NET Core 10 and produces `400` problem details.

House standards applied: modules are feature slices (ADR 0007, ADR 0009) with endpoints in the module project (structure-0001), rich domain models behind interfaces with `Reconcile` (ADR 0013), xUnit with Moq and Bogus at 80% coverage (recommendation-0003), and no third-party package without a defensible reason.

## Goals / Non-Goals

**Goals:**
- A visitor gets a durable identity in one anonymous call and keeps it through refresh for as long as they come back within 90 days.
- Token validation is written once (ServiceDefaults) and reused by every API from one configuration section.
- Handlers are tested without storage; storage is tested against Azurite; nothing in the module project knows Azure.
- The first module establishes the shared `Core` project so later modules (Games, Events) start from the same base.
- A returning visitor is recognised on the landing page without any interaction; a new visitor is asked for a player name once, at the moment they choose to create or join a game.

**Non-Goals:**
- Credential-based registration, external identity providers, or ADR 0010 style subject-id resolution (there is no external provider; the Players service is the issuer).
- Changing a display name, deleting a user, listing users, or any admin surface.
- Asymmetric signing keys, key rotation, Key Vault.
- Device or session tracking beyond one refresh-token chain per issuance; a user may hold several active refresh tokens (one per browser).
- Attaching the access token to outgoing frontend requests (a bearer interceptor) and renewing the access token proactively while a page stays open. No frontend call needs authentication yet; the first game feature that does adds both.
- Showing or editing the player name in the frontend.

## Decisions

**Project layout.** Four projects in `src/Players/` plus one new `HexMaster.Battleship.Players.Data` project, and the new `src/Core/HexMaster.Battleship.Core`. Namespaces follow ADR 0009:

```
src/Core/HexMaster.Battleship.Core/
  Cqrs/ICommandHandler.cs, IQueryHandler.cs
  DomainModels/DomainModel.cs, DomainModelState.cs, IDomainModel.cs, DomainException.cs
src/Players/HexMaster.Battleship.Players.Abstractions/
  DataTransferObjects/CreateTokensRequest.cs, RefreshTokensRequest.cs, TokenPairResponse.cs, CurrentUserResponse.cs
  DomainModels/IUser.cs, IRefreshToken.cs
  Repositories/IUserRepository.cs, IRefreshTokenRepository.cs
src/Players/HexMaster.Battleship.Players/
  DomainModels/User.cs, RefreshToken.cs
  Features/CreateTokens/CreateTokensCommand.cs, CreateTokensCommandHandler.cs
  Features/RefreshTokens/RefreshTokensCommand.cs, RefreshTokensCommandHandler.cs
  Features/GetCurrentUser/GetCurrentUserQuery.cs, GetCurrentUserQueryHandler.cs
  Services/ITokenService.cs, JwtTokenService.cs, RefreshTokenGenerator.cs, CallSignGenerator.cs
  Endpoints/PlayersEndpoints.cs (entry point), TokenEndpoints.cs (/tokens), UserEndpoints.cs (/users)
  Configuration/TokenOptions.cs
  PlayersModuleRegistration.cs
src/Players/HexMaster.Battleship.Players.Data/
  Entities/UserEntity.cs, RefreshTokenEntity.cs
  Repositories/UserTableRepository.cs, RefreshTokenTableRepository.cs
  PlayersDataRegistration.cs
```

Alternative considered: keep the storage code inside the module project to avoid a fifth project. Rejected: ADR 0013 says the module project must not know its storage, and the repository tests against Azurite then stay out of the pure unit test path.

**Storage model.** Two tables, both created on first use with `CreateIfNotExists`:

| Table | PartitionKey | RowKey | Properties |
|---|---|---|---|
| `users` | first two characters of the user id (spreads load; the id is a `Guid` string) | user id | `DisplayName`, `CreatedAt`, `LastSeenAt`, `IsRevoked` |
| `refreshtokens` | `"token"` prefix plus the first two characters of the hash | Base64Url SHA-256 hash of the token | `UserId`, `IssuedAt`, `ExpiresAt`, `ConsumedAt` (null until rotated) |

Looking up a refresh token is a point read by hash, so no query scans. Rotation is an atomic conditional update on the consumed row (ETag) followed by an insert of the new row; if the conditional update fails because the row was already consumed, the request is refused, which closes the replay window. Alternative considered: store the refresh token on the user row. Rejected: it limits a user to one browser and makes rotation a read-modify-write on the user row.

**Token formats.** Access token: JWT, `alg` HS256, claims `sub` (user id), `name` (display name), `iat`, `exp`, `iss`, `aud`, `jti`. Signed with `TokenOptions.SigningKey` (a Base64 or plain string of at least 32 bytes). Refresh token: 32 random bytes from `RandomNumberGenerator`, Base64Url encoded, returned once; stored as the Base64Url SHA-256 hash. Alternative considered: make the refresh token itself a signed JWT. Rejected: the issue says the refresh token identifies the user in the store, so a lookup is needed anyway, and a random value leaks nothing if the table is read.

**Lifetimes and configuration.** `TokenOptions` bound from `Players:Tokens` with `SigningKey`, `Issuer` (default `battleship-players`), `Audience` (default `battleship`), `AccessTokenLifetime` (default `00:15:00`), `RefreshTokenLifetime` (default `90.00:00:00`), `ClockSkew` (default `00:00:30`). Registered with `AddOptions<TokenOptions>().BindConfiguration("Players:Tokens").ValidateDataAnnotations().ValidateOnStart()`, with a custom validation that the decoded key is at least 32 bytes, so a missing key stops the host at startup. Locally the key lives in `dotnet user-secrets` of the Players API and is passed to the other APIs by the AppHost as a parameter (`builder.AddParameter("players-signing-key", secret: true)` fed into each project's `Players__Tokens__SigningKey`). Alternative considered: generate a key on startup when none is configured. Rejected: every restart would invalidate every token and services would not share the key.

**Shared validation in ServiceDefaults.** `AddBattleshipAuthentication(this IHostApplicationBuilder)` adds `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` with `TokenValidationParameters` built from the same `Players:Tokens` section (issuer, audience, symmetric key, `ClockSkew`), plus `AddAuthorization()`. It does not call `UseAuthentication`/`UseAuthorization`; `WebApplication` adds those automatically when authentication services exist, and endpoints opt in with `RequireAuthorization()`. The Players API is the only caller in this change; Games keeps working untouched. Alternative considered: a separate `HexMaster.Battleship.Authentication` package. Rejected for now: one extension method with one options type does not justify a project; it can move later.

**Feature slices and domain models.** `User : DomainModel<Guid>, IUser` with `DisplayName`, `CreatedAt`, `LastSeenAt`, `IsRevoked`, operations `Touch(now)` and `Revoke()`, `Create(displayName, now)` enforcing the 1 to 40 character rule, and `Reconcile(...)`. `RefreshToken : DomainModel<string>, IRefreshToken` keyed by hash with `UserId`, `IssuedAt`, `ExpiresAt`, `ConsumedAt`, operations `Consume(now)` (throws when already consumed or expired) and `IsActive(now)`. `CreateTokensCommandHandler` creates the user, asks `ITokenService` for a pair, stores the refresh token hash. `RefreshTokensCommandHandler` hashes the presented value, loads the token, loads the user, checks `IsActive` and `IsRevoked`, consumes, issues a new pair, stores the new hash, touches the user. Domain rule failures surface as a `TokenRefusedException` the endpoint maps to `401` problem details, so handlers stay free of HTTP types.

**Endpoints.** `TokenEndpoints` maps `POST /tokens` (`201` with `TokenPairResponse`, `400` validation problem) and `POST /tokens/refresh` (`200`, `400`, `401`). `UserEndpoints` maps `GET /users/me` with `RequireAuthorization()`, reading `sub` and `name` from `ClaimsPrincipal`. `PlayersEndpoints.MapPlayersEndpoints` chains both. `Program.cs` becomes `AddServiceDefaults`, `AddBattleshipAuthentication`, `AddOpenApi`, `AddPlayersModule`, `AddPlayersData`, then `MapDefaultEndpoints`, `MapOpenApi` (development), `MapPlayersEndpoints`.

**AppHost.** `var storage = builder.AddAzureStorage("storage").RunAsEmulator(e => e.WithLifetime(ContainerLifetime.Persistent)); var tables = storage.AddTables("tables");` then `.WithReference(tables).WaitFor(tables)` on the Players API, plus the signing-key parameter. Persistent lifetime keeps users across restarts during development.

**Testing.** Unit tests (xUnit, Moq, Bogus) for `User`, `RefreshToken`, both command handlers, the query handler, `JwtTokenService` (claims, expiry, signature) and `TokenOptions` validation, with repositories mocked. Integration tests use `WebApplicationFactory<Program>` with an Azurite container started through the Aspire testing host (`Aspire.Hosting.Testing`) or, when that proves heavy, `Testcontainers` is **not** added; instead the test fixture requires a running Azurite (`UseDevelopmentStorage=true`) and is skipped with a clear reason when it is not reachable. Coverage target 80% on the module project, measured with `coverlet.collector`.

**Frontend token storage.** A small `TokenStorage` class over the Web Storage API with four keys: `battleship.refreshToken` and `battleship.refreshTokenExpiresAt` in `localStorage`, `battleship.accessToken` and `battleship.accessTokenExpiresAt` in `sessionStorage`. Every read and write is wrapped in `try`/`catch` so a browser that blocks storage degrades to "unknown player" instead of throwing. Storing a pair writes all four keys; clearing removes all four. Alternative considered: both tokens in `localStorage`. Rejected by the issue: the access token must not outlive the browser session. Alternative considered: an `HttpOnly` cookie for the refresh token. Rejected for now: it needs cookie handling and CSRF protection across the gateway, and the issue asks for local storage explicitly.

**Player session store.** `PlayerSession` is a root `@Service` store (session-wide state, per the Angular agent's rules) with one private `signal<PlayerSessionState>` holding `status` (`unknown`, `restoring`, `known`) and exposing read-only `status` and `isKnown` slices. Intent methods:
- `restore()`: if no refresh token is stored, or its stored expiry has passed (tokens are then cleared), stays `unknown`. Otherwise sets `restoring`, posts the refresh token to `/api/tokens/refresh`, stores the new pair and becomes `known`. On `401` it clears the tokens and becomes `unknown`. On a network or `5xx` failure it keeps the refresh token (the failure may be transient) and becomes `unknown`; the next `restore()` tries again. Concurrent calls share one in-flight request.
- `createPlayer(displayName)`: posts `{ displayName }` to `/api/tokens`, stores the pair and becomes `known`; failures are returned to the caller and leave the state `unknown`.
- `ensurePlayer()`: used by the calls to action. Waits for an in-flight restore; if the player is still unknown and a refresh token is stored, runs `restore()` once more; then reports whether the player is known.

`PlayersApi` is a thin `HttpClient` wrapper with `createTokens` and `refreshTokens`, so the store is testable with `HttpTestingController`. Requests go to `/api/tokens` and `/api/tokens/refresh`; the `issue-11-yarp-local-gateway` interceptor rewrites them to the gateway.

**Two tabs and rotation.** Two tabs opening the landing page at the same moment present the same refresh token; the second presentation is refused because the first rotated it. Before clearing on `401`, `restore()` reads `localStorage` again: if the stored refresh token differs from the one it presented, another tab already rotated it, so it retries once with the new value instead of clearing. Only a `401` for the token still stored clears the session.

**Landing page refresh.** `WelcomePage` injects `PlayerSession` and calls `restore()` once when it is created. The page does not wait for it; the hero renders immediately. The refresh runs on every visit to `/` that has a stored refresh token, as the issue asks, even when the session already holds an access token.

**Player-name component and dialog.** `shared/player-name/player-name.ts` (`battle-player-name`) is a standalone, dialog-agnostic component: a Signal Forms form with one `playerName` field (trimmed, required, 1 to 40 characters to match the backend rule), a submit button and an inline error region. Its component-scoped `PlayerNameStore` owns the form model, `submitting` and `error`, and its `submit()` intent calls `PlayerSession.createPlayer`. On success the component emits `playerCreated`; on failure it shows a retryable error and keeps the typed name. `PlayerNameDialog` hosts it in an Angular Material dialog with a heading, the component and a cancel action, and closes with `true` on `playerCreated` and `false` on cancel or Escape. The UI requires a name even though the API accepts none: the issue asks the visitor for one, and the generated call sign stays as the API's fallback for other clients.

**Gated calls to action.** The hero calls to action stay links with `routerLink` so they keep link semantics, keyboard behaviour and the existing tests. A click handler calls `ensurePlayer()`; when the player is known the navigation proceeds. When not, it prevents the navigation, opens `PlayerNameDialog`, and navigates to the same route only when the dialog closes with `true`. Alternative considered: a route guard on `/games/new` and `/games/join`. Rejected: a guard cannot open a dialog without the visitor first leaving the landing page, and deep links to those routes would need a different flow; the guard can be added later when the game screens exist.

## Risks / Trade-offs

- [Symmetric key shared by every API] -> acceptable for a game with one trust domain; the key never leaves configuration, and moving to RS256 only changes `JwtTokenService` and the validation parameters.
- [Refresh replay between the read and the conditional update] -> ETag-conditioned update makes the second replay fail; a failed update is treated as refused.
- [Azurite table API differences from Azure Tables] -> only point reads, conditional updates and inserts are used, all supported by Azurite; the integration test runs against the same emulator the AppHost starts.
- [`Aspire.Azure.Data.Tables` and `Aspire.Hosting.Azure.Storage` versions must match Aspire 13.6.1] -> the first task pins the versions and builds before any code is written.
- [Users who never refresh accumulate rows] -> out of scope now; a cleanup of expired refresh tokens is a later change and does not affect the contracts.
- [Refresh token in `localStorage` is readable by any script on the origin] -> accepted by the issue for a game with no credentials; the token is rotated on every visit, and a strict content security policy is a later hardening step.
- [Rotating on every landing page visit multiplies refresh token rows] -> one row per visit, each expiring after 90 days; the cleanup of consumed and expired rows already listed above covers it.
- [Frontend depends on `issue-11-yarp-local-gateway`] -> apply that change first; the frontend tasks assume `provideHttpClient` and the `/api/` rewrite exist.
- [Adding `Core` touches the solution shape] -> it only adds a project; nothing existing references it until Games adopts it.

## Open Questions

- Whether the display name should be unique. Assumed not: two users may both be `Admiral Nelson`; uniqueness is not required by the issue and would need a secondary index.
- Whether the refresh endpoint should also accept a still-valid access token to "extend" early. Assumed not: refresh needs only the refresh token, as the issue describes.
