using System.Diagnostics;
using static SpartanX.App.Interop.NativeMethods;

namespace SpartanX.App.Services;

/// <summary>System commands offered by the menu bar's SpartanX menu.</summary>
internal static class ShellActions
{
    public static void OpenStartMenu()
    {
        keybd_event(VK_LWIN, 0, 0, 0);
        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
    }

    public static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception)
        {
        }
    }

    public static void OpenWindowsSettings() => Open("ms-settings:");

    public static void OpenTaskManager() => Open("taskmgr.exe");

    public static void Lock() => LockWorkStation();

    public static void Sleep() => SetSuspendState(false, false, false);

    public static void Shutdown(bool restart)
    {
        try
        {
            Process.Start(new ProcessStartInfo("shutdown.exe", restart ? "/r /t 0" : "/s /t 0") { UseShellExecute = false, CreateNoWindow = true })?.Dispose();
        }
        catch (Exception)
        {
        }
    }
}
