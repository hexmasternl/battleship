using System.Collections.Concurrent;
using System.Diagnostics;

namespace HexMaster.Battleship.Core.Tests.Observability;

// Collects the HexMaster.Battleship.* activities stopped during one test. It starts a root
// activity and keeps only activities of that trace, so tests running in parallel never see each
// other's spans. Create it inside the test method, so the root is Activity.Current for the code
// under test.
public sealed class ActivityCollector : IDisposable
{
    private static readonly ActivitySource TestSource = new("HexMaster.Battleship.Tests");

    private readonly ActivityListener _listener;
    private readonly ConcurrentQueue<Activity> _stopped = new();
    private readonly Activity _root;

    public ActivityCollector()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("HexMaster.Battleship.", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = Collect
        };
        ActivitySource.AddActivityListener(_listener);

        _root = TestSource.StartActivity("test")
            ?? throw new InvalidOperationException("The test activity was not sampled.");
    }

    public IReadOnlyList<Activity> Activities => [.. _stopped];

    public Activity Single(string operationName) => Assert.Single(Activities, a => a.OperationName == operationName);

    public void Dispose()
    {
        _root.Stop();
        _listener.Dispose();
    }

    private void Collect(Activity activity)
    {
        if (_root is not null && activity != _root && activity.TraceId == _root.TraceId)
        {
            _stopped.Enqueue(activity);
        }
    }
}
