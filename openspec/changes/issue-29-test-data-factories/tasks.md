# Tasks

## 1. Players test factories

- [ ] 1.1 Add `UserFactory.New(string? displayName = null, DateTimeOffset? now = null)`, which returns a `User` built through `User.Create` with a Bogus display name and timestamp. Verify it builds and that a factory test asserts the overrides are honoured.
- [ ] 1.2 Add `RefreshTokenFactory.New(DateTimeOffset now, string? hash = null, Guid? userId = null, TimeSpan? lifetime = null)`, which returns a `RefreshToken` built through `RefreshToken.Create` with a Bogus hash, a new user id and a 90-day default lifetime. Verify it builds and that a factory test asserts the defaults and overrides.

## 2. Players tests use the factories

- [ ] 2.1 Replace the `User.Create` calls at `Storage/TableRepositoryTests.cs:19,36` with `UserFactory.New(now: Now)`, and the `RefreshToken.Create` calls at lines 62 and 80 with `RefreshTokenFactory.New(Now, hash: hash)`. Verify with `dotnet test src/Players/HexMaster.Battleship.Players.Tests` (these tests skip without Azurite; run them with Azurite on the default ports or with `BATTLESHIP_TESTS_TABLES` set).
- [ ] 2.2 Replace the literal `"Admiral Nelson"` display names in `Features/CreateTokensCommandHandlerTests.cs` (lines 44, 71, 83) and `Integration/PlayersApiTests.cs` (line 27) with `UserFactory.DisplayName()`. Keep the deliberate boundary inputs (`null`, `new string('x', 41)`, `"never-issued"`). Verify that `dotnet test src/Players/HexMaster.Battleship.Players.Tests` passes.

## 3. IntegrationEvents tests use the factory

- [ ] 3.1 Add an optional `playerName` override to `IntegrationEventFactory.PlayerCreated`, and replace `new PlayerCreatedEvent(playerId, "Captain Nemo")` at `Integration/IntegrationEventsDeliveryTests.cs:81` with `IntegrationEventFactory.PlayerCreated(playerId)`. Assert against the factory-produced name instead of a literal. Verify that `dotnet test src/Core/HexMaster.Battleship.IntegrationEvents.Tests` passes.

## 4. Check

- [ ] 4.1 Run `dotnet test src/Battleship.slnx`. Then run `grep -rnE "(User|RefreshToken)\.Create\(|new (Player|Game)[A-Za-z]*Event\(" src --include=*.cs` restricted to `*.Tests` projects, excluding `Factories/`, `DomainModels/UserTests.cs` and `DomainModels/RefreshTokenTests.cs`. Verify that every test passes and the grep returns no matches.
