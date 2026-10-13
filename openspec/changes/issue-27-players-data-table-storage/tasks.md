# Tasks

## 1. Rename the project

- [x] 1.1 Delete the local `bin/` and `obj/` under `src/Players/HexMaster.Battleship.Players.Data`. Run `git mv src/Players/HexMaster.Battleship.Players.Data src/Players/HexMaster.Battleship.Players.Data.TableStorage`, then `git mv` the csproj inside it to `HexMaster.Battleship.Players.Data.TableStorage.csproj`. Verify that `git status` shows the files as renamed, not as deleted and added.
- [x] 1.2 Change the namespaces in `Entities/*.cs` and `Repositories/*.cs`, and the `using` in `PlayersDataRegistration.cs`, to `HexMaster.Battleship.Players.Data.TableStorage.*`. Keep `PlayersDataRegistration` in `Microsoft.Extensions.Hosting` with `AddPlayersData()` unchanged. Verify that `grep -rn "Players\.Data\b" src --include=*.cs` returns nothing outside `.TableStorage` names.

## 2. Update references

- [x] 2.1 Point `src/Battleship.slnx`, `Players.Api.csproj` and `Players.Tests.csproj` at the new path. Update the `using` directives in `Players.Tests/Integration/PlayersApiTests.cs` and `Players.Tests/Storage/TableRepositoryTests.cs`. Verify that `dotnet build src/Battleship.slnx` succeeds with no new warnings.
- [ ] 2.2 Run `dotnet test src/Battleship.slnx` with Azurite running, so the table repository and API tests execute rather than skip. Verify that every test passes and that the skipped count is 0 for the Players storage tests.

## 3. Documentation

- [x] 3.1 Update the README's repository layout entry for `Players/` to name `Data.TableStorage`. Verify by searching the README for the new name.

## 4. Integration check

- [ ] 4.1 Start the AppHost with `aspire run`. Create a player from the landing page, then refresh the page so the stored refresh token is used. Verify that the Players API is healthy on the dashboard, that the player is recognised after the refresh, and that the `users` and `refreshtokens` tables in Azurite still hold their rows.
