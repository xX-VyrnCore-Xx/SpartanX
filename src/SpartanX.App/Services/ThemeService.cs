using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SpartanX.Core.Settings;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace SpartanX.App.Services;

/// <summary>
/// Applies theme, accent colour and backdrop material to every SpartanX window, live.
/// The accent is exposed as the mutable <c>SpartanAccentBrush</c> resource so changing it
/// recolours already-open windows without a restart.
/// </summary>
internal sealed class ThemeService
{
    private readonly ISettingsService _settings;
    private readonly UISettings _uiSettings = new();
    private readonly List<WeakReference<Window>> _windows = [];

    public ThemeService(ISettingsService settings)
    {
        _settings = settings;
        _settings.Changed += (_, e) =>
        {
            if (e.Section is AppearanceSettings or AppSettings)
            {
                ApplyAll();
            }
        };
        // Follows the Windows accent when the user changes it in Windows Settings.
        _uiSettings.ColorValuesChanged += (_, _) =>
        {
            if (_settings.Current.Appearance.AccentMode == AccentMode.Windows)
            {
                App.MainQueue?.TryEnqueue(ApplyAll);
            }
        };
    }

    public Color Accent => _settings.Current.Appearance.AccentMode switch
    {
        AccentMode.Windows => _uiSettings.GetColorValue(UIColorType.Accent),
        AccentMode.Custom => ParseHex(_settings.Current.Appearance.CustomAccent),
        _ => ParseHex(AppearanceSettings.SpartanRed),
    };

    public ElementTheme ElementTheme => _settings.Current.Appearance.Theme switch
    {
        AppTheme.Light => ElementTheme.Light,
        AppTheme.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    /// <summary>Registers a window and applies the current look. <paramref name="useBackdrop"/> is
    /// false for transparent surfaces (Island, Dock) that draw their own background.</summary>
    public void Register(Window window, bool useBackdrop = true)
    {
        _windows.Add(new WeakReference<Window>(window));
        Apply(window, useBackdrop);
        window.Closed += (_, _) => _windows.RemoveAll(w => !w.TryGetTarget(out var t) || t == window);
    }

    public void ApplyAll()
    {
        UpdateAccentResources();
        foreach (var weak in _windows.ToList())
        {
            if (weak.TryGetTarget(out var w))
            {
                Apply(w, w.SystemBackdrop is not WinUIEx.TransparentTintBackdrop);
            }
        }
    }

    public void UpdateAccentResources()
    {
        var accent = Accent;
        var resources = Application.Current.Resources;
        if (resources.TryGetValue("SpartanAccentBrush", out var b) && b is SolidColorBrush brush)
        {
            brush.Color = accent;
        }

        if (resources.TryGetValue("SpartanAccentSoftBrush", out var s) && s is SolidColorBrush soft)
        {
            soft.Color = Color.FromArgb(0x33, accent.R, accent.G, accent.B);
        }

        // System controls (ToggleSwitch, Slider, buttons...) read these keys.
        resources["SystemAccentColor"] = accent;
        resources["SystemAccentColorLight1"] = Lighten(accent, 0.15);
        resources["SystemAccentColorLight2"] = Lighten(accent, 0.30);
        resources["SystemAccentColorLight3"] = Lighten(accent, 0.45);
        resources["SystemAccentColorDark1"] = Lighten(accent, -0.15);
        resources["SystemAccentColorDark2"] = Lighten(accent, -0.30);
        resources["SystemAccentColorDark3"] = Lighten(accent, -0.45);
    }

    private void Apply(Window window, bool useBackdrop)
    {
        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = ElementTheme;
        }

        if (!useBackdrop)
        {
            return;
        }

        var material = _settings.Current.Appearance.Material;
        var current = window.SystemBackdrop switch
        {
            MicaBackdrop { Kind: MicaKind.BaseAlt } => BackdropMaterial.Glass,
            MicaBackdrop => BackdropMaterial.Mica,
            DesktopAcrylicBackdrop => BackdropMaterial.Acrylic,
            _ => BackdropMaterial.Solid,
        };
        if (current == material)
        {
            return; // Re-creating the backdrop on every theme/accent change makes windows flash.
        }

        window.SystemBackdrop = material switch
        {
            BackdropMaterial.Mica => new MicaBackdrop { Kind = MicaKind.Base },
            BackdropMaterial.Glass => new MicaBackdrop { Kind = MicaKind.BaseAlt },
            BackdropMaterial.Acrylic => new DesktopAcrylicBackdrop(),
            _ => null,
        };
    }

    public static Color ParseHex(string? hex)
    {
        hex = (hex ?? "").TrimStart('#');
        if (hex.Length == 6 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            return Color.FromArgb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }

        return Color.FromArgb(0xFF, 0xB3, 0x20, 0x2A);
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static Color Lighten(Color c, double amount)
    {
        static byte Mix(byte v, double a) => (byte)Math.Clamp(a >= 0 ? v + (255 - v) * a : v * (1 + a), 0, 255);
        return Color.FromArgb(c.A, Mix(c.R, amount), Mix(c.G, amount), Mix(c.B, amount));
    }
}
