namespace HexMaster.Battleship.Players.Abstractions.DataTransferObjects;

// The display name limits shared by the request validation and the User domain model.
public static class DisplayNameRules
{
    public const int MinLength = 1;
    public const int MaxLength = 40;

    // At least one character that is not white space.
    public const string NotBlankPattern = @"^[\s\S]*\S[\s\S]*$";
}
