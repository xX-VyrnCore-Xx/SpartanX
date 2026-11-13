using System.Diagnostics;
using static SpartanX.App.Interop.NativeMethods;

namespace SpartanX.App.Services;

/// <summary>
/// Cross-process control of SpartanX: single-instance names, asking the running instance to
/// quit, and force-terminating anything left so the install folder can be deleted.
/// </summary>
internal static class InstanceControl
{
    public const string MutexName = @"Local\SpartanX.SingleInstance";
    public const string ActivateEventName = @"Local\SpartanX.Activate";
    public const string QuitEventName = @"Local\SpartanX.Quit";

    /// <summary>
    /// Asks every other SpartanX process to quit cleanly, waits up to <paramref name="grace"/>,
    /// then kills whatever is still running. Returns how many processes had to be killed.
    /// </summary>
    public static int ShutdownOthers(TimeSpan grace)
    {
        SignalQuit();

        var deadline = DateTime.UtcNow + grace;
        while (DateTime.UtcNow < deadline && OtherInstances().Any())
        {
            Thread.Sleep(100);
        }

        var killed = 0;
        foreach (var process in OtherInstances())
        {
            using (process)
            {
                try
                {
                    // A killed process can't unregister its AppBars (menu bar / Dock);
                    // do it for it so the screen work area is given back.
                    ReleaseAppBars(process.Id);
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                    killed++;
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Already exited, or not ours to kill (another user's session).
                }
            }
        }

        return killed;
    }

    /// <summary>Signals the running instance (if any) to quit. Returns false if none is listening.</summary>
    public static bool SignalQuit()
    {
        try
        {
            using var quit = EventWaitHandle.OpenExisting(QuitEventName);
            quit.Set();
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    /// <summary>All SpartanX processes except this one (matched by executable name).</summary>
    public static IEnumerable<Process> OtherInstances()
    {
        var name = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "SpartanX";
        var self = Environment.ProcessId;
        return Process.GetProcessesByName(name).Where(p =>
        {
            if (p.Id != self)
            {
                return true;
            }

            p.Dispose();
            return false;
        });
    }

    private static void ReleaseAppBars(int processId)
    {
        var windows = new List<nint>();
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == processId)
            {
                windows.Add(hwnd);
            }

            return true;
        }, 0);

        foreach (var hwnd in windows)
        {
            var data = new APPBARDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>(),
                hWnd = hwnd,
            };
            SHAppBarMessage(ABM_REMOVE, ref data); // No-op for windows that aren't AppBars.
        }
    }
}
