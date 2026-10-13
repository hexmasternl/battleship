using HexMaster.Battleship.DevBot.Strategies;

namespace HexMaster.Battleship.DevBot.Sessions;

// The settings that change while the bot runs: the preset and auto-join. Every session reads them
// on each decision, so a change applies from the next shot or the next created game.
public sealed class BotSettings(DevBotOptions options)
{
    private volatile BotIntelligence intelligence = options.Intelligence;
    private volatile bool autoJoin = options.AutoJoin;

    public BotIntelligence Intelligence
    {
        get => intelligence;
        set => intelligence = Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown intelligence preset.");
    }

    public bool AutoJoin
    {
        get => autoJoin;
        set => autoJoin = value;
    }

    public TimeSpan JoinGracePeriod { get; } = options.JoinGracePeriod;

    public TimeSpan ShotDelay { get; } = options.ShotDelay;

    public TimeSpan ReconcileInterval { get; } = options.ReconcileInterval;
}
