# Proposal

> Tracks GitHub issue [hexmasternl/battleship#27](https://github.com/hexmasternl/battleship/issues/27).

## Why

The solution-structure standard names data projects `{Module}.Data.{StorageType}`, so the storage technology shows in the project name and a second store can be added alongside it. The Players data project, which holds only Azure Table Storage code, is called `HexMaster.Battleship.Players.Data` with no storage suffix.

## What Changes

- **Rename the project.** `src/Players/HexMaster.Battleship.Players.Data/` becomes `src/Players/HexMaster.Battleship.Players.Data.TableStorage/`. The `.csproj` file, assembly name and root namespace follow, and the move uses `git mv` so file history is kept.
- **Rename the namespaces.** `HexMaster.Battleship.Players.Data.Entities` and `.Repositories` become `HexMaster.Battleship.Players.Data.TableStorage.Entities` and `.Repositories`. `PlayersDataRegistration` keeps its `Microsoft.Extensions.Hosting` namespace and the `AddPlayersData()` name, so callers don't change.
- **Update the references.** `src/Battleship.slnx`, the Players API and Players test project references, and the test `using` directives point at the new name.
- **Update the docs.** The README's repository layout names the storage suffix.
- There is no behaviour change: same tables, same connection name (`tables`), same AppHost wiring.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. This is a rename only, so `skip_specs: true` is set.

## Impact

- **Code:**
  - `src/Players/HexMaster.Battleship.Players.Data/**`: the five source files and the csproj move.
  - `src/Players/HexMaster.Battleship.Players.Api/HexMaster.Battleship.Players.Api.csproj`: the reference.
  - `src/Players/HexMaster.Battleship.Players.Tests/HexMaster.Battleship.Players.Tests.csproj`: the reference.
  - `Integration/PlayersApiTests.cs` and `Storage/TableRepositoryTests.cs`: the `using` directives.
  - `src/Battleship.slnx`
- **Docs:** README repository layout.
- **APIs, data and dependencies:** none. The Azure tables `users` and `refreshtokens` and the stored data are untouched.
- **Coordination:** change `issue-13-create-new-game` plans a new `HexMaster.Battleship.Games.Data` project, and its design says it "mirrors #7's Players layout". It should create that project as `HexMaster.Battleship.Games.Data.TableStorage` instead, whichever change lands first. This change does not edit issue-13.
- **Historical references:** archived and completed changes (`issue-7-user-identification`) keep the old name in their text, because they describe what was built at the time.
