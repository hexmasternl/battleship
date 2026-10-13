using System.ComponentModel.DataAnnotations;

namespace HexMaster.Battleship.Players.Abstractions.DataTransferObjects;

// POST /tokens. The display name is optional; without one the service generates a call sign.
public sealed record CreateTokensRequest(
    [property: StringLength(DisplayNameRules.MaxLength, MinimumLength = DisplayNameRules.MinLength)]
    [property: RegularExpression(DisplayNameRules.NotBlankPattern, ErrorMessage = "The display name must not be blank.")]
    string? DisplayName = null);
