namespace HexMaster.Battleship.DevBot.Admin;

// The internal admin API the AppHost calls for the dashboard commands and properties. It is not
// routed by the gateway, so the browser cannot reach it.

// PUT /admin/intelligence. The preset by name, in any casing.
public sealed record SetIntelligenceRequest(string? Intelligence);

// PUT /admin/auto-join.
public sealed record SetAutoJoinRequest(bool? Enabled);

// POST /admin/games/{code}.
public sealed record JoinGameAdminResponse(string GameCode);

// GET /admin/state, and the answer of every change.
public sealed record AdminStateResponse(string Intelligence, bool AutoJoin, IReadOnlyList<string> Games, Guid? UserId);
