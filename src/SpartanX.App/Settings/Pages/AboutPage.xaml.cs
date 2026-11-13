using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SpartanX.App.Services;
using SpartanX.Core;
using SpartanX.Core.Localization;

namespace SpartanX.App.Settings.Pages;

public sealed partial class AboutPage : UserControl
{
    public AboutPage(IServiceProvider services)
    {
        InitializeComponent();
        VersionText.Text = Localizer.Format("Settings.About.Version", AppInfo.Version);
    }

    private void Repo_Click(object sender, RoutedEventArgs e) => ShellActions.Open(AppInfo.RepositoryUrl);

    private void Issue_Click(object sender, RoutedEventArgs e) => ShellActions.Open(AppInfo.RepositoryUrl + "/issues/new/choose");

    private void License_Click(object sender, RoutedEventArgs e) => ShellActions.Open(AppInfo.RepositoryUrl + "/blob/main/LICENSE");
}
