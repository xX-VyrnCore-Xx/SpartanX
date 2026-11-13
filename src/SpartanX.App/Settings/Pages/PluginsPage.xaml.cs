using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SpartanX.Core.Settings;

namespace SpartanX.App.Settings.Pages;

/// <summary>Built-in Island modules today; the third-party plugin SDK is on the roadmap.</summary>
public sealed partial class PluginsPage : UserControl
{
    public PluginsPage(IServiceProvider services)
    {
        I = services.GetRequiredService<ISettingsService>().Current.Island;
        InitializeComponent();
    }

    public IslandSettings I { get; }
}
