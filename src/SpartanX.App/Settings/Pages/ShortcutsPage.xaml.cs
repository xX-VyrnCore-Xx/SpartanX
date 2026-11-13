using CommunityToolkit.WinUI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SpartanX.App.Services;
using SpartanX.Core.Input;
using SpartanX.Core.Localization;
using SpartanX.Core.Settings;
using Windows.System;
using Windows.UI.Core;

namespace SpartanX.App.Settings.Pages;

/// <summary>Click a shortcut, press the new combination, done (Esc cancels, Backspace clears).</summary>
public sealed partial class ShortcutsPage : UserControl
{
    private readonly ShortcutSettings _s;
    private readonly HotkeyService _hotkeys;
    private Button? _recording;

    public ShortcutsPage(IServiceProvider services)
    {
        _s = services.GetRequiredService<ISettingsService>().Current.Shortcuts;
        _hotkeys = services.GetRequiredService<HotkeyService>();
        InitializeComponent();

        AddRow(HotkeyAction.ToggleDock, "\uE7C4", () => _s.ToggleDock, v => _s.ToggleDock = v);
        AddRow(HotkeyAction.OpenIsland, "\uE91C", () => _s.OpenIsland, v => _s.OpenIsland = v);
        AddRow(HotkeyAction.OpenSettings, "\uE713", () => _s.OpenSettings, v => _s.OpenSettings = v);
        AddRow(HotkeyAction.PlayPause, "\uE768", () => _s.PlayPause, v => _s.PlayPause = v);
        AddRow(HotkeyAction.StartTimer, "\uE916", () => _s.StartTimer, v => _s.StartTimer = v);
        UpdateConflicts();
    }

    private void AddRow(HotkeyAction action, string glyph, Func<string> get, Action<string> set)
    {
        var button = new Button { MinWidth = 160, Content = Display(get()) };
        var card = new SettingsCard
        {
            Header = Localizer.Get($"Settings.Shortcuts.{action}"),
            HeaderIcon = new FontIcon { Glyph = glyph },
            Content = button,
        };

        button.Click += (_, _) =>
        {
            _recording = button;
            button.Content = Localizer.Get("Settings.Shortcuts.Press");
            button.Focus(FocusState.Programmatic);
        };
        button.LostFocus += (_, _) =>
        {
            if (_recording == button)
            {
                _recording = null;
                button.Content = Display(get());
            }
        };
        button.PreviewKeyDown += (_, e) =>
        {
            if (_recording != button)
            {
                return;
            }

            e.Handled = true;
            if (e.Key == VirtualKey.Escape)
            {
                _recording = null;
                button.Content = Display(get());
                return;
            }

            if (e.Key == VirtualKey.Back)
            {
                set("");
                _recording = null;
                button.Content = Display("");
                UpdateConflicts();
                return;
            }

            if (IsModifier(e.Key))
            {
                return; // Wait for the real key.
            }

            var mods = KeyModifiers.None;
            if (IsDown(VirtualKey.Control)) mods |= KeyModifiers.Ctrl;
            if (IsDown(VirtualKey.Menu)) mods |= KeyModifiers.Alt;
            if (IsDown(VirtualKey.Shift)) mods |= KeyModifiers.Shift;
            if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows)) mods |= KeyModifiers.Win;

            var name = KeyName(e.Key);
            if (mods == KeyModifiers.None || name is null || !KeyGesture.TryParse($"{mods.ToString().Replace(", ", "+")}+{name}", out var gesture))
            {
                button.Content = Localizer.Get("Settings.Shortcuts.NeedModifier");
                return;
            }

            set(gesture!.ToString());
            _recording = null;
            button.Content = Display(gesture.ToString());
            DispatcherQueue.TryEnqueue(UpdateConflicts);
        };

        Rows.Children.Add(card);
    }

    private void UpdateConflicts()
    {
        var conflicts = _hotkeys.Conflicts;
        ConflictBar.IsOpen = conflicts.Count > 0;
        ConflictBar.Message = string.Join(", ", conflicts.Select(c => Localizer.Get($"Settings.Shortcuts.{c}")));
    }

    private static string Display(string gesture) =>
        string.IsNullOrWhiteSpace(gesture) ? Localizer.Get("Settings.Shortcuts.None") : gesture.Replace("+", " + ");

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    private static bool IsModifier(VirtualKey key) => key is VirtualKey.Control or VirtualKey.Menu or VirtualKey.Shift
        or VirtualKey.LeftWindows or VirtualKey.RightWindows or VirtualKey.LeftControl or VirtualKey.RightControl
        or VirtualKey.LeftMenu or VirtualKey.RightMenu or VirtualKey.LeftShift or VirtualKey.RightShift;

    private static string? KeyName(VirtualKey key)
    {
        var k = (int)key;
        if (k is >= 0x41 and <= 0x5A or >= 0x30 and <= 0x39)
        {
            return ((char)k).ToString();
        }

        if (k is >= 0x70 and <= 0x87)
        {
            return $"F{k - 0x6F}";
        }

        return key switch
        {
            VirtualKey.Space => "Space",
            VirtualKey.Enter => "Enter",
            VirtualKey.Tab => "Tab",
            VirtualKey.Left => "Left",
            VirtualKey.Right => "Right",
            VirtualKey.Up => "Up",
            VirtualKey.Down => "Down",
            VirtualKey.Home => "Home",
            VirtualKey.End => "End",
            VirtualKey.PageUp => "PageUp",
            VirtualKey.PageDown => "PageDown",
            VirtualKey.Delete => "Delete",
            VirtualKey.Insert => "Insert",
            (VirtualKey)0xBC => ",",
            (VirtualKey)0xBE => ".",
            (VirtualKey)0xBD => "-",
            (VirtualKey)0xBB => "=",
            _ => null,
        };
    }
}
