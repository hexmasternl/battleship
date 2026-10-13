using System.Diagnostics;
using System.Diagnostics.Metrics;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Core.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace HexMaster.Battleship.Core.Tests.Observability;

public class InstrumentedHandlerTests
{
    private readonly IMeterFactory _meterFactory =
        new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();

    [Fact]
    public async Task Command_RunsInsideASpanNamedAfterItsUseCase()
    {
        using var activities = new ActivityCollector();
        var handler = Decorate(new SampleCommandHandler());

        var result = await handler.Handle(new PlaceShipCommand(3), CancellationToken.None);

        Assert.Equal(6, result);
        var span = activities.Single("PlaceShip");
        Assert.Equal(ActivityKind.Internal, span.Kind);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal("PlaceShip", span.GetTagItem(CoreTelemetry.UseCaseAttribute));
        Assert.Equal(CoreTelemetry.CommandKind, span.GetTagItem(CoreTelemetry.UseCaseKindAttribute));
        Assert.Equal(CoreTelemetry.Name, span.Source.Name);
    }

    [Fact]
    public async Task Command_HandlerSeesTheUseCaseSpanAsCurrent()
    {
        using var activities = new ActivityCollector();
        var inner = new SampleCommandHandler();

        await Decorate(inner).Handle(new PlaceShipCommand(1), CancellationToken.None);

        Assert.Equal("PlaceShip", inner.CurrentActivityName);
    }

    [Fact]
    public async Task FailingCommand_EndsTheSpanInErrorRecordsTheExceptionAndRethrows()
    {
        using var activities = new ActivityCollector();
        var failure = new InvalidOperationException("The board is full.");
        var logs = new FakeLogCollector();
        var handler = Decorate(new SampleCommandHandler(failure), new FakeLogger(logs));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new PlaceShipCommand(1), CancellationToken.None));

        Assert.Same(failure, thrown);
        var span = activities.Single("PlaceShip");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(failure.Message, span.StatusDescription);
        var exception = Assert.Single(span.Events, e => e.Name == "exception");
        Assert.Contains(exception.Tags, t => t.Key == "exception.type" && Equals(t.Value, typeof(InvalidOperationException).FullName));
        var log = Assert.Single(logs.GetSnapshot());
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.Contains("PlaceShip", log.Message);
    }

    [Fact]
    public async Task Command_RecordsItsDurationWithASuccessOutcome()
    {
        using var duration = CollectDuration();

        await Decorate(new SampleCommandHandler()).Handle(new PlaceShipCommand(1), CancellationToken.None);

        var measurement = Assert.Single(duration.GetMeasurementSnapshot());
        Assert.True(measurement.Value >= 0);
        Assert.Equal("PlaceShip", measurement.Tags[CoreTelemetry.UseCaseTag]);
        Assert.Equal(CoreTelemetry.SuccessOutcome, measurement.Tags[CoreTelemetry.OutcomeTag]);
    }

    [Fact]
    public async Task FailingCommand_RecordsItsDurationWithAFailureOutcome()
    {
        using var duration = CollectDuration();
        var handler = Decorate(new SampleCommandHandler(new InvalidOperationException("boom")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new PlaceShipCommand(1), CancellationToken.None));

        var measurement = Assert.Single(duration.GetMeasurementSnapshot());
        Assert.Equal(CoreTelemetry.FailureOutcome, measurement.Tags[CoreTelemetry.OutcomeTag]);
    }

    [Fact]
    public async Task CommandWithoutResult_RunsInsideItsUseCaseSpan()
    {
        using var activities = new ActivityCollector();
        var inner = new SampleVoidCommandHandler();
        var handler = new InstrumentedCommandHandler<FireCommand>(inner, new CoreMetrics(_meterFactory), NullLogger.Instance);

        await handler.Handle(new FireCommand(), CancellationToken.None);

        Assert.True(inner.Handled);
        Assert.Equal(ActivityStatusCode.Ok, activities.Single("Fire").Status);
    }

    [Fact]
    public async Task Query_RunsInsideASpanOfKindQuery()
    {
        using var activities = new ActivityCollector();
        var handler = new InstrumentedQueryHandler<GetBoardQuery, string>(
            new SampleQueryHandler(), new CoreMetrics(_meterFactory), NullLogger.Instance);

        Assert.Equal("board", await handler.Handle(new GetBoardQuery(), CancellationToken.None));

        var span = activities.Single("GetBoard");
        Assert.Equal(CoreTelemetry.QueryKind, span.GetTagItem(CoreTelemetry.UseCaseKindAttribute));
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
    }

    [Fact]
    public void Registration_ResolvesTheDecoratorAroundTheConcreteHandler()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddCommandHandler<PlaceShipCommand, int, SampleCommandHandler>()
            .AddCommandHandler<FireCommand, SampleVoidCommandHandler>()
            .AddQueryHandler<GetBoardQuery, string, SampleQueryHandler>()
            .BuildServiceProvider();
        using var scope = provider.CreateScope();

        var command = scope.ServiceProvider.GetRequiredService<ICommandHandler<PlaceShipCommand, int>>();
        var voidCommand = scope.ServiceProvider.GetRequiredService<ICommandHandler<FireCommand>>();
        var query = scope.ServiceProvider.GetRequiredService<IQueryHandler<GetBoardQuery, string>>();

        Assert.IsType<SampleCommandHandler>(Assert.IsType<InstrumentedCommandHandler<PlaceShipCommand, int>>(command).Inner);
        Assert.IsType<SampleVoidCommandHandler>(Assert.IsType<InstrumentedCommandHandler<FireCommand>>(voidCommand).Inner);
        Assert.IsType<SampleQueryHandler>(Assert.IsType<InstrumentedQueryHandler<GetBoardQuery, string>>(query).Inner);
    }

    [Theory]
    [InlineData(typeof(PlaceShipCommand), "Command", "PlaceShip")]
    [InlineData(typeof(GetBoardQuery), "Query", "GetBoard")]
    [InlineData(typeof(Command), "Command", "Command")]
    [InlineData(typeof(Salvo), "Command", "Salvo")]
    public void UseCaseName_DropsTheRequestSuffix(Type requestType, string suffix, string expected)
    {
        var useCase = suffix == "Query"
            ? typeof(InstrumentedQueryHandler<,>).MakeGenericType(requestType, typeof(string))
            : typeof(InstrumentedCommandHandler<>).MakeGenericType(requestType);

        Assert.Equal(expected, useCase.GetField("UseCase")!.GetValue(null));
    }

    private ICommandHandler<PlaceShipCommand, int> Decorate(SampleCommandHandler inner, ILogger? logger = null) =>
        new InstrumentedCommandHandler<PlaceShipCommand, int>(inner, new CoreMetrics(_meterFactory), logger ?? NullLogger.Instance);

    private MetricCollector<double> CollectDuration() =>
        new(_meterFactory, CoreTelemetry.Name, CoreMetrics.UseCaseDurationName);

    public sealed record PlaceShipCommand(int Length);

    public sealed record FireCommand;

    public sealed record GetBoardQuery;

    public sealed record Command;

    public sealed record Salvo;

    public sealed class SampleCommandHandler(Exception? failure = null) : ICommandHandler<PlaceShipCommand, int>
    {
        public string? CurrentActivityName { get; private set; }

        public async Task<int> Handle(PlaceShipCommand command, CancellationToken cancellationToken)
        {
            await Task.Yield();
            CurrentActivityName = Activity.Current?.DisplayName;
            return failure is null ? command.Length * 2 : throw failure;
        }
    }

    public sealed class SampleVoidCommandHandler : ICommandHandler<FireCommand>
    {
        public bool Handled { get; private set; }

        public Task Handle(FireCommand command, CancellationToken cancellationToken)
        {
            Handled = true;
            return Task.CompletedTask;
        }
    }

    public sealed class SampleQueryHandler : IQueryHandler<GetBoardQuery, string>
    {
        public Task<string> Handle(GetBoardQuery query, CancellationToken cancellationToken) => Task.FromResult("board");
    }
}
