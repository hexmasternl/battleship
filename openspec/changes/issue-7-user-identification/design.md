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

**Non-Goals:**
- Credential-based registration, external identity providers, or ADR 0010 style subject-id resolution (there is no external provider; the Players service is the issuer).
- Changing a display name, deleting a user, listing users, or any admin surface.
- Asymmetric signing keys, key rotation, Key Vault.
- Device or session tracking beyond one refresh-token chain per issuance; a user may hold several active refresh tokens (one per browser).
- Frontend token storage and the HTTP interceptor.

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

## Risks / Trade-offs

- [Symmetric key shared by every API] -> acceptable for a game with one trust domain; the key never leaves configuration, and moving to RS256 only changes `JwtTokenService` and the validation parameters.
- [Refresh replay between the read and the conditional update] -> ETag-conditioned update makes the second replay fail; a failed update is treated as refused.
- [Azurite table API differences from Azure Tables] -> only point reads, conditional updates and inserts are used, all supported by Azurite; the integration test runs against the same emulator the AppHost starts.
- [`Aspire.Azure.Data.Tables` and `Aspire.Hosting.Azure.Storage` versions must match Aspire 13.6.1] -> the first task pins the versions and builds before any code is written.
- [Users who never refresh accumulate rows] -> out of scope now; a cleanup of expired refresh tokens is a later change and does not affect the contracts.
- [Adding `Core` touches the solution shape] -> it only adds a project; nothing existing references it until Games adopts it.

## Open Questions

- Whether the display name should be unique. Assumed not: two users may both be `Admiral Nelson`; uniqueness is not required by the issue and would need a secondary index.
- Whether the refresh endpoint should also accept a still-valid access token to "extend" early. Assumed not: refresh needs only the refresh token, as the issue describes.
