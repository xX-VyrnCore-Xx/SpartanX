using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SpartanX.App.Helpers;
using SpartanX.Core.Settings;

namespace SpartanX.App.Settings.Pages;

public sealed partial class MenuBarPage : UserControl
{
    public MenuBarPage(IServiceProvider services)
    {
        M = services.GetRequiredService<ISettingsService>().Current.MenuBar;
        InitializeComponent();
        EnumBinder.Bind(ClockBox, () => M.ClockFormat, v => M.ClockFormat = v);
    }

    public MenuBarSettings M { get; }
}
