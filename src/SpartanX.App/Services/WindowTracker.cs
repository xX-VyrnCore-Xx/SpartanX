using System.Diagnostics;
using System.Text;
using Microsoft.UI.Dispatching;
using SpartanX.Core.Abstractions;
using static SpartanX.App.Interop.NativeMethods;

namespace SpartanX.App.Services;

/// <summary>
/// Tracks the windows that would appear in the Windows taskbar, using WinEvent hooks
/// (debounced) plus a slow safety poll.
/// </summary>
internal sealed class WindowTracker : IWindowTracker, IDisposable
{
    private readonly DispatcherQueue _queue;
    private readonly DispatcherQueueTimer _debounce;
    private readonly DispatcherQueueTimer _poll;
    private readonly List<nint> _hooks = [];
    private readonly Dictionary<int, string?> _exeCache = [];
    private readonly int _ownPid = Environment.ProcessId;
    // Must stay referenced for as long as the hooks exist.
    private readonly WinEventDelegate _hookCallback;

    public WindowTracker(DispatcherQueue queue)
    {
        _queue = queue;
        _hookCallback = OnWinEvent;
        _debounce = queue.CreateTimer();
        _debounce.Interval = TimeSpan.FromMilliseconds(120);
        _debounce.IsRepeating = false;
        _debounce.Tick += (_, _) => Refresh();
        _poll = queue.CreateTimer();
        _poll.Interval = TimeSpan.FromSeconds(3);
        _poll.Tick += (_, _) => Refresh();
    }

    public IReadOnlyList<TrackedWindow> Windows { get; private set; } = [];

    public nint ForegroundWindow { get; private set; }

    public event EventHandler? Changed;

    public void Start()
    {
        Hook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND);
        Hook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND);
        Hook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_HIDE);
        Hook(EVENT_OBJECT_NAMECHANGE, EVENT_OBJECT_NAMECHANGE);
        Hook(EVENT_OBJECT_CLOAKED, EVENT_OBJECT_UNCLOAKED);
        _poll.Start();
        Refresh();
    }

    private void Hook(uint min, uint max)
    {
        var hook = SetWinEventHook(min, max, 0, _hookCallback, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        if (hook != 0)
        {
            _hooks.Add(hook);
        }
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        // Only whole-window events matter (OBJID_WINDOW = 0, CHILDID_SELF = 0).
        if (idObject != 0 || idChild != 0)
        {
            return;
        }

        // Out-of-context hooks are delivered on our UI thread's message loop.
        _debounce.Stop();
        _debounce.Start();
    }

    private void Refresh()
    {
        var list = new List<TrackedWindow>();
        EnumWindows((hwnd, _) =>
        {
            if (IsTaskbarWindow(hwnd, out var pid))
            {
                var title = GetTitle(hwnd);
                var exe = ResolveExe(hwnd, pid);
                list.Add(new TrackedWindow(hwnd, title, exe, pid, IsIconic(hwnd)));
            }

            return true;
        }, 0);

        var foreground = GetForegroundWindow();
        if (foreground == ForegroundWindow && list.SequenceEqual(Windows))
        {
            return;
        }

        Windows = list;
        ForegroundWindow = foreground;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool IsTaskbarWindow(nint hwnd, out int pid)
    {
        pid = 0;
        if (!IsWindowVisible(hwnd) || GetWindowTextLength(hwnd) == 0)
        {
            return false;
        }

        var ex = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        var owner = GetWindow(hwnd, GW_OWNER);
        var isAppWindow = (ex & WS_EX_APPWINDOW) != 0;
        if (!isAppWindow && ((ex & WS_EX_TOOLWINDOW) != 0 || owner != 0))
        {
            return false;
        }

        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return false;
        }

        GetWindowThreadProcessId(hwnd, out pid);
        return pid != _ownPid;
    }

    private static string GetTitle(nint hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        var sb = new StringBuilder(length + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private string? ResolveExe(nint hwnd, int pid)
    {
        var exe = GetExe(pid);
        if (exe is not null && exe.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            // UWP / packaged apps: the real process owns a child CoreWindow.
            string? inner = null;
            EnumChildWindows(hwnd, (child, _) =>
            {
                GetWindowThreadProcessId(child, out var childPid);
                if (childPid != pid)
                {
                    inner = GetExe(childPid);
                    return false;
                }

                return true;
            }, 0);
            exe = inner ?? exe;
        }

        return exe;
    }

    private string? GetExe(int pid)
    {
        if (_exeCache.TryGetValue(pid, out var cached))
        {
            return cached;
        }

        string? path = null;
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle != 0)
        {
            try
            {
                var size = 1024;
                var sb = new StringBuilder(size);
                if (QueryFullProcessImageName(handle, 0, sb, ref size))
                {
                    path = sb.ToString();
                }
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        if (_exeCache.Count > 512)
        {
            _exeCache.Clear(); // PIDs get recycled; keep the cache small and fresh.
        }

        _exeCache[pid] = path;
        return path;
    }

    public void Activate(nint handle)
    {
        if (IsIconic(handle))
        {
            ShowWindow(handle, SW_RESTORE);
        }

        // SwitchToThisWindow bypasses the foreground lock that SetForegroundWindow hits
        // when called from a non-foreground process such as the Dock.
        SwitchToThisWindow(handle, true);
        SetForegroundWindow(handle);
        _debounce.Start();
    }

    public void Minimize(nint handle)
    {
        ShowWindow(handle, SW_MINIMIZE);
        _debounce.Start();
    }

    public void Close(nint handle) => PostMessage(handle, WM_CLOSE, 0, 0);

    public void Dispose()
    {
        foreach (var hook in _hooks)
        {
            UnhookWinEvent(hook);
        }

        _hooks.Clear();
        _poll.Stop();
    }
}
