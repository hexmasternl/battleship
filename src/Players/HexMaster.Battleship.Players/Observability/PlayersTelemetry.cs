using System.Diagnostics;

namespace HexMaster.Battleship.Players.Observability;

// The tracing source of the Players module and the attributes and tags it writes. Use cases get
// their span from the handler decorator; the handlers add these attributes to Activity.Current.
public static class PlayersTelemetry
{
    public const string Name = "HexMaster.Battleship.Players";

    public static readonly ActivitySource ActivitySource = new(Name);

    // Span attributes. The internal user id may appear on spans and in logs, never in metric tags.
    public const string PlayerIdAttribute = "player.id";
    public const string RefusalReasonAttribute = "token.refusal_reason";

    // Metric tag of players.tokens.refused.
    public const string ReasonTag = "reason";
}

// The closed set of reasons a refresh token is refused, as span attribute and metric tag values.
public static class TokenRefusalReasons
{
    public const string UnknownToken = "unknown_token";
    public const string ExpiredToken = "expired_token";
    public const string TokenUsedAlready = "token_used_already";
    public const string UnknownUser = "unknown_user";
    public const string RevokedUser = "revoked_user";
    public const string Other = "other";

    // Maps the reason a TokenRefusedException carries to its snake_case value; anything outside
    // the known set becomes "other", so the tag set stays closed.
    public static string Of(string reason) => reason switch
    {
        "unknown token" => UnknownToken,
        "expired token" => ExpiredToken,
        "token used already" => TokenUsedAlready,
        "unknown user" => UnknownUser,
        "revoked user" => RevokedUser,
        _ => Other
    };
}
