# Tasks

## 1. Move the projects

- [ ] 1.1 Move the projects and update every reference to them (design D3):
  - `git mv` both `src/Core/HexMaster.Battleship.IntegrationEvents` and `src/Core/HexMaster.Battleship.IntegrationEvents.Tests` to `src/Shared/`
  - in `Battleship.slnx`, move their entries into a new `/Shared/` folder
  - update the ServiceDefaults `ProjectReference` path (temporarily, until task 2.1 removes it)
  - update the links in `README.md` (the integration events link and the repository layout)
  - update the comment path in `src/Frontend/src/app/events/realtime-event-types.ts`

  Verify that `dotnet build src/Battleship.slnx` and `dotnet test src/Battleship.slnx` pass, and that `git log --follow` on a moved file shows its history.

## 2. Decouple ServiceDefaults

- [ ] 2.1 Remove the IntegrationEvents `ProjectReference` from ServiceDefaults and the `AddIntegrationEvents()` call from `AddServiceDefaults()` (design D1). Add the reference and `builder.AddIntegrationEvents()` right after `AddServiceDefaults()` in `Players.Api` and `Games.Api`. Leave `Events.Api` untouched (design D2). Verify that `dotnet build src/Battleship.slnx` passes.
- [ ] 2.2 Rewrite `IntegrationEventsRegistrationTests` for the opt-in contract: the sender is registered by `AddIntegrationEvents()`, `AddServiceDefaults()` alone registers no sender, and one singleton `DaprClient` remains when both are called (design D4). Update the IntegrationEvents `README.md` "Publishing" section and the comment in `IntegrationEventsExtensions.cs` to describe the opt-in call. Verify that `dotnet test src/Battleship.slnx` passes with the new tests.

## 3. Cross-check open changes

- [ ] 3.1 Search `openspec/changes/` (excluding `archive/`) for `src/Core/HexMaster.Battleship.IntegrationEvents`. List the hits in the pull request description so whoever applies those changes uses the new path. Do not edit other changes' artifacts. Verify the list is in the PR body.

## 4. Integration check

- [ ] 4.1 Run `aspire run`. Confirm that all resources reach Running, including the Events API without a registered sender. With a stream open for player `A`, publish the README "Try it by hand" event and confirm that it arrives. Verify the result and record it in the pull request.
