namespace HexMaster.Battleship.Aspire.AppHost;

/// <summary>
/// Verifies, before the AppHost starts any resource, that the Dapr CLI is installed and that
/// <c>dapr init</c> has been run. Without this check the sidecar resources would fail and
/// restart endlessly on the dashboard without saying why.
/// </summary>
internal static class DaprPrerequisites
{
    private const string InstallCliUrl = "https://docs.dapr.io/getting-started/install-dapr-cli/";

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> naming the missing step when the Dapr
    /// CLI or the initialised Dapr runtime cannot be found on this machine.
    /// </summary>
    public static void EnsureInstalled()
    {
        if (FindDaprCli() is null)
        {
            throw new InvalidOperationException(
                "Dapr prerequisite missing: the Dapr CLI was not found. " +
                $"Install it ({InstallCliUrl}), run 'dapr init', then start the AppHost again.");
        }

        var runtime = DaprRuntimePath();

        if (!File.Exists(runtime))
        {
            throw new InvalidOperationException(
                $"Dapr prerequisite missing: the Dapr runtime is not initialised ('{runtime}' does not exist). " +
                "Run 'dapr init', then start the AppHost again.");
        }
    }

    // Mirrors the locations the Community Toolkit Dapr integration searches for the CLI.
    private static string? FindDaprCli()
    {
        var fileName = OperatingSystem.IsWindows() ? "dapr.exe" : "dapr";

        IEnumerable<string> wellKnown = OperatingSystem.IsWindows()
            ? [Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") ?? "C:", "dapr", fileName)]
            : [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dapr", fileName),
                Path.Combine("/usr/local/bin", fileName),
                Path.Combine("/usr/bin", fileName)
            ];

        var onPath = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, fileName));

        return wellKnown.Concat(onPath).FirstOrDefault(File.Exists);
    }

    // `dapr init` installs the runtime binary under ~/.dapr/bin.
    private static string DaprRuntimePath()
    {
        var fileName = OperatingSystem.IsWindows() ? "daprd.exe" : "daprd";

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dapr",
            "bin",
            fileName);
    }
}
