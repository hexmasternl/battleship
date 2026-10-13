using System.Diagnostics.Metrics;

namespace HexMaster.Battleship.Players.Observability;

// The business metrics of the Players module. No tag carries a user id, token or display name.
public sealed class PlayersMetrics
{
    public const string TokensIssuedName = "players.tokens.issued";
    public const string TokensRefreshedName = "players.tokens.refreshed";
    public const string TokensRefusedName = "players.tokens.refused";

    private readonly Counter<long> _tokensIssued;
    private readonly Counter<long> _tokensRefreshed;
    private readonly Counter<long> _tokensRefused;

    public PlayersMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(PlayersTelemetry.Name);
        _tokensIssued = meter.CreateCounter<long>(
            TokensIssuedName, unit: "{token}", description: "Token pairs issued to new anonymous users.");
        _tokensRefreshed = meter.CreateCounter<long>(
            TokensRefreshedName, unit: "{token}", description: "Token pairs issued in exchange for a refresh token.");
        _tokensRefused = meter.CreateCounter<long>(
            TokensRefusedName, unit: "{token}", description: "Refresh tokens refused, by reason.");
    }

    public void TokensIssued() => _tokensIssued.Add(1);

    public void TokensRefreshed() => _tokensRefreshed.Add(1);

    // reason is one of TokenRefusalReasons.
    public void TokenRefused(string reason) =>
        _tokensRefused.Add(1, new KeyValuePair<string, object?>(PlayersTelemetry.ReasonTag, reason));
}
