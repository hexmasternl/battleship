# Proposal

> Tracks GitHub issue [hexmasternl/battleship#46](https://github.com/hexmasternl/battleship/issues/46).

## Why

`StrategyStrengthTests.Presets_PlayedAgainstTheSameFleets_RankByAverageShotsToWin` asserts both that the bot presets rank correctly and that the run finishes in under 10 seconds. The ranking is deterministic because every fleet and every game is seeded, but the time limit depends on the machine: with coverage collection on, or when the whole solution runs in parallel, the run takes about 11 s and the test fails even though no code is wrong. A flaky test erodes trust in the suite and blocks pull request verification (seen on PR #45).

## What Changes

- Remove the `stopwatch.Elapsed < TimeSpan.FromSeconds(10)` assertion from the strength test.
- Keep the three ranking assertions (Random > HuntAndTarget > HuntWithParity ≥ ProbabilityDensity) on the same 200 seeded fleets and shot seeds.
- Keep writing each preset's average shots and the elapsed time to the test output, so slow runs stay visible without failing the build.
- No separate performance test is added. Assumed: the 10-second limit was a sanity guard, not a requirement; the `dev-opponent` spec sets no performance budget for the strategies, and the bot fires one shot per turn after a 1-second delay, so strategy speed is not on a user-visible path.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
<!-- none: test-only change, no spec-level behaviour changes (skip_specs: true) -->

## Impact

- `src/DevTools/HexMaster.Battleship.DevBot.Tests/Strategies/StrategyStrengthTests.cs`: one assertion removed, output kept.
- No production code, shot strategy, API or dependency changes.
- Out of scope: changes to the shot strategies themselves, and benchmark tooling.
