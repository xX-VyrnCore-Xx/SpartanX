using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SpartanX.App.Helpers;
using SpartanX.App.Services;
using SpartanX.Core.Settings;

namespace SpartanX.App.Settings.Pages;

public sealed partial class AppearancePage : UserControl
{
    private readonly AppearanceSettings _s;

    public AppearancePage(IServiceProvider services)
    {
        _s = services.GetRequiredService<ISettingsService>().Current.Appearance;
        InitializeComponent();
        EnumBinder.Bind(ThemeBox, () => _s.Theme, v => _s.Theme = v);
        EnumBinder.Bind(MaterialBox, () => _s.Material, v => _s.Material = v);
        Welcome.Pages.AppearanceStep.BuildSwatches(Swatches, _s);
        HexBox.Text = _s.CustomAccent;
        CustomPreview.Background = new SolidColorBrush(ThemeService.ParseHex(_s.CustomAccent));
    }

    private void Hex_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = HexBox.Text.Trim();
        if (System.Text.RegularExpressions.Regex.IsMatch(text, "^#?[0-9a-fA-F]{6}$"))
        {
            var hex = text.StartsWith('#') ? text : "#" + text;
            CustomPreview.Background = new SolidColorBrush(ThemeService.ParseHex(hex));
            if (!string.Equals(hex, _s.CustomAccent, StringComparison.OrdinalIgnoreCase))
            {
                _s.CustomAccent = hex.ToUpperInvariant();
                _s.AccentMode = AccentMode.Custom;
            }
        }
    }
}
