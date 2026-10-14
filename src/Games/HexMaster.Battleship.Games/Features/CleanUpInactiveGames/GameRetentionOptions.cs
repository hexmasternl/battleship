namespace HexMaster.Battleship.Games.Features.CleanUpInactiveGames;

// How long a game is kept after its last activity. Bound from Games:Retention.
public sealed class GameRetentionOptions
{
    public const string ConfigurationSection = "Games:Retention";

    public int InactiveDays { get; set; } = 90;

    public TimeSpan Retention => TimeSpan.FromDays(InactiveDays);
}
