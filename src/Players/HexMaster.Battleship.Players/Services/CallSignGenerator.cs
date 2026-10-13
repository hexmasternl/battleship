namespace HexMaster.Battleship.Players.Services;

// The display name of a user who did not choose one: a nautical rank and a surname.
public sealed class CallSignGenerator
{
    internal static readonly string[] Ranks =
    [
        "Admiral", "Captain", "Commander", "Commodore", "Ensign", "Lieutenant", "Midshipman", "Skipper",
    ];

    internal static readonly string[] Surnames =
    [
        "Anson", "Bligh", "Blake", "Cook", "Drake", "Farragut", "Hawke", "Hood", "Jellicoe", "Jervis",
        "Nelson", "Perry", "Rodney", "Ruyter", "Tromp", "Hein", "Vernon", "Halsey", "Nimitz", "Beatty",
    ];

    public string Generate() =>
        $"{Ranks[Random.Shared.Next(Ranks.Length)]} {Surnames[Random.Shared.Next(Surnames.Length)]}";
}
