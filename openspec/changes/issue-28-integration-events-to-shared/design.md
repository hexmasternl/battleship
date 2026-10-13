# Design

## Context

See `proposal.md` (Why). What the code has today:

- **The two projects** live in `src/Core/`, under the `/Core/` solution folder (`Battleship.slnx:9-10`).
- **The sender project** references `Events.Abstractions` (`..\..\Events\...`). It compile-links `AspireConstants.cs` from the ServiceDefaults folder (`..\..\Aspire\...`).
- **The test project** references the sender project, the AppHost and ServiceDefaults.
- **ServiceDefaults at HEAD** references the sender project, and `AddServiceDefaults()` calls `builder.AddIntegrationEvents()` (`Extensions.cs:27`). The uncommitted local edit removes the reference but not the call, so the build fails (CS1061).
- **Nothing outside the project injects `IIntegrationEventSender` yet.** Players will publish `player.*` events, and Games will publish `game.*` events (issues #13 to #19). The Events API only subscribes.
- **`IntegrationEventsRegistrationTests`** asserts that `AddServiceDefaults()` registers the sender and a single `DaprClient`.

## Goals / Non-Goals

**Goals:**
- `src/Core/` holds only the shared kernel.
- ServiceDefaults depends on no module or contract project.
- The build stays green at every step.

**Non-Goals:**
- Renaming namespaces or assemblies.
- Changing the catalog, the envelope or the sender's behaviour.
- Moving `AspireConstants` itself (it stays in ServiceDefaults and remains compile-linked).

## Decisions

### D1. Each publishing API calls `AddIntegrationEvents()`, not ServiceDefaults

ServiceDefaults is the platform layer: telemetry, health, resilience, discovery and auth plumbing. ADR 0002 has contracts depend on the platform, never the platform on contracts. Keeping the reference would mean every service, the Events API included, carries the Dapr sender and the whole event catalog whether it publishes or not.

The cost is one line per publishing API. Each API gets a `ProjectReference` to IntegrationEvents and calls `builder.AddIntegrationEvents()` right after `AddServiceDefaults()`. That line documents in each host that the service publishes, so an implicit capability becomes explicit.

This contradicts the current `integration-events` requirement *Every backend service can publish without its own wiring*. That requirement is modified (see the delta): one opt-in call, after which no further wiring is needed. The single-source rule for the component and topic names stays exactly as it was.

- **Alternative: keep the ServiceDefaults reference and set `skip_specs`.** That is the smallest diff, but it keeps the platform-on-contract dependency the issue is about, and the user's local edit already moves away from it.
- **Alternative: move `AddIntegrationEvents` into ServiceDefaults with a reflection or assembly scan.** That hides the dependency instead of removing it.

### D2. Players.Api and Games.Api register now, Events.Api does not

- **Players** owns `player.created.v1` and `player.updated.v1`, and the Players API is the host that will publish them.
- **Games** owns every `game.*` event. Its API registers now, so issue #13's first publishing handler only needs to inject the sender.
- **Events** consumes through its Dapr subscription and never publishes. It does not register a sender.

Today `AddDaprClient()` is reached through `AddIntegrationEvents()`. The Events API's subscription needs only `Dapr.AspNetCore`'s `MapSubscribeHandler` and `UseCloudEvents`, not a `DaprClient`. Removing the call from ServiceDefaults therefore doesn't affect it. The integration check (task 4.1) confirms this.

### D3. Move with `git mv` and keep the names

`git mv src/Core/HexMaster.Battleship.IntegrationEvents src/Shared/HexMaster.Battleship.IntegrationEvents` (and the same for the tests) keeps the history. Relative paths inside both `.csproj` files stay valid, because `src/Shared/` is at the same depth as `src/Core/`: `..\..\Events\...` and `..\..\Aspire\...` still resolve.

The paths that do change:
- the solution file entries
- references from other projects (ServiceDefaults at HEAD, the two APIs, and the test project's own references, which are depth-neutral)
- documentation links

### D4. The registration tests follow the new contract

`IntegrationEventsRegistrationTests` changes as follows:
- `AddServiceDefaults_RegistersTheDaprSender` becomes `AddIntegrationEvents_RegistersTheDaprSender`.
- A new test, `AddServiceDefaults_AloneRegistersNoSender`, matches the new spec scenario.
- The single-`DaprClient` test calls `AddServiceDefaults()` and then `AddIntegrationEvents()`.

The test project keeps its ServiceDefaults reference so it can verify that the two compose.

## Risks / Trade-offs

- [In-flight changes (#16, #17, #18, #24) name `src/Core/...` paths] → The path is the only difference and the project names are the same. Whoever applies those changes after this one uses `src/Shared/`. The proposal records this. Their spec content is unaffected.
- [A future service forgets `AddIntegrationEvents()` and fails at runtime on a missing `IIntegrationEventSender`] → DI fails fast at the first resolution, and the error names the interface. The IntegrationEvents README "Publishing" section states the opt-in.
- [The user's uncommitted `.csproj` edit overlaps task 2.1] → The implementation happens in its own worktree from `development`, where HEAD still has the reference. The user's local edit stays theirs to discard once this change merges.
