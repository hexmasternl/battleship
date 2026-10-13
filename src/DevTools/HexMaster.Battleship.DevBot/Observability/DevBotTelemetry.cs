using System.Diagnostics;

namespace HexMaster.Battleship.DevBot.Observability;

// Named after the assembly, so the ServiceDefaults wildcard registers it (ADR 0008).
internal static class DevBotTelemetry
{
    public const string Name = "HexMaster.Battleship.DevBot";

    public const string GameCodeAttribute = "battleship.game.code";
    public const string OutcomeAttribute = "battleship.devbot.outcome";

    public static readonly ActivitySource ActivitySource = new(Name);
}
