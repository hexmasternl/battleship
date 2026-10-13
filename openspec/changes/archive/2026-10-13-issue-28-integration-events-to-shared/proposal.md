# Proposal

> Tracks GitHub issue [hexmasternl/battleship#28](https://github.com/hexmasternl/battleship/issues/28).

## Why

The integration-event catalog and its Dapr sender sit under `src/Core/`, which ADR 0002 reserves for the domain-agnostic shared kernel. The catalog is a cross-module contract that knows about Players and Games, so it belongs under `src/Shared/`. In the same move, the dependency from ServiceDefaults to the catalog has to go: the platform layer should not depend on a contract project. A local edit already removes that reference, and it currently breaks the build because `AddServiceDefaults()` still calls `AddIntegrationEvents()`.

## What Changes

- Move `HexMaster.Battleship.IntegrationEvents` and `HexMaster.Battleship.IntegrationEvents.Tests` from `src/Core/` to `src/Shared/`, under a new `/Shared/` solution folder. Project names, assembly names and namespaces stay the same.
- ServiceDefaults no longer references IntegrationEvents, and `AddServiceDefaults()` no longer calls `AddIntegrationEvents()`.
- Each API that publishes integration events (Players and Games) references IntegrationEvents and calls `builder.AddIntegrationEvents()` itself. The Events API only consumes, so it does not register a sender.
- The registration tests change from "AddServiceDefaults registers the sender" to "AddIntegrationEvents registers the sender", plus a check that `AddServiceDefaults()` alone no longer does.
- Update every path that points at the old location:
  - the relative `ProjectReference`s and the linked `AspireConstants.cs`
  - the README link
  - the frontend comment in `realtime-event-types.ts`
  - the module README

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `integration-events`: the requirement *Every backend service can publish without its own wiring* changes. A service now opts in with one registration call instead of getting the sender implicitly from the shared service defaults. The single shared definition of the component and topic names is unchanged.

## Impact

- **Code:**
  - `src/Core/HexMaster.Battleship.IntegrationEvents*` moves to `src/Shared/`
  - `ServiceDefaults/Extensions.cs` and its `.csproj`
  - the `Players.Api` and `Games.Api` `Program.cs` files and `.csproj` files
  - `src/Battleship.slnx`
- **Tests:** `IntegrationEventsRegistrationTests` is rewritten. All other tests move unchanged.
- **Docs:** `README.md` (the link and the repository layout), the IntegrationEvents `README.md` ("Publishing" section), and the comment in `src/Frontend/src/app/events/realtime-event-types.ts`.
- **Other changes in flight:** `issue-16`, `issue-17`, `issue-18` and `issue-24` refer to `src/Core/HexMaster.Battleship.IntegrationEvents`. Whichever of them is applied after this change uses the `src/Shared/` path.
- **APIs and behaviour:** no endpoint or event contract changes.
