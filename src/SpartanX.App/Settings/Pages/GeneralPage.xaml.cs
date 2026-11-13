using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SpartanX.App.Services;
using SpartanX.Core.Localization;
using SpartanX.Core.Settings;
using SpartanX.Core.Updates;

namespace SpartanX.App.Settings.Pages;

public sealed partial class GeneralPage : UserControl
{
    private readonly ISettingsService _settings;
    private readonly UpdateService _updates;
    private readonly bool _ready;
    private UpdateInfo? _update;

    public GeneralPage(IServiceProvider services)
    {
        _settings = services.GetRequiredService<ISettingsService>();
        _updates = services.GetRequiredService<UpdateService>();
        S = _settings.Current;
        InitializeComponent();

        LanguageBox.Items.Add(new ComboBoxItem { Content = Localizer.Get("Settings.General.LanguageSystem"), Tag = "" });
        foreach (var lang in Localizer.SupportedLanguages)
        {
            LanguageBox.Items.Add(new ComboBoxItem { Content = lang.NativeName, Tag = lang.Tag });
        }

        LanguageBox.SelectedItem = LanguageBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == S.General.Language) ?? LanguageBox.Items[0];
        _ready = true;

        if (_updates.Available is { } available)
        {
            ShowUpdate(available);
        }
    }

    public AppSettings S { get; }

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || LanguageBox.SelectedItem is not ComboBoxItem { Tag: string tag } || tag == S.General.Language)
        {
            return;
        }

        S.General.Language = tag;
        RestartBar.IsOpen = true;
    }

    private void Restart_Click(object sender, RoutedEventArgs e) => App.Current.Restart();

    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        CheckButton.IsEnabled = false;
        UpdateRing.IsActive = true;
        UpdateCard.Description = Localizer.Get("Settings.General.Checking");
        var update = await _updates.CheckAsync();
        UpdateRing.IsActive = false;
        CheckButton.IsEnabled = true;
        if (update is null)
        {
            UpdateCard.Description = Localizer.Get("Settings.General.UpToDate");
            InstallButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            ShowUpdate(update);
        }
    }

    private void ShowUpdate(UpdateInfo update)
    {
        _update = update;
        UpdateCard.Description = Localizer.Format("Settings.General.UpdateAvailable", update.Version);
        InstallButton.Visibility = Visibility.Visible;
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_update is null)
        {
            return;
        }

        InstallButton.IsEnabled = false;
        UpdateRing.IsActive = true;
        var progress = new Progress<double>(p => UpdateCard.Description = Localizer.Format("Settings.General.Downloading", Math.Round(p * 100)));
        try
        {
            await _updates.InstallAsync(_update, progress);
        }
        catch (Exception ex)
        {
            App.Log(ex);
            UpdateCard.Description = Localizer.Get("Settings.General.UpdateFailed");
            InstallButton.IsEnabled = true;
            UpdateRing.IsActive = false;
        }
    }

    private void RunSetup_Click(object sender, RoutedEventArgs e) => App.Current.RerunWelcome();

    private void DataFolder_Click(object sender, RoutedEventArgs e) => ShellActions.Open(App.DataDirectory);

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        ResetFlyout.Hide();
        _settings.ResetToDefaults();
        App.Current.Restart();
    }
}
