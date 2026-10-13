# Tasks

## 1. Core test project

- [x] 1.1 Create `src/Core/HexMaster.Battleship.Core.Tests/HexMaster.Battleship.Core.Tests.csproj`:
  - net10.0, nullable, implicit usings, `IsPackable=false`, a global `Using Include="Xunit"`
  - packages Bogus 35.6.5, coverlet.collector 10.1.0, Microsoft.Extensions.Diagnostics.Testing 10.10.0, Microsoft.NET.Test.Sdk 18.11.0, Moq 4.21.0, xunit 2.9.3 and xunit.runner.visualstudio 4.0.1 (the versions the other test projects use), with `PrivateAssets="all"` on the collector and the runner
  - a single `ProjectReference` to `HexMaster.Battleship.Core`

  Add it to the `/Core/` folder in `src/Battleship.slnx`. Verify `dotnet build src/Battleship.slnx` succeeds.

## 2. Move the Core tests

- [x] 2.1 Move `Players.Tests/Core/DomainModelTests.cs` to `Core.Tests/DomainModels/DomainModelTests.cs`, change its namespace to `HexMaster.Battleship.Core.Tests.DomainModels`. Move `Players.Tests/Core/InstrumentedHandlerTests.cs` (it also tests Core types) to `Core.Tests/Observability/` with its own `ActivityCollector`, and delete the now-empty `Players.Tests/Core/` folder. Verify that `dotnet test src/Core/HexMaster.Battleship.Core.Tests` runs the moved tests green, and that `dotnet test src/Players/HexMaster.Battleship.Players.Tests` still passes without them.
- [x] 2.2 Measure coverage from the Core tests alone: `dotnet test src/Core/HexMaster.Battleship.Core.Tests --collect:"XPlat Code Coverage"`. Verify that `HexMaster.Battleship.Core` line coverage is at least 80%. If it isn't, add tests for the uncovered members and record the figure in the pull request.

## 3. Documentation

- [x] 3.1 Update the README repository layout so the `Core/` line mentions the `Core.Tests` project. Verify the layout matches `ls src/Core`.

## 4. Integration check

- [x] 4.1 Run `dotnet test src/Battleship.slnx` and verify every test project passes, with the same total test count as before the move.
