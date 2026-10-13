namespace HexMaster.Battleship.Core.DomainModels;

// Thrown when an operation would break an invariant of a domain model.
public class DomainException(string message) : Exception(message);
