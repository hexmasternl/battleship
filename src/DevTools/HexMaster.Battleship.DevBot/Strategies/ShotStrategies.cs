namespace HexMaster.Battleship.DevBot.Strategies;

// Maps a preset to its strategy. The strategies hold no state, so one instance each is shared.
public static class ShotStrategies
{
    private static readonly RandomStrategy Random = new();

    public static IShotStrategy For(BotIntelligence intelligence) => intelligence switch
    {
        BotIntelligence.Random => Random,
        _ => throw new ArgumentOutOfRangeException(nameof(intelligence), intelligence, "Unknown intelligence preset."),
    };
}
