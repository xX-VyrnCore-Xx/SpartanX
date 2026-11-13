using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SpartanX.App.Animation;
using SpartanX.App.Interop;
using SpartanX.App.Services;
using SpartanX.App.Settings.Pages;
using SpartanX.Core.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace SpartanX.App.Settings;

/// <summary>Sidebar entry: coloured rounded-square icon + title, like macOS System Settings.</summary>
public sealed class NavEntry(string id, string glyph, string color, string keywords)
{
    public string Id { get; } = id;
    public string Glyph { get; } = glyph;
    public string Title => Localizer.Get($"Settings.{Id}.Title");
    public string Keywords { get; } = keywords;
    public Brush Brush { get; } = new SolidColorBrush(ThemeService.ParseHex(color));
}

public sealed partial class SettingsWindow : Window
{
    private static readonly NavEntry[] Entries =
    [
        new("General", "\uE713", "#8E8E93", "language startup taskbar update reset lingua avvio aggiornamenti"),
        new("Appearance", "\uE790", "#1C1C1E", "theme dark light accent colour color material mica acrylic tema scuro chiaro colore"),
        new("Dock", "\uE7C4", "#5E5CE6", "dock icons magnification hide pinned stacks folders icone ingrandimento cartelle"),
        new("Island", "\uE91C", "#000000", "dynamic island media music notifications timer weather musica notifiche meteo"),
        new("MenuBar", "\uE700", "#0A84FF", "menu bar clock battery orologio batteria"),
        new("Animations", "\uE945", "#FF375F", "animation spring speed motion animazioni velocità movimento"),
        new("Shortcuts", "\uE765", "#30D158", "keyboard hotkeys shortcuts tastiera scorciatoie"),
        new("Plugins", "\uEA86", "#FF9F0A", "plugins modules extensions moduli estensioni"),
        new("About", "\uE946", "#B3202A", "about version license github informazioni versione licenza"),
    ];

    private readonly IServiceProvider _services;

    internal SettingsWindow(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();

        Title = Localizer.Get("Settings.WindowTitle");
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarArea);
        WindowHelper.SetIcon(this);
        services.GetRequiredService<ThemeService>().Register(this);
        WindowHelper.CenterOnPrimary(this, 1040, 720);

        Nav.ItemsSource = Entries;
    }

    public void Navigate(string? id)
    {
        Search.Text = "";
        Nav.ItemsSource = Entries;
        Nav.SelectedItem = Entries.FirstOrDefault(e => e.Id == id) ?? Nav.SelectedItem ?? Entries[0];
    }

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedItem is not NavEntry entry)
        {
            return;
        }

        PageTitle.Text = entry.Title;
        UIElement page = entry.Id switch
        {
            "General" => new GeneralPage(_services),
            "Appearance" => new AppearancePage(_services),
            "Dock" => new DockPage(_services),
            "Island" => new IslandPage(_services),
            "MenuBar" => new MenuBarPage(_services),
            "Animations" => new AnimationsPage(_services),
            "Shortcuts" => new ShortcutsPage(_services),
            "Plugins" => new PluginsPage(_services),
            _ => new AboutPage(_services),
        };

        PageHost.Content = page;
        Scroller.ChangeView(null, 0, null, disableAnimation: true);
        Motion.SlideIn(page, 0, 18, 380);
    }

    private void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        var q = sender.Text.Trim();
        var matches = string.IsNullOrEmpty(q)
            ? Entries
            : Entries.Where(e => e.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase) || e.Keywords.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        var selected = Nav.SelectedItem;
        Nav.ItemsSource = matches;
        if (selected is NavEntry s && matches.Contains(s))
        {
            Nav.SelectedItem = s;
        }
        else if (matches.Length > 0)
        {
            Nav.SelectedItem = matches[0];
        }
    }
}
