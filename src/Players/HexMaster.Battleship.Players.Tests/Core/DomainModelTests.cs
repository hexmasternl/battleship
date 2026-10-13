using HexMaster.Battleship.Core.DomainModels;

namespace HexMaster.Battleship.Players.Tests.Core;

public class DomainModelTests
{
    [Fact]
    public void NewModel_StaysNew_WhenAValueChanges()
    {
        var model = SampleModel.New("before");

        model.SetName("after");

        Assert.Equal(DomainModelState.New, model.State);
        Assert.Equal("after", model.Name);
    }

    [Fact]
    public void NewModel_StaysNew_WhenAnOperationChangesNothing()
    {
        var model = SampleModel.New("same");

        model.SetName("same");

        Assert.Equal(DomainModelState.New, model.State);
    }

    [Fact]
    public void PristineModel_BecomesTouched_WhenAnOperationChangesNothing()
    {
        var model = SampleModel.Pristine("same");

        model.SetName("same");

        Assert.Equal(DomainModelState.Touched, model.State);
    }

    [Fact]
    public void PristineModel_BecomesModified_WhenAValueChanges()
    {
        var model = SampleModel.Pristine("before");

        model.SetName("after");

        Assert.Equal(DomainModelState.Modified, model.State);
        Assert.Equal("after", model.Name);
    }

    [Fact]
    public void ModifiedModel_IsNotDowngradedToTouched()
    {
        var model = SampleModel.Pristine("before");
        model.SetName("after");

        model.SetName("after");

        Assert.Equal(DomainModelState.Modified, model.State);
    }

    [Fact]
    public void DeletedModel_RejectsChange()
    {
        var model = SampleModel.Pristine("before");
        model.Delete();

        Assert.Equal(DomainModelState.Deleted, model.State);
        Assert.Throws<DomainException>(() => model.SetName("after"));
        Assert.Throws<DomainException>(model.Delete);
        Assert.Equal("before", model.Name);
    }

    [Theory]
    [InlineData(DomainModelState.Touched)]
    [InlineData(DomainModelState.Modified)]
    [InlineData(DomainModelState.Deleted)]
    public void Model_CannotStartInAnotherStateThanNewOrPristine(DomainModelState state)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleModel("name", state));
    }

    private sealed class SampleModel(string name, DomainModelState state) : DomainModel<Guid>(Guid.NewGuid(), state)
    {
        public string Name { get; private set; } = name;

        public static SampleModel New(string name) => new(name, DomainModelState.New);

        public static SampleModel Pristine(string name) => new(name, DomainModelState.Pristine);

        public void SetName(string value) => Change(Name, value, v => Name = v);
    }
}
