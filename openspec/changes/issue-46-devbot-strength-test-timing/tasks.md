# Tasks

## 1. Strength test

- [x] 1.1 In `src/DevTools/HexMaster.Battleship.DevBot.Tests/Strategies/StrategyStrengthTests.cs`, remove the `Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), ...)` assertion. Keep the `Stopwatch`, the per-preset `output.WriteLine` lines and the `Elapsed:` output line, the 200 seeded fleets, the `seed + 1000` shot seeds and the three ranking assertions unchanged. Verify `dotnet test src/DevTools/HexMaster.Battleship.DevBot.Tests --logger "console;verbosity=detailed"` passes and the output still shows each preset's average and the elapsed time.

## 2. Integration check

- [x] 2.1 Run `dotnet test src/DevTools/HexMaster.Battleship.DevBot.Tests --collect:"XPlat Code Coverage"` three times and `dotnet test src/Battleship.slnx --collect:"XPlat Code Coverage"` twice, the runs that failed before. Verify every run passes, and record the elapsed times reported by the test in the pull request.
