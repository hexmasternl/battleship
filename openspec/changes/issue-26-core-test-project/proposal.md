# Proposal

> Tracks GitHub issue [hexmasternl/battleship#26](https://github.com/hexmasternl/battleship/issues/26).

## Why

`HexMaster.Battleship.Core` has no test project of its own. Its only tests (`DomainModelTests`) live in `Players.Tests/Core/`, so the shared kernel's coverage depends on one consumer module. The testing ADR asks every project for a mirrored test project that meets the 80% coverage floor on its own.

## What Changes

- Add `src/Core/HexMaster.Battleship.Core.Tests`. It uses xUnit, Moq, Bogus and coverlet, with the same package versions as `Players.Tests` and `PrivateAssets="all"` on the collector and runner. It references only `HexMaster.Battleship.Core` and is listed under `/Core/` in `Battleship.slnx`.
- Move `DomainModelTests` (7 tests and its private `SampleModel`) from `Players.Tests/Core/` to `Core.Tests/DomainModels/`, with the namespace `HexMaster.Battleship.Core.Tests.DomainModels`.
- Delete the `Core/` folder from `Players.Tests`.
- Update the README repository layout to list the Core test project.
- No production code or behaviour changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. This is a test-layout change with no spec-level behaviour (`skip_specs: true`).

## Impact

- **New project:** `src/Core/HexMaster.Battleship.Core.Tests`.
- **Changed:** `src/Players/HexMaster.Battleship.Players.Tests` (one file removed), `src/Battleship.slnx`, `README.md`.
- **Dependencies:** none new. The project reuses the package set already in the repository.
