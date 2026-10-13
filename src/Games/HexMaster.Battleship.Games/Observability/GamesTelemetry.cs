using System.Diagnostics;

namespace HexMaster.Battleship.Games.Observability;

// The tracing source of the Games module and the attributes it writes. Use cases get their span
// from the handler decorator; handlers add these attributes to Activity.Current.
public static class GamesTelemetry
{
    public const string Name = "HexMaster.Battleship.Games";

    public static readonly ActivitySource ActivitySource = new(Name);

    // Span attributes. Game and player ids and game codes may appear on spans and in logs, never
    // in metric tags.
    public const string GameIdAttribute = "game.id";
    public const string GameCodeAttribute = "game.code";
    public const string PlayerIdAttribute = "player.id";
    public const string CodeAttemptsAttribute = "game.code_attempts";
}
