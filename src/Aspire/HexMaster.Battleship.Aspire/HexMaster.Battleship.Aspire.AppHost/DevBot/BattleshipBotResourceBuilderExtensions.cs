using CommunityToolkit.Aspire.Hosting.Dapr;

namespace HexMaster.Battleship.Aspire.AppHost.DevBot;

/// <summary>Adds and configures the dev bot opponent.</summary>
public static class BattleshipBotResourceBuilderExtensions
{
    /// <summary>The intelligence presets the worker knows, in the order the dashboard offers them.</summary>
    public static IReadOnlyList<string> Presets { get; } = ["Random", "HuntAndTarget", "HuntWithParity", "ProbabilityDensity"];

    /// <summary>
    /// Adds the bot: the DevBot worker with a bot icon, a health check, its own Dapr sidecar on the
    /// shared pub/sub component, and the gateway as the only backend it calls. It is left out of the
    /// manifest; call it in run mode only.
    /// </summary>
    public static IResourceBuilder<BattleshipBotResource> AddBattleshipBot(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<IResourceWithServiceDiscovery> gateway,
        IResourceBuilder<IDaprComponentResource> pubsub,
        string? daprConfig = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(pubsub);

        // WithProjectDefaults is the 13.6 way to run a custom ProjectResource like AddProject does;
        // it is still marked for evaluation (see design D2).
#pragma warning disable ASPIREPROJECTS001
        var bot = builder.AddResource(new BattleshipBotResource(name))
            .WithAnnotation<IProjectMetadata>(new Projects.HexMaster_Battleship_DevBot())
            .WithProjectDefaults(new ProjectResourceOptions());
#pragma warning restore ASPIREPROJECTS001

        return bot
            .WithIconName("Bot")
            .ExcludeFromManifest()
            .WithHttpHealthCheck("/health")
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions { AppId = BattleshipBotResource.DaprAppId, Config = daprConfig })
                .WithReference(pubsub))
            .WithReference(gateway)
            .WaitFor(gateway)
            .WithBotCommands()
            .WithBotStateProperties();
    }

    /// <summary>The preset the bot starts with. Without this call it starts with HuntAndTarget.</summary>
    public static IResourceBuilder<BattleshipBotResource> WithIntelligence(
        this IResourceBuilder<BattleshipBotResource> builder, string intelligence)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var preset = Presets.FirstOrDefault(p => string.Equals(p, intelligence, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentOutOfRangeException(nameof(intelligence), intelligence,
                $"Unknown intelligence preset; use one of {string.Join(", ", Presets)}.");

        return builder.WithEnvironment(BattleshipBotResource.IntelligenceVariable, preset);
    }

    /// <summary>How long the bot leaves a new game to a human before joining it. Default 10 seconds.</summary>
    public static IResourceBuilder<BattleshipBotResource> WithJoinGracePeriod(
        this IResourceBuilder<BattleshipBotResource> builder, TimeSpan gracePeriod)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentOutOfRangeException.ThrowIfLessThan(gracePeriod, TimeSpan.Zero);

        return builder.WithEnvironment(BattleshipBotResource.JoinGracePeriodVariable, gracePeriod.ToString("c"));
    }

    /// <summary>How long the bot waits before each shot, so its moves can be followed. Default 1 second.</summary>
    public static IResourceBuilder<BattleshipBotResource> WithShotDelay(
        this IResourceBuilder<BattleshipBotResource> builder, TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);

        return builder.WithEnvironment(BattleshipBotResource.ShotDelayVariable, delay.ToString("c"));
    }

    internal static IResourceBuilder<BattleshipBotResource> WithBotCommands(this IResourceBuilder<BattleshipBotResource> builder) =>
        builder.WithBotCommandSet(BotAdminClient.For);

    // Filled in by the state task.
    internal static IResourceBuilder<BattleshipBotResource> WithBotStateProperties(this IResourceBuilder<BattleshipBotResource> builder) => builder;
}
