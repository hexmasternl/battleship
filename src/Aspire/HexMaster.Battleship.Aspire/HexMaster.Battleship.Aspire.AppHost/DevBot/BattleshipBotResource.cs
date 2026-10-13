namespace HexMaster.Battleship.Aspire.AppHost.DevBot;

/// <summary>
/// The local development bot opponent: the <c>HexMaster.Battleship.DevBot</c> worker, run like any
/// project, with its own commands and its state shown as resource properties. Local runs only.
/// </summary>
public sealed class BattleshipBotResource(string name) : ProjectResource(name)
{
    /// <summary>The Dapr app id of the bot's sidecar, which gives it its own queue on the topic.</summary>
    public const string DaprAppId = "dev-bot";

    internal const string IntelligenceProperty = "devbot.intelligence";
    internal const string AutoJoinProperty = "devbot.auto-join";
    internal const string GamesProperty = "devbot.games";

    internal const string IntelligenceVariable = "DevBot__Intelligence";
    internal const string JoinGracePeriodVariable = "DevBot__JoinGracePeriod";
    internal const string ShotDelayVariable = "DevBot__ShotDelay";

    /// <summary>The worker's HTTP endpoint, where the AppHost reaches its admin API.</summary>
    public EndpointReference HttpEndpoint => field ??= new EndpointReference(this, "http");
}
