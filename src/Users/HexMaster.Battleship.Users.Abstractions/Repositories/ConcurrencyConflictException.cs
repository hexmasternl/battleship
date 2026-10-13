namespace HexMaster.Battleship.Users.Abstractions.Repositories;

// Thrown by a repository when a stored record changed between reading and writing it.
public sealed class ConcurrencyConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);
