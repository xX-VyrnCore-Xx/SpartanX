using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SpartanX.Core.Abstractions;
using SpartanX.Core.Localization;
using SpartanX.Core.Settings;
using SpartanX.Core.Welcome;

namespace SpartanX.App.Welcome.Pages;

public sealed partial class PrivacyStep : UserControl
{
    public PrivacyStep(WelcomeViewModel vm)
    {
        General = vm.Settings.General;
        InitializeComponent();
        NotificationStatus.Text = Localizer.Get("Welcome.Privacy.NotificationsHint");
    }

    public GeneralSettings General { get; }

    private async void Grant_Click(object sender, RoutedEventArgs e)
    {
        var service = App.Current.Services.GetRequiredService<INotificationService>();
        var ok = await service.RequestAccessAsync();
        NotificationStatus.Text = ok
            ? Localizer.Get("Welcome.Privacy.Granted")
            : Localizer.Get(service.UnavailableReasonKey ?? "Notifications.Unavailable.Denied");
        GrantButton.IsEnabled = !ok;
    }
}
