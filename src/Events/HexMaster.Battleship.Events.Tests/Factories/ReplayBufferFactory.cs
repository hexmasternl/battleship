using HexMaster.Battleship.Events.Replay;
using Microsoft.Extensions.Options;

namespace HexMaster.Battleship.Events.Tests.Factories;

public static class ReplayBufferFactory
{
    public static ReplayBuffer Create(int capacity = 1000, TimeSpan? maxAge = null, TimeProvider? timeProvider = null) =>
        new(
            Options.Create(new ReplayOptions { Capacity = capacity, MaxAge = maxAge ?? TimeSpan.FromMinutes(2) }),
            timeProvider ?? TimeProvider.System);
}
