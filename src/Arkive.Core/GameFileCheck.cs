namespace Arkive.Core;

/// <summary>
/// How a player gets the game's own files back, for messages that ask them to. The Western client is on Steam; the
/// Korean one is installed and checked by its own launcher, so both are named.
/// </summary>
public static class GameFileCheck
{
    /// <summary>"Verify the game files (…)" as the start of a sentence.</summary>
    public const string Verify =
        "Verify the game files (Steam: Verify integrity of game files; the Korean client: its launcher's file check)";

    /// <summary>The same in the middle of a sentence.</summary>
    public const string VerifyLower =
        "verify the game files (Steam: Verify integrity of game files; the Korean client: its launcher's file check)";
}
