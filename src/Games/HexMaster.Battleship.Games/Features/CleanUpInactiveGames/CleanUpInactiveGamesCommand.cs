namespace HexMaster.Battleship.Games.Features.CleanUpInactiveGames;

// Removes every game without activity for longer than the retention period. Sent by the schedule,
// never by a player.
public sealed record CleanUpInactiveGamesCommand;
