# Proposal

> Tracks GitHub issue [hexmasternl/battleship#7](https://github.com/hexmasternl/battleship/issues/7).

## Why

Nothing in the solution identifies a player: the Players API still serves the template `/weatherforecast` endpoint, has no data store and no authentication, and no other service can tell one browser from another. Creating a game, joining one and routing real-time events to the right client all need a stable identity for a visitor who never registers, so anonymous identity has to exist before any of those features can be built.

## What Changes

- Add anonymous token issuance to the Players service: `POST /tokens` creates a user with a unique id and a display name (optional, defaulting to a generated nautical call sign), stores it in Azure Table Storage and returns a signed access token, an opaque refresh token and both expiry instants.
- Add refresh with rotation: `POST /tokens/refresh` exchanges a valid refresh token for a new access token and a new refresh token, invalidates the old refresh token and records the user's last-seen time; unknown, expired, revoked or already-rotated refresh tokens are rejected with `401` and a problem-details body.
- Add `GET /users/me`, the first endpoint that requires a bearer token, returning the caller's id and display name.
- Define the token formats: access tokens are HS256-signed JWTs with the user id as `sub` and the display name as a claim, valid for a configurable short lifetime (default 15 minutes); refresh tokens are 256-bit random values that are stored only as a SHA-256 hash and expire 90 days after issue.
- Store users and refresh tokens in two Azure Table Storage tables behind a repository abstraction, so handlers are unit-testable without storage and the integration tests run against Azurite.
- Model an Azure Storage resource with a tables sub-resource in the Aspire AppHost that runs as the Azurite emulator locally, and inject its connection into the Players API through the Aspire client integration, so `aspire run` needs no Azure subscription.
- Add a reusable bearer-authentication registration to ServiceDefaults (`AddBattleshipAuthentication`) that validates the same tokens from the same configuration, so the Games API and the future Events API can protect endpoints without re-implementing validation.
- Read the signing key and lifetimes from configuration (`Players:Tokens:*`, user secrets locally) and fail fast at startup when the signing key is missing or too short.
- Introduce the shared `HexMaster.Battleship.Core` project (command and query handler interfaces, domain model base classes) that the house standards expect every module to build on, since this is the first real feature in the solution.

## Capabilities

### New Capabilities
- `user-identification`: how an anonymous visitor becomes a known user of Battleship: token issuance, the access and refresh token contracts, refresh rotation and rejection rules, the stored user record and the `/users/me` identity endpoint.
- `bearer-authentication`: how any Battleship API validates an access token issued by the Players service and exposes the caller's identity to endpoints, including the configuration it needs and the failure behaviour when that configuration is missing.

### Modified Capabilities
<!-- none: there are no existing specs for the backend services -->

## Impact

- `src/Players/HexMaster.Battleship.Players.Abstractions/`: gains the DTO records, the `IUser` and `IRefreshToken` domain model interfaces and the repository interfaces; `Class1.cs` is removed.
- `src/Players/HexMaster.Battleship.Players/`: gains the domain models, the three feature slices (`CreateTokens`, `RefreshTokens`, `GetCurrentUser`), the token service, the endpoints and the module registration; `Class1.cs` is removed. The project gains a framework reference to `Microsoft.AspNetCore.App` and references `Microsoft.IdentityModel.JsonWebTokens` (ships with the JWT bearer package).
- New `src/Players/HexMaster.Battleship.Players.Data/`: the Azure Table Storage repositories, referencing `Azure.Data.Tables` through the `Aspire.Azure.Data.Tables` client integration.
- `src/Players/HexMaster.Battleship.Players.Api/`: `Program.cs` loses the weather-forecast sample and calls the module registration, the endpoint mapping and the authentication registration; references `Aspire.Azure.Data.Tables`.
- `src/Players/HexMaster.Battleship.Players.Tests/`: replaces `UnitTest1.cs` with handler, domain model and token service tests (Moq and Bogus added per the house testing standard) and an integration test fixture that runs the API against Azurite.
- `src/Aspire/.../HexMaster.Battleship.Aspire.AppHost/`: references `Aspire.Hosting.Azure.Storage`; `AppHost.cs` adds the storage resource (emulator locally) and passes the tables resource to the Players API.
- `src/Aspire/.../HexMaster.Battleship.Aspire.ServiceDefaults/`: references `Microsoft.AspNetCore.Authentication.JwtBearer`; gains `AddBattleshipAuthentication`.
- New `src/Core/HexMaster.Battleship.Core/`: `ICommandHandler`, `IQueryHandler`, `DomainModel<TId>`, `DomainModelState`, `DomainException`. Added to `src/Battleship.slnx` together with the Data project.
- `README.md`: gains a section on running with Azurite and setting the signing key with `dotnet user-secrets`.
- No frontend change. The Games API is untouched apart from the authentication helper becoming available to it; no Games endpoint requires authorization yet.
