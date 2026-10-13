namespace HexMaster.Battleship.Core.DomainModels;

// The lifecycle of a domain model. The repository reads it to decide what to write:
// New inserts, Modified updates, Deleted deletes, Pristine and Touched write nothing (ADR 0013).
public enum DomainModelState
{
    New,
    Pristine,
    Touched,
    Modified,
    Deleted,
}
