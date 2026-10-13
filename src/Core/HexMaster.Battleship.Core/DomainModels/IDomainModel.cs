namespace HexMaster.Battleship.Core.DomainModels;

// What every domain model exposes, so repositories and handlers can work with the
// interfaces in a module's Abstractions project.
public interface IDomainModel<out TId>
{
    TId Id { get; }

    DomainModelState State { get; }

    void Delete();
}
