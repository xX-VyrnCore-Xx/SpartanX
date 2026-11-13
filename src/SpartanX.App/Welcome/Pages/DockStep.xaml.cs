using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SpartanX.App.Helpers;
using SpartanX.Core.Settings;
using SpartanX.Core.Welcome;

namespace SpartanX.App.Welcome.Pages;

public sealed partial class DockStep : UserControl
{
    private static readonly string[] PreviewColors = ["#FF0A84FF", "#FF30D158", "#FFFF9F0A", "#FFBF5AF2", "#FFFF375F", "#FF64D2FF", "#FFB3202A"];

    public DockStep(WelcomeViewModel vm)
    {
        Dock = vm.Settings.Dock;
        General = vm.Settings.General;
        InitializeComponent();
        EnumBinder.Bind(PositionBox, () => Dock.Position, v => Dock.Position = v);

        foreach (var hex in PreviewColors)
        {
            PreviewIcons.Children.Add(new Border { Background = new SolidColorBrush(Services.ThemeService.ParseHex(hex)) });
        }

        Dock.PropertyChanged += OnDockChanged;
        Unloaded += (_, _) => Dock.PropertyChanged -= OnDockChanged;
        UpdatePreview();
    }

    public DockSettings Dock { get; }

    public GeneralSettings General { get; }

    private void OnDockChanged(object? sender, PropertyChangedEventArgs e) => UpdatePreview();

    /// <summary>A scaled-down Dock that mirrors size and position as the user drags.</summary>
    private void UpdatePreview()
    {
        var size = Dock.IconSize * 0.5;
        foreach (var child in PreviewIcons.Children.OfType<Border>())
        {
            child.Width = size;
            child.Height = size;
            child.CornerRadius = new CornerRadius(size * 0.225);
        }

        var vertical = Dock.Position != DockPosition.Bottom;
        PreviewIcons.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        PreviewBar.HorizontalAlignment = Dock.Position switch
        {
            DockPosition.Left => HorizontalAlignment.Left,
            DockPosition.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center,
        };
        PreviewBar.VerticalAlignment = vertical ? VerticalAlignment.Center : VerticalAlignment.Bottom;
        PreviewBar.Opacity = Dock.AutoHide ? 0.45 : 1;
    }
}
