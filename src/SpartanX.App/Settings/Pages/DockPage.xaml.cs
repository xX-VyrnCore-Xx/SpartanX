using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SpartanX.App.Helpers;
using SpartanX.Core.Abstractions;
using SpartanX.Core.Settings;
using Windows.Storage.Pickers;

namespace SpartanX.App.Settings.Pages;

public sealed partial class DockPage : UserControl
{
    private readonly IAppLauncher _launcher;

    public DockPage(IServiceProvider services)
    {
        D = services.GetRequiredService<ISettingsService>().Current.Dock;
        _launcher = services.GetRequiredService<IAppLauncher>();
        InitializeComponent();
        EnumBinder.Bind(PositionBox, () => D.Position, v => D.Position = v);
    }

    public DockSettings D { get; }

    private async void AddApp_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Desktop };
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".lnk");
        InitializePicker(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is not null && !D.PinnedApps.Any(p => string.Equals(p.Path, file.Path, StringComparison.OrdinalIgnoreCase)))
        {
            D.PinnedApps.Add(new PinnedApp(_launcher.GetDisplayName(file.Path), file.Path));
        }
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add("*");
        InitializePicker(picker);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null && !D.Stacks.Contains(folder.Path, StringComparer.OrdinalIgnoreCase))
        {
            D.Stacks.Add(folder.Path);
        }
    }

    private void RemovePin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PinnedApp app })
        {
            D.PinnedApps.Remove(app);
        }
    }

    private void RemoveStack_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path })
        {
            D.Stacks.Remove(path);
        }
    }

    /// <summary>Unpackaged apps must parent pickers to a window handle.</summary>
    private void InitializePicker(object picker)
    {
        var window = App.Current.SettingsWindowHandle;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, window);
    }
}
