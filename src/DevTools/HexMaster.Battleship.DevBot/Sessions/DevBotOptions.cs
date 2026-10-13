using HexMaster.Battleship.DevBot.Strategies;

namespace HexMaster.Battleship.DevBot.Sessions;

// Bound from the DevBot section; the AppHost sets these as DevBot__* environment variables.
public sealed class DevBotOptions
{
    public const string ConfigurationSection = "DevBot";

    public BotIntelligence Intelligence { get; set; } = BotIntelligence.HuntAndTarget;

    public bool AutoJoin { get; set; } = true;

    public TimeSpan JoinGracePeriod { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan ShotDelay { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan ReconcileInterval { get; set; } = TimeSpan.FromSeconds(15);
}
