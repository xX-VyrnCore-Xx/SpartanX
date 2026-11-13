using Microsoft.UI.Xaml;
using SpartanX.App.Interop;
using SpartanX.Core.Input;
using SpartanX.Core.Settings;
using WinUIEx.Messaging;
using static SpartanX.App.Interop.NativeMethods;

namespace SpartanX.App.Services;

public enum HotkeyAction { ToggleDock, OpenIsland, OpenSettings, PlayPause, StartTimer }

/// <summary>System-wide shortcuts through RegisterHotKey, re-registered whenever settings change.</summary>
internal sealed class HotkeyService : IDisposable
{
    private readonly ISettingsService _settings;
    private readonly Dictionary<int, HotkeyAction> _registered = [];
    private WindowMessageMonitor? _monitor;
    private nint _hwnd;

    public HotkeyService(ISettingsService settings)
    {
        _settings = settings;
        _settings.Changed += (_, e) =>
        {
            if (e.Section is ShortcutSettings && _hwnd != 0)
            {
                RegisterAll();
            }
        };
    }

    public event EventHandler<HotkeyAction>? Triggered;

    /// <summary>Names of shortcuts Windows refused (already taken by another app).</summary>
    public IReadOnlyList<HotkeyAction> Conflicts { get; private set; } = [];

    /// <summary>Hotkey messages are posted to a window; we borrow the menu bar's.</summary>
    public void Attach(Window window)
    {
        _hwnd = window.GetHwnd();
        _monitor = new WindowMessageMonitor(window);
        _monitor.WindowMessageReceived += (_, e) =>
        {
            if (e.Message.MessageId == WM_HOTKEY && _registered.TryGetValue((int)e.Message.WParam, out var action))
            {
                Triggered?.Invoke(this, action);
            }
        };
        RegisterAll();
    }

    private void RegisterAll()
    {
        UnregisterAll();
        var s = _settings.Current.Shortcuts;
        var conflicts = new List<HotkeyAction>();
        var map = new (HotkeyAction Action, string Text)[]
        {
            (HotkeyAction.ToggleDock, s.ToggleDock),
            (HotkeyAction.OpenIsland, s.OpenIsland),
            (HotkeyAction.OpenSettings, s.OpenSettings),
            (HotkeyAction.PlayPause, s.PlayPause),
            (HotkeyAction.StartTimer, s.StartTimer),
        };

        var id = 0xB000;
        foreach (var (action, text) in map)
        {
            if (!KeyGesture.TryParse(text, out var gesture))
            {
                continue;
            }

            if (RegisterHotKey(_hwnd, id, (uint)gesture!.Modifiers | MOD_NOREPEAT, (uint)gesture.VirtualKey))
            {
                _registered[id] = action;
            }
            else
            {
                conflicts.Add(action);
            }

            id++;
        }

        Conflicts = conflicts;
    }

    private void UnregisterAll()
    {
        foreach (var id in _registered.Keys)
        {
            UnregisterHotKey(_hwnd, id);
        }

        _registered.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        _monitor?.Dispose();
    }
}
