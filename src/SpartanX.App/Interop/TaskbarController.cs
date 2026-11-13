using System.Text.Json;
using static SpartanX.App.Interop.NativeMethods;

namespace SpartanX.App.Interop;

/// <summary>
/// Hides the Windows taskbar while SpartanX's Dock and menu bar replace it, and always puts it
/// back on exit. The original auto-hide state is persisted so even a crash can be undone
/// on the next launch.
/// </summary>
internal sealed class TaskbarController(string stateDirectory)
{
    private readonly string _stateFile = Path.Combine(stateDirectory, "taskbar-state.json");
    private readonly Lock _gate = new();
    private bool _hidden;

    public bool IsHidden => _hidden;

    public void Hide()
    {
        lock (_gate)
        {
            if (!File.Exists(_stateFile))
            {
                // Remember how the user had it, once, before we touch anything.
                File.WriteAllText(_stateFile, JsonSerializer.Serialize(new State(GetAutoHide())));
            }

            // Auto-hide frees the work area the taskbar reserved; hiding the window removes it visually.
            SetAutoHide(true);
            foreach (var hwnd in TaskbarWindows())
            {
                ShowWindow(hwnd, SW_HIDE);
            }

            _hidden = true;
        }
    }

    /// <summary>Explorer re-shows the taskbar on some events (e.g. Win key, restart); call periodically.</summary>
    public void EnforceHidden()
    {
        if (!_hidden)
        {
            return;
        }

        foreach (var hwnd in TaskbarWindows())
        {
            if (IsWindowVisible(hwnd))
            {
                ShowWindow(hwnd, SW_HIDE);
            }
        }
    }

    /// <summary>Restores the taskbar exactly as it was. Safe to call many times.</summary>
    public void Restore()
    {
        lock (_gate)
        {
            if (File.Exists(_stateFile))
            {
                try
                {
                    var state = JsonSerializer.Deserialize<State>(File.ReadAllText(_stateFile));
                    SetAutoHide(state?.AutoHide ?? false);
                }
                catch (JsonException)
                {
                    SetAutoHide(false);
                }

                File.Delete(_stateFile);
            }

            foreach (var hwnd in TaskbarWindows())
            {
                ShowWindow(hwnd, SW_SHOWNA);
            }

            _hidden = false;
        }
    }

    private static IEnumerable<nint> TaskbarWindows()
    {
        var primary = FindWindow("Shell_TrayWnd", null);
        if (primary != 0)
        {
            yield return primary;
        }

        nint secondary = 0;
        while ((secondary = FindWindowEx(0, secondary, "Shell_SecondaryTrayWnd", null)) != 0)
        {
            yield return secondary;
        }
    }

    private static bool GetAutoHide()
    {
        var data = new APPBARDATA { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>() };
        return ((int)SHAppBarMessage(ABM_GETSTATE, ref data) & ABS_AUTOHIDE) != 0;
    }

    private static void SetAutoHide(bool autoHide)
    {
        var data = new APPBARDATA
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>(),
            hWnd = FindWindow("Shell_TrayWnd", null),
            lParam = autoHide ? ABS_AUTOHIDE : ABS_ALWAYSONTOP,
        };
        SHAppBarMessage(ABM_SETSTATE, ref data);
    }

    private sealed record State(bool AutoHide);
}
