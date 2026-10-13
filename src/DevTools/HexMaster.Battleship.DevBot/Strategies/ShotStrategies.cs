namespace HexMaster.Battleship.DevBot.Strategies;

// Maps a preset to its strategy. The strategies hold no state, so one instance each is shared.
public static class ShotStrategies
{
    private static readonly RandomStrategy Random = new();
    private static readonly HuntAndTargetStrategy HuntAndTarget = new();
    private static readonly HuntWithParityStrategy HuntWithParity = new();

    public static IShotStrategy For(BotIntelligence intelligence) => intelligence switch
    {
        BotIntelligence.Random => Random,
        BotIntelligence.HuntAndTarget => HuntAndTarget,
        BotIntelligence.HuntWithParity => HuntWithParity,
        _ => throw new ArgumentOutOfRangeException(nameof(intelligence), intelligence, "Unknown intelligence preset."),
    };
}
