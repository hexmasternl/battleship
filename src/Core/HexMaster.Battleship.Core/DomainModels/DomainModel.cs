namespace HexMaster.Battleship.Core.DomainModels;

// Base class for every domain model: owns the identifier, the lifecycle state and its allowed
// transitions. Models contain only their own properties, validation and operations (ADR 0013).
public abstract class DomainModel<TId> : IDomainModel<TId>
{
    protected DomainModel(TId id, DomainModelState initialState)
    {
        if (initialState is not (DomainModelState.New or DomainModelState.Pristine))
        {
            throw new ArgumentOutOfRangeException(nameof(initialState), "A domain model starts as New or Pristine.");
        }

        Id = id;
        State = initialState;
    }

    public TId Id { get; }

    public DomainModelState State { get; private set; }

    public void Delete()
    {
        EnsureNotDeleted();
        State = DomainModelState.Deleted;
    }

    // Assigns the value when it differs from the current one. Touched never downgrades Modified,
    // and a New model stays New however often it changes.
    protected void Change<T>(T current, T value, Action<T> assign)
    {
        ArgumentNullException.ThrowIfNull(assign);
        EnsureNotDeleted();

        if (EqualityComparer<T>.Default.Equals(current, value))
        {
            if (State == DomainModelState.Pristine)
            {
                State = DomainModelState.Touched;
            }

            return;
        }

        assign(value);
        if (State != DomainModelState.New)
        {
            State = DomainModelState.Modified;
        }
    }

    private void EnsureNotDeleted()
    {
        if (State == DomainModelState.Deleted)
        {
            throw new DomainException("A deleted domain model cannot change.");
        }
    }
}
