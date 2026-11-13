using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SpartanX.App.Helpers;
using SpartanX.App.Services;
using SpartanX.Core.Localization;
using SpartanX.Core.Settings;
using SpartanX.Core.Welcome;

namespace SpartanX.App.Welcome.Pages;

public sealed partial class AppearanceStep : UserControl
{
    /// <summary>Accent presets: null hex = follow Windows.</summary>
    internal static readonly (string Key, string? Hex)[] Presets =
    [
        ("Accent.Spartan", AppearanceSettings.SpartanRed),
        ("Accent.Windows", null),
        ("Accent.Blue", "#0A84FF"),
        ("Accent.Purple", "#BF5AF2"),
        ("Accent.Pink", "#FF375F"),
        ("Accent.Orange", "#FF9F0A"),
        ("Accent.Green", "#30D158"),
        ("Accent.Graphite", "#8E8E93"),
    ];

    private readonly AppearanceSettings _settings;

    public AppearanceStep(WelcomeViewModel vm)
    {
        _settings = vm.Settings.Appearance;
        InitializeComponent();
        EnumBinder.Bind(MaterialBox, () => _settings.Material, v => _settings.Material = v);
        BuildSwatches(Swatches, _settings);
        UpdateThemeRings();
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<AppTheme>(tag, out var theme))
        {
            _settings.Theme = theme;
            UpdateThemeRings();
        }
    }

    private void UpdateThemeRings()
    {
        var accent = (Brush)Application.Current.Resources["SpartanAccentBrush"];
        var none = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        LightRing.BorderBrush = _settings.Theme == AppTheme.Light ? accent : none;
        DarkRing.BorderBrush = _settings.Theme == AppTheme.Dark ? accent : none;
        AutoRing.BorderBrush = _settings.Theme == AppTheme.System ? accent : none;
    }

    /// <summary>Shared with Settings → Appearance.</summary>
    internal static void BuildSwatches(Panel host, AppearanceSettings settings)
    {
        void Refresh()
        {
            foreach (var child in host.Children.OfType<Button>())
            {
                var (_, hex) = ((string, string?))child.Tag;
                var selected = hex is null
                    ? settings.AccentMode == AccentMode.Windows
                    : hex == AppearanceSettings.SpartanRed
                        ? settings.AccentMode == AccentMode.Spartan
                        : settings.AccentMode == AccentMode.Custom && string.Equals(settings.CustomAccent, hex, StringComparison.OrdinalIgnoreCase);
                child.BorderThickness = new Thickness(selected ? 3 : 0);
            }
        }

        host.Children.Clear();
        foreach (var preset in Presets)
        {
            var color = preset.Hex is null
                ? new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent)
                : ThemeService.ParseHex(preset.Hex);
            var swatch = new Button
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(0),
                Background = new SolidColorBrush(color),
                BorderBrush = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
                Tag = (preset.Key, preset.Hex),
            };
            ToolTipService.SetToolTip(swatch, Localizer.Get(preset.Key));
            swatch.Click += (_, _) =>
            {
                if (preset.Hex is null)
                {
                    settings.AccentMode = AccentMode.Windows;
                }
                else if (preset.Hex == AppearanceSettings.SpartanRed)
                {
                    settings.AccentMode = AccentMode.Spartan;
                }
                else
                {
                    settings.CustomAccent = preset.Hex;
                    settings.AccentMode = AccentMode.Custom;
                }

                Refresh();
            };
            host.Children.Add(swatch);
        }

        Refresh();
    }
}
