namespace HexMaster.Battleship.DevBot.Strategies;

// The playing strengths the bot offers, weakest first. Each maps to one IShotStrategy.
public enum BotIntelligence
{
    Random,
    HuntAndTarget,
    HuntWithParity,
    ProbabilityDensity,
}
