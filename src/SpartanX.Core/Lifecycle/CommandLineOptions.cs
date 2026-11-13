namespace SpartanX.Core.Lifecycle;

/// <summary>What SpartanX.exe was asked to do on the command line.</summary>
public enum LaunchCommand
{
    /// <summary>Normal start (or bring the running instance's Settings forward).</summary>
    Run,

    /// <summary><c>--quit</c>: close every running SpartanX process, restore the taskbar, exit.</summary>
    Quit,

    /// <summary><c>--uninstall</c>: like Quit, plus remove launch-at-login. Add <c>--purge</c> to delete settings.</summary>
    Uninstall,
}

/// <param name="WaitForPrevious">
/// Set by <c>--restarted</c> / <c>--updated</c>: the previous instance is still shutting down,
/// so wait for it instead of treating it as "already running".
/// </param>
public sealed record CommandLineOptions(LaunchCommand Command, bool Purge, bool Silent, bool WaitForPrevious = false)
{
    public static CommandLineOptions Parse(IEnumerable<string> args)
    {
        var command = LaunchCommand.Run;
        var purge = false;
        var silent = false;
        var waitForPrevious = false;

        foreach (var raw in args)
        {
            var arg = raw.Trim().TrimStart('-', '/').ToLowerInvariant();
            switch (arg)
            {
                case "quit" or "exit" or "shutdown":
                    // Uninstall wins if both are given.
                    if (command != LaunchCommand.Uninstall)
                    {
                        command = LaunchCommand.Quit;
                    }

                    break;
                case "uninstall":
                    command = LaunchCommand.Uninstall;
                    break;
                case "purge":
                    purge = true;
                    break;
                case "silent" or "quiet" or "q":
                    silent = true;
                    break;
                case "restarted" or "updated":
                    waitForPrevious = true;
                    break;
            }
        }

        // --purge only makes sense while uninstalling.
        return new CommandLineOptions(command, purge && command == LaunchCommand.Uninstall, silent, waitForPrevious);
    }
}
