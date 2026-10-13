using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HexMaster.Battleship.Aspire.AppHost.DevBot;

/// <summary>
/// The dashboard and CLI commands of the dev bot. Each is enabled only while the bot is healthy;
/// the auto-join pair also only when it would change the setting.
/// </summary>
internal static class BotCommands
{
    public const string SetIntelligence = "set-intelligence";
    public const string EnableAutoJoin = "enable-auto-join";
    public const string DisableAutoJoin = "disable-auto-join";
    public const string JoinGame = "join-game";

    public const string IntelligenceArgument = "intelligence";
    public const string CodeArgument = "code";

    private const int CodeLength = 8;

    public static IResourceBuilder<BattleshipBotResource> WithBotCommandSet(
        this IResourceBuilder<BattleshipBotResource> builder,
        Func<BattleshipBotResource, BotAdminClient> clientFor)
    {
        var bot = builder.Resource;

        builder.WithCommand(SetIntelligence, "Set intelligence",
            context => clientFor(bot).SetIntelligenceAsync(
                context.Arguments.GetString(IntelligenceArgument) ?? string.Empty, context.CancellationToken),
            new CommandOptions
            {
                Description = "Choose how well the bot plays, from its next shot in every game.",
                IconName = "BrainCircuit",
                Arguments = [IntelligenceInput()],
                UpdateState = WhenHealthy,
            });

        builder.WithCommand(EnableAutoJoin, "Enable auto-join",
            context => clientFor(bot).SetAutoJoinAsync(true, context.CancellationToken),
            new CommandOptions
            {
                Description = "Join new games that nobody else joins within the grace period.",
                IconName = "PersonAdd",
                UpdateState = context => AutoJoinCommandState(context, enables: true),
            });

        builder.WithCommand(DisableAutoJoin, "Disable auto-join",
            context => clientFor(bot).SetAutoJoinAsync(false, context.CancellationToken),
            new CommandOptions
            {
                Description = "Stop joining new games; games in progress continue.",
                IconName = "PersonDelete",
                UpdateState = context => AutoJoinCommandState(context, enables: false),
            });

        builder.WithCommand(JoinGame, "Join game",
            context => clientFor(bot).JoinGameAsync(
                (context.Arguments.GetString(CodeArgument) ?? string.Empty).Trim().ToUpperInvariant(), context.CancellationToken),
            new CommandOptions
            {
                Description = "Send the bot to a game by its code, or resume a game it plays after a restart.",
                IconName = "Play",
                Arguments = [CodeInput()],
                ValidateArguments = ValidateCode,
                UpdateState = WhenHealthy,
            });

        return builder;
    }

    internal static InteractionInput IntelligenceInput() => new()
    {
        Name = IntelligenceArgument,
        Label = "Intelligence",
        InputType = InputType.Choice,
        Required = true,
        Options = [.. BattleshipBotResourceBuilderExtensions.Presets.Select(p => KeyValuePair.Create(p, p))],
    };

    internal static InteractionInput CodeInput() => new()
    {
        Name = CodeArgument,
        Label = "Game code",
        InputType = InputType.Text,
        Required = true,
        MaxLength = CodeLength,
        Placeholder = "K7Q2M9XA",
    };

    internal static Task ValidateCode(InputsDialogValidationContext context)
    {
        var input = context.Inputs[CodeArgument];
        if (CodeError(input.Value) is { } error)
        {
            context.AddValidationError(input, error);
        }

        return Task.CompletedTask;
    }

    internal static string? CodeError(string? value)
    {
        var code = value?.Trim() ?? string.Empty;
        return code.Length == CodeLength && code.All(char.IsAsciiLetterOrDigit)
            ? null
            : $"A game code is {CodeLength} letters and digits, such as K7Q2M9XA.";
    }

    internal static ResourceCommandState WhenHealthy(UpdateCommandStateContext context) =>
        context.ResourceSnapshot.HealthStatus == HealthStatus.Healthy ? ResourceCommandState.Enabled : ResourceCommandState.Disabled;

    // While the setting is unknown (the first state poll has not answered yet) both commands are enabled.
    internal static ResourceCommandState AutoJoinCommandState(UpdateCommandStateContext context, bool enables)
    {
        if (WhenHealthy(context) != ResourceCommandState.Enabled)
        {
            return ResourceCommandState.Disabled;
        }

        var property = context.ResourceSnapshot.Properties
            .FirstOrDefault(p => p.Name == BattleshipBotResource.AutoJoinProperty);

        return property?.Value is bool autoJoin && autoJoin == enables
            ? ResourceCommandState.Disabled
            : ResourceCommandState.Enabled;
    }
}
