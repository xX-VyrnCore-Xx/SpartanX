using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SpartanX.App.Helpers;
using SpartanX.Core.Abstractions;
using SpartanX.Core.Localization;
using SpartanX.Core.Settings;

namespace SpartanX.App.Settings.Pages;

public sealed partial class IslandPage : UserControl
{
    public IslandPage(IServiceProvider services)
    {
        I = services.GetRequiredService<ISettingsService>().Current.Island;
        InitializeComponent();
        EnumBinder.Bind(InteractionBox, () => I.Interaction, v => I.Interaction = v);

        var notifications = services.GetRequiredService<INotificationService>();
        if (!notifications.IsAvailable && notifications.UnavailableReasonKey is { } reason)
        {
            NotificationsCard.Description = Localizer.Get(reason);
        }
    }

    public IslandSettings I { get; }
}
