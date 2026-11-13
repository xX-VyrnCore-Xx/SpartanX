using SpartanX.App.Interop;
using SpartanX.Core.Lifecycle;
using SpartanX.Core.Localization;
using static SpartanX.App.Interop.NativeMethods;

namespace SpartanX.App.Services;

/// <summary>
/// Headless <c>--quit</c> / <c>--uninstall</c> mode: closes every SpartanX process, gives the
/// taskbar back and (for uninstall) removes launch-at-login and optionally the settings.
/// Runs before any window is created.
/// </summary>
internal static class MaintenanceRunner
{
    public static int Run(CommandLineOptions options)
    {
        Localizer.SetLanguage("");
        var errors = new List<string>();

        InstanceControl.ShutdownOthers(TimeSpan.FromSeconds(5));

        Try(errors, () => new TaskbarController(App.DataDirectory).Restore());

        if (options.Command == LaunchCommand.Uninstall)
        {
            Try(errors, () => new StartupService().SetEnabled(false));
            if (options.Purge)
            {
                Try(errors, DeleteDataDirectory);
            }
        }

        if (!options.Silent)
        {
            var message = errors.Count == 0
                ? Localizer.Format(options.Command == LaunchCommand.Uninstall ? "Uninstall.Done.Message" : "Quit.Done.Message", AppContext.BaseDirectory)
                : Localizer.Get("Uninstall.Failed.Message") + "\n\n" + string.Join("\n", errors);
            MessageBox(0, message, "SpartanX", errors.Count == 0 ? MB_ICONINFORMATION : MB_ICONWARNING);
        }

        return errors.Count == 0 ? 0 : 1;
    }

    private static void DeleteDataDirectory()
    {
        if (!Directory.Exists(App.DataDirectory))
        {
            return;
        }

        // Files may still be releasing for a moment after the old process died.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                Directory.Delete(App.DataDirectory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                Thread.Sleep(300);
            }
        }
    }

    private static void Try(List<string> errors, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            errors.Add(e.Message);
        }
    }
}
