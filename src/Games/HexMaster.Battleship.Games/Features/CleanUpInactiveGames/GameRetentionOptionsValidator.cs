using Microsoft.Extensions.Options;

namespace HexMaster.Battleship.Games.Features.CleanUpInactiveGames;

// Runs on start, so the Games API refuses to start with a retention that would remove every game.
public sealed class GameRetentionOptionsValidator : IValidateOptions<GameRetentionOptions>
{
    public ValidateOptionsResult Validate(string? name, GameRetentionOptions options) =>
        options.InactiveDays > 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{GameRetentionOptions.ConfigurationSection}:InactiveDays must be greater than 0.");
}
