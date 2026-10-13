# Proposal

> Tracks GitHub issue [hexmasternl/battleship#29](https://github.com/hexmasternl/battleship/issues/29).

## Why

The testing ADR (xUnit, Moq, Bogus) requires test data to come from Bogus-backed factories in each test project's `Factories/` folder. A few tests still build domain objects and integration events by hand, with hard-coded values such as `TimeSpan.FromDays(90)`, `"Captain Nemo"` and `"Admiral Nelson"`. They repeat construction logic the factories should own and miss the variation Bogus provides.

## What Changes

- `UserFactory` gains a builder, `New(...)`, that creates a fresh `User` through `User.Create` with Bogus defaults and optional overrides.
- `RefreshTokenFactory` gains `New(...)`, which goes through `RefreshToken.Create` with a Bogus hash and the default 90-day lifetime.
- `IntegrationEventFactory.PlayerCreated` accepts an optional `playerName` override.
- These tests use the factories instead of constructing objects directly:
  - `TableRepositoryTests` (4 sites)
  - `IntegrationEventsDeliveryTests` (1 site)
- Literal display names (`"Admiral Nelson"`) in these tests come from `UserFactory.DisplayName()`:
  - `CreateTokensCommandHandlerTests`
  - `UsersApiTests`
- Deliberate boundary inputs stay literal: `null`, a 41-character name, an unknown token string.
- The domain-model tests (`UserTests`, `RefreshTokenTests`) keep calling `Create` / `Reconcile` directly, because those methods are what they test.
- Test code only, with no production or behaviour change.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None (`skip_specs`). This is a test-only refactor.

## Impact

- **Test code**:
  - `src/Users/HexMaster.Battleship.Users.Tests`: `Factories/UserFactory.cs`, `Factories/RefreshTokenFactory.cs`, `Storage/TableRepositoryTests.cs`, `Features/CreateTokensCommandHandlerTests.cs`, `Integration/UsersApiTests.cs`
  - `src/Shared/HexMaster.Battleship.IntegrationEvents.Tests`: `Factories/IntegrationEventFactory.cs`, `Integration/IntegrationEventsDeliveryTests.cs`
- **Production code**: none.
- **Dependencies**: none new; Bogus is already referenced.
- **Related**: issue #26 moves `DomainModelTests` into a new Core test project. Its factories, if any, follow the same convention.
