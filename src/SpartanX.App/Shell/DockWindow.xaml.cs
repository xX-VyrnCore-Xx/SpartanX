using System.Collections.Specialized;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SpartanX.App.Animation;
using SpartanX.App.Controls;
using SpartanX.App.Interop;
using SpartanX.App.Services;
using SpartanX.Core.Abstractions;
using SpartanX.Core.Animation;
using SpartanX.Core.Dock;
using SpartanX.Core.Localization;
using SpartanX.Core.Settings;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage;
using static SpartanX.App.Interop.NativeMethods;

namespace SpartanX.App.Shell;

/// <summary>
/// The Dock. A screen-wide transparent window whose hit region hugs the bar (and grows while
/// the pointer is on it so magnified icons stay interactive).
/// </summary>
public sealed partial class DockWindow : Window
{
    private const double Padding = 8;
    private const double EdgeGap = 6;
    private const double LabelSpace = 44;
    private const double Spacing = 6;

    private readonly DockViewModel _vm;
    private readonly IAppLauncher _launcher;
    private readonly ShellAppBar _appBar;
    private readonly Dictionary<DockItemViewModel, DockIconView> _views = [];
    private readonly DockItemViewModel _trash;
    private readonly Border _separator = new() { Width = 1, Opacity = 0.35 };
    private readonly Func<double, bool> _magnifyTick;
    private readonly Func<double, bool> _slideTick;
    private readonly Spring _slide = new(0, SpringPresets.Smooth) { RestThreshold = 0.5 };
    private double _scale = 1;
    private RectInt32 _window;
    private bool _hovered;
    private bool _hidden;
    private bool _magnifying;
    private bool _sliding;
    private double? _pointer;
    private DockIconView? _labelFor;
    private CancellationTokenSource? _hideDelay;

    internal DockWindow(DockViewModel viewModel, IAppLauncher launcher, ThemeService theme)
    {
        _vm = viewModel;
        _launcher = launcher;
        InitializeComponent();

        WindowHelper.MakeShellSurface(this, topmost: true, noActivate: true);
        SystemBackdrop = new WinUIEx.TransparentTintBackdrop();
        theme.Register(this, useBackdrop: false);
        _appBar = new ShellAppBar(this.GetHwnd());
        _separator.Background = (Brush)Application.Current.Resources["DockIndicatorBrush"];

        _trash = new DockItemViewModel(DockItemKind.Trash, "::trash", Localizer.Get("Dock.Trash"), "shell:RecycleBinFolder");
        _magnifyTick = MagnifyTick;
        _slideTick = SlideTick;

        _vm.Apps.CollectionChanged += OnItemsChanged;
        _vm.Stacks.CollectionChanged += OnItemsChanged;
        Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DockSettings.Enabled))
            {
                return; // Handled by ShellController.
            }

            Layout();
        };

        _vm.Rebuild();
        SyncChildren();
    }

    private DockSettings Settings => _vm.Settings;

    private bool Vertical => Settings.Position != DockPosition.Bottom;

    private double BaseSize => Settings.IconSize;

    private double MaxScale => Settings.Magnification ? Math.Max(1, Settings.MagnificationScale) : 1;

    /// <summary>Thickness of the visible bar (physical size is this × DPI scale).</summary>
    private double BarThickness => BaseSize + Padding * 2 + DockIconView.IndicatorGap - 4;

    private bool _shownOnce;

    /// <summary>The first show goes through Activate() so WinUI loads the content; later ones never steal focus.</summary>
    private void ShowWithoutFocus()
    {
        if (!_shownOnce)
        {
            _shownOnce = true;
            Activate();
        }

        AppWindow.Show(false);
    }

    public void SetVisible(bool visible)
    {
        if (visible)
        {
            Layout();
            ShowWithoutFocus();
        }
        else
        {
            _appBar.Release();
            AppWindow.Hide();
        }
    }

    public void ToggleAutoHide() => Settings.AutoHide = !Settings.AutoHide;

    public void ReleaseAppBar() => _appBar.Release();

    // ---- Layout -------------------------------------------------------------------

    public void Layout()
    {
        if (!Settings.Enabled)
        {
            return; // Positioning would show the window again.
        }

        _scale = this.GetScale();
        var display = DisplayArea.Primary.OuterBounds;
        var cross = BaseSize * MaxScale + DockIconView.IndicatorGap + Padding + EdgeGap + LabelSpace;
        var crossPx = (int)Math.Ceiling(cross * _scale);
        var monitor = new RECT { Left = display.X, Top = display.Y, Right = display.X + display.Width, Bottom = display.Y + display.Height };

        _window = Settings.Position switch
        {
            DockPosition.Left => new RectInt32(display.X, display.Y, crossPx, display.Height),
            DockPosition.Right => new RectInt32(display.X + display.Width - crossPx, display.Y, crossPx, display.Height),
            _ => new RectInt32(display.X, display.Y + display.Height - crossPx, display.Width, crossPx),
        };

        if (Settings.AutoHide)
        {
            _appBar.Release();
        }
        else
        {
            var edge = Settings.Position switch { DockPosition.Left => ABE_LEFT, DockPosition.Right => ABE_RIGHT, _ => ABE_BOTTOM };
            _appBar.Reserve(edge, monitor, (int)Math.Ceiling((BarThickness + EdgeGap) * _scale));
        }

        WindowHelper.PlaceTopmost(this, _window.X, _window.Y, _window.Width, _window.Height);

        // Orientation-dependent alignment: everything hugs the screen edge.
        Icons.Orientation = Vertical ? Orientation.Vertical : Orientation.Horizontal;
        var hAlign = Settings.Position switch { DockPosition.Left => HorizontalAlignment.Left, DockPosition.Right => HorizontalAlignment.Right, _ => HorizontalAlignment.Center };
        var vAlign = Vertical ? VerticalAlignment.Center : VerticalAlignment.Bottom;
        Icons.HorizontalAlignment = Bar.HorizontalAlignment = hAlign;
        Icons.VerticalAlignment = Bar.VerticalAlignment = vAlign;
        Icons.Margin = Settings.Position switch
        {
            DockPosition.Left => new Thickness(EdgeGap + 2, 0, 0, 0),
            DockPosition.Right => new Thickness(0, 0, EdgeGap + 2, 0),
            _ => new Thickness(0, 0, 0, EdgeGap + 2),
        };
        Bar.Margin = Settings.Position switch
        {
            DockPosition.Left => new Thickness(EdgeGap, 0, 0, 0),
            DockPosition.Right => new Thickness(0, 0, EdgeGap, 0),
            _ => new Thickness(0, 0, 0, EdgeGap),
        };
        Bar.CornerRadius = new CornerRadius(Math.Min(22, BaseSize * 0.35 + 4));

        if (Vertical)
        {
            Bar.Width = BarThickness;
            _separator.Width = BaseSize * 0.7;
            _separator.Height = 1;
            _separator.Margin = new Thickness(0, 2, 0, 2);
        }
        else
        {
            Bar.Height = BarThickness;
            _separator.Width = 1;
            _separator.Height = BaseSize * 0.7;
            _separator.Margin = new Thickness(2, 0, 2, DockIconView.IndicatorGap + BaseSize * 0.15);
        }

        _separator.VerticalAlignment = Vertical ? VerticalAlignment.Center : VerticalAlignment.Bottom;
        _separator.HorizontalAlignment = Settings.Position switch { DockPosition.Left => HorizontalAlignment.Left, DockPosition.Right => HorizontalAlignment.Right, _ => HorizontalAlignment.Center };

        foreach (var view in AllViews())
        {
            view.BaseSize = BaseSize;
            view.CrossSize = BaseSize * MaxScale + DockIconView.IndicatorGap;
            view.SetOrientation(Vertical, Settings.Position == DockPosition.Left);
            view.SetShowIndicator(Settings.ShowRunningIndicators);
            view.SetScaleTarget(1, immediate: true);
        }

        _hidden = Settings.AutoHide && !_hovered;
        _slide.Snap(_hidden ? HiddenOffset() : 0);
        ApplySlide();
        UpdateBarSize();
        UpdateRegion();
    }

    private IEnumerable<DockIconView> AllViews() => Icons.Children.OfType<DockIconView>();

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncChildren();

    private void SyncChildren()
    {
        var desired = new List<UIElement>();
        foreach (var item in _vm.Apps)
        {
            desired.Add(GetView(item));
        }

        desired.Add(_separator);
        foreach (var item in _vm.Stacks)
        {
            desired.Add(GetView(item));
        }

        desired.Add(GetView(_trash));

        foreach (var stale in _views.Where(kv => !desired.Contains(kv.Value)).ToList())
        {
            _views.Remove(stale.Key);
        }

        // Rebuild children in order, animating genuinely new tiles in.
        var existing = Icons.Children.ToHashSet();
        Icons.Children.Clear();
        foreach (var element in desired)
        {
            Icons.Children.Add(element);
            if (!existing.Contains(element) && element is DockIconView view && IsLoadedInVisualTree())
            {
                Motion.FadeScale(view, show: true, fromScale: 0.4);
            }
        }

        UpdateBarSize();
        UpdateRegion();
    }

    private bool IsLoadedInVisualTree() => Root.XamlRoot is not null;

    private DockIconView GetView(DockItemViewModel item)
    {
        if (_views.TryGetValue(item, out var view))
        {
            return view;
        }

        view = new DockIconView(item, BaseSize) { CrossSize = BaseSize * MaxScale + DockIconView.IndicatorGap };
        view.SetOrientation(Vertical, Settings.Position == DockPosition.Left);
        view.SetShowIndicator(Settings.ShowRunningIndicators);
        view.Tapped += Icon_Tapped;
        view.RightTapped += Icon_RightTapped;
        view.PointerEntered += (_, _) => ShowLabel(view);
        view.PointerExited += (_, _) => HideLabel(view);
        Motion.EnableImplicitOffset(view, 220);
        _views[item] = view;
        return view;
    }

    /// <summary>Total length of the icon row at the current (animated) scales.</summary>
    private double RowLength()
    {
        var length = 0.0;
        var count = 0;
        foreach (var child in Icons.Children)
        {
            length += child switch
            {
                DockIconView v => v.Length,
                Border b when b == _separator => (Vertical ? b.Height : b.Width) + 4,
                _ => 0,
            };
            count++;
        }

        return length + Math.Max(0, count - 1) * Spacing;
    }

    private void UpdateBarSize()
    {
        var length = RowLength() + Padding * 2;
        if (Vertical)
        {
            Bar.Height = length;
        }
        else
        {
            Bar.Width = length;
        }
    }

    // ---- Magnification ----------------------------------------------------------

    private void Root_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _hideDelay?.Cancel();
        if (!_hovered)
        {
            _hovered = true;
            UpdateRegion();
            if (_hidden)
            {
                SlideTo(hidden: false);
            }
        }

        Root_PointerMoved(sender, e);
    }

    private void Root_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(Root).Position;
        if (!InActiveZone(p))
        {
            // The window is screen-wide; once the pointer leaves the dock area we must give
            // the space back to the desktop immediately instead of waiting for PointerExited.
            if (_hovered)
            {
                LeaveHover();
            }

            return;
        }

        if (!_hovered)
        {
            _hideDelay?.Cancel();
            _hovered = true;
            UpdateRegion();
        }

        _pointer = _hidden ? null : (Vertical ? p.Y : p.X);
        StartMagnify();
    }

    /// <summary>Bar band plus the space magnified icons and the label can occupy.</summary>
    private bool InActiveZone(Point p)
    {
        var reach = BaseSize * MaxScale + EdgeGap + Padding * 2 + DockIconView.IndicatorGap;
        var barOrigin = Bar.TransformToVisual(Root).TransformPoint(new Point(0, 0));
        if (Vertical)
        {
            var inCross = Settings.Position == DockPosition.Left ? p.X <= reach : p.X >= Root.ActualWidth - reach;
            return inCross && p.Y >= barOrigin.Y - BaseSize && p.Y <= barOrigin.Y + Bar.ActualHeight + BaseSize;
        }

        return p.Y >= Root.ActualHeight - reach && p.X >= barOrigin.X - BaseSize && p.X <= barOrigin.X + Bar.ActualWidth + BaseSize;
    }

    private void Root_PointerExited(object sender, PointerRoutedEventArgs e) => LeaveHover();

    private async void LeaveHover()
    {
        _pointer = null;
        StartMagnify();
        HideLabel(null);

        _hideDelay?.Cancel();
        var cts = _hideDelay = new CancellationTokenSource();
        if (!Settings.AutoHide)
        {
            _hovered = false;
            UpdateRegion();
            return;
        }

        try
        {
            await Task.Delay(450, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        _hovered = false;
        SlideTo(hidden: true);
        UpdateRegion();
    }

    private void StartMagnify()
    {
        var views = AllViews().ToList();
        if (views.Count == 0)
        {
            return;
        }

        // Rest-layout centres along the axis, in Root coordinates, so the effect is stable
        // while the row itself grows.
        var restLength = 0.0;
        var centers = new List<double>();
        var rowChildren = Icons.Children.ToList();
        var positions = new List<(UIElement Element, double Center)>();
        foreach (var child in rowChildren)
        {
            var len = child is DockIconView ? BaseSize : (Vertical ? _separator.Height : _separator.Width) + 4;
            positions.Add((child, restLength + len / 2));
            restLength += len + Spacing;
        }

        restLength -= Spacing;
        var rootLength = Vertical ? Root.ActualHeight : Root.ActualWidth;
        var start = (rootLength - restLength) / 2;
        var iconCenters = positions.Where(p => p.Element is DockIconView).Select(p => start + p.Center).ToList();

        var scales = MagnificationCalculator.ComputeScales(iconCenters, _pointer, BaseSize, MaxScale);
        for (var i = 0; i < views.Count; i++)
        {
            views[i].SetScaleTarget(scales[i], immediate: false);
        }

        if (!_magnifying)
        {
            _magnifying = true;
            FrameTicker.Run(_magnifyTick);
        }
    }

    private bool MagnifyTick(double dt)
    {
        var moving = false;
        foreach (var view in AllViews())
        {
            moving |= view.StepScale(dt);
        }

        UpdateBarSize();
        if (_labelFor is not null)
        {
            PositionLabel(_labelFor);
        }

        _magnifying = moving;
        return moving;
    }

    // ---- Auto-hide ------------------------------------------------------------------

    private double HiddenOffset() => BarThickness + EdgeGap + 4;

    private void SlideTo(bool hidden)
    {
        _hidden = hidden;
        _slide.Parameters = Motion.Spring(SpringPresets.Smooth);
        _slide.Target = hidden ? HiddenOffset() : 0;
        if (!_sliding)
        {
            _sliding = true;
            FrameTicker.Run(_slideTick);
        }
    }

    private bool SlideTick(double dt)
    {
        var moving = _slide.Step(dt);
        ApplySlide();
        if (!moving)
        {
            _sliding = false;
            UpdateRegion();
        }

        return moving;
    }

    private void ApplySlide()
    {
        ElementCompositionPreview.SetIsTranslationEnabled(Slider, true);
        var o = (float)_slide.Value;
        var t = Settings.Position switch
        {
            DockPosition.Left => new Vector3(-o, 0, 0),
            DockPosition.Right => new Vector3(o, 0, 0),
            _ => new Vector3(0, o, 0),
        };
        ElementCompositionPreview.GetElementVisual(Slider).Properties.InsertVector3("Translation", t);
    }

    // ---- Hit region ------------------------------------------------------------------

    private void UpdateRegion()
    {
        if (_window.Width == 0)
        {
            return;
        }

        if (_hovered)
        {
            WindowHelper.SetHitRegion(this, new RectInt32(0, 0, _window.Width, _window.Height));
            return;
        }

        if (_hidden)
        {
            // A 2px strip on the screen edge wakes the Dock up.
            var strip = (int)Math.Ceiling(2 * _scale);
            WindowHelper.SetHitRegion(this, Settings.Position switch
            {
                DockPosition.Left => new RectInt32(0, 0, strip, _window.Height),
                DockPosition.Right => new RectInt32(_window.Width - strip, 0, strip, _window.Height),
                _ => new RectInt32(0, _window.Height - strip, _window.Width, strip),
            });
            return;
        }

        var length = (RowLength() + Padding * 2) * _scale;
        var thick = (BarThickness + EdgeGap) * _scale;
        WindowHelper.SetHitRegion(this, Settings.Position switch
        {
            DockPosition.Left => new RectInt32(0, (int)((_window.Height - length) / 2), (int)thick, (int)length),
            DockPosition.Right => new RectInt32(_window.Width - (int)thick, (int)((_window.Height - length) / 2), (int)thick, (int)length),
            _ => new RectInt32((int)((_window.Width - length) / 2), _window.Height - (int)thick, (int)length, (int)thick),
        });
    }

    // ---- Label ---------------------------------------------------------------------

    private void ShowLabel(DockIconView view)
    {
        _labelFor = view;
        LabelText.Text = view.Item.DisplayName;
        Label.Visibility = Visibility.Visible;
        PositionLabel(view);
    }

    private void HideLabel(DockIconView? view)
    {
        if (view is null || view == _labelFor)
        {
            _labelFor = null;
            Label.Visibility = Visibility.Collapsed;
        }
    }

    private void PositionLabel(DockIconView view)
    {
        Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = Label.DesiredSize;
        var origin = view.TransformToVisual(Root).TransformPoint(new Point(0, 0));
        var tileSize = view.Length;
        double x, y;
        switch (Settings.Position)
        {
            case DockPosition.Left:
                x = origin.X + DockIconView.IndicatorGap + tileSize + 12;
                y = origin.Y + tileSize / 2 - size.Height / 2;
                break;
            case DockPosition.Right:
                x = origin.X + view.CrossSize - DockIconView.IndicatorGap - tileSize - 12 - size.Width;
                y = origin.Y + tileSize / 2 - size.Height / 2;
                break;
            default:
                x = origin.X + tileSize / 2 - size.Width / 2;
                y = origin.Y + view.CrossSize - DockIconView.IndicatorGap - tileSize - size.Height - 10;
                break;
        }

        Canvas.SetLeft(Label, Math.Max(4, x));
        Canvas.SetTop(Label, Math.Max(0, y));
    }

    // ---- Interaction ----------------------------------------------------------------

    private void Icon_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not DockIconView view)
        {
            return;
        }

        switch (view.Item.Kind)
        {
            case DockItemKind.Stack:
                ShowStack(view);
                break;
            case DockItemKind.Trash:
                _ = _launcher.LaunchAsync(view.Item.Path!);
                break;
            default:
                _vm.ActivateCommand.Execute(view.Item);
                break;
        }

        e.Handled = true;
    }

    private void Icon_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not DockIconView view)
        {
            return;
        }

        HideLabel(null);
        var item = view.Item;
        var menu = new MenuFlyout();
        switch (item.Kind)
        {
            case DockItemKind.App:
                foreach (var w in item.Windows)
                {
                    var handle = w.Handle;
                    var windowItem = new MenuFlyoutItem { Text = w.Title };
                    windowItem.Click += (_, _) => App.Current.Services.GetService<IWindowTracker>()?.Activate(handle);
                    menu.Items.Add(windowItem);
                }

                if (item.Windows.Count > 0)
                {
                    menu.Items.Add(new MenuFlyoutSeparator());
                }

                menu.Items.Add(MenuItem(item.IsRunning ? "Dock.NewWindow" : "Dock.Open", "\uE8A7", () => _vm.NewWindowCommand.Execute(item)));
                menu.Items.Add(MenuItem(item.IsPinned ? "Dock.RemoveFromDock" : "Dock.KeepInDock", item.IsPinned ? "\uE77A" : "\uE718", () => _vm.TogglePinCommand.Execute(item)));
                menu.Items.Add(MenuItem("Dock.ShowInExplorer", "\uE838", () => _vm.RevealCommand.Execute(item)));
                if (item.IsRunning)
                {
                    menu.Items.Add(new MenuFlyoutSeparator());
                    menu.Items.Add(MenuItem("Dock.Quit", "\uE711", () => _vm.QuitCommand.Execute(item)));
                }

                break;
            case DockItemKind.Stack:
                menu.Items.Add(MenuItem("Dock.Open", "\uE838", () => _ = _launcher.LaunchAsync(item.Path!)));
                menu.Items.Add(MenuItem("Dock.RemoveFromDock", "\uE77A", () => _vm.RemoveStackCommand.Execute(item)));
                break;
            case DockItemKind.Trash:
                menu.Items.Add(MenuItem("Dock.Open", "\uE838", () => _ = _launcher.LaunchAsync(item.Path!)));
                break;
        }

        menu.ShowAt(view, new FlyoutShowOptions { Placement = Vertical ? FlyoutPlacementMode.Auto : FlyoutPlacementMode.Top });
        e.Handled = true;
    }

    private void Bar_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var menu = new MenuFlyout();
        var hiding = new ToggleMenuFlyoutItem { Text = Localizer.Get("Dock.AutoHide"), IsChecked = Settings.AutoHide };
        hiding.Click += (_, _) => Settings.AutoHide = hiding.IsChecked;
        var magnify = new ToggleMenuFlyoutItem { Text = Localizer.Get("Dock.Magnification"), IsChecked = Settings.Magnification };
        magnify.Click += (_, _) => Settings.Magnification = magnify.IsChecked;
        menu.Items.Add(hiding);
        menu.Items.Add(magnify);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(MenuItem("Dock.Settings", "\uE713", () => App.Current.OpenSettings("Dock")));
        menu.ShowAt(Bar, e.GetPosition(Bar));
        e.Handled = true;
    }

    private static MenuFlyoutItem MenuItem(string key, string glyph, Action action)
    {
        var item = new MenuFlyoutItem { Text = Localizer.Get(key), Icon = new FontIcon { Glyph = glyph } };
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>Folder stack: a grid of the most recent items, like macOS "Grid" stacks.</summary>
    private void ShowStack(DockIconView view)
    {
        var folder = view.Item.Path!;
        var grid = new GridView { MaxHeight = 420, Width = 380, SelectionMode = ListViewSelectionMode.None, IsItemClickEnabled = true };
        IEnumerable<FileSystemInfo> entries;
        try
        {
            entries = new DirectoryInfo(folder).EnumerateFileSystemInfos()
                .Where(f => !f.Attributes.HasFlag(System.IO.FileAttributes.Hidden))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(24)
                .ToList();
        }
        catch (Exception)
        {
            entries = [];
        }

        var icons = App.Current.Services.GetService<IIconProvider>();
        foreach (var entry in entries)
        {
            var image = new Image { Width = 48, Height = 48 };
            var panel = new StackPanel { Width = 84, Spacing = 4, Padding = new Thickness(4) };
            panel.Children.Add(image);
            panel.Children.Add(new TextBlock { Text = entry.Name, FontSize = 11, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, TextWrapping = TextWrapping.Wrap });
            panel.Tag = entry.FullName;
            grid.Items.Add(panel);
            _ = LoadAsync(image, entry.FullName);
        }

        async Task LoadAsync(Image target, string path)
        {
            if (icons is not null && await icons.GetIconAsync(path, 96) is ImageSource source)
            {
                target.Source = source;
            }
        }

        var flyout = new Flyout();
        grid.ItemClick += (_, e) =>
        {
            if (e.ClickedItem is FrameworkElement { Tag: string path })
            {
                _ = _launcher.LaunchAsync(path);
                flyout.Hide();
            }
        };

        var openFolder = new HyperlinkButton { Content = Localizer.Get("Dock.OpenInExplorer"), HorizontalAlignment = HorizontalAlignment.Right };
        openFolder.Click += (_, _) => { _ = _launcher.LaunchAsync(folder); flyout.Hide(); };
        var header = new TextBlock { Text = view.Item.DisplayName, Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"] };
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(header);
        content.Children.Add(entries.Any() ? grid : new TextBlock { Text = Localizer.Get("Dock.EmptyFolder"), Opacity = 0.6 });
        content.Children.Add(openFolder);
        flyout.Content = content;
        flyout.ShowAt(view, new FlyoutShowOptions { Placement = Vertical ? FlyoutPlacementMode.Auto : FlyoutPlacementMode.Top });
    }

    // ---- Drag & drop to pin -----------------------------------------------------------

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Link;
            e.DragUIOverride.Caption = Localizer.Get("Dock.KeepInDock");
        }
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        foreach (var item in await e.DataView.GetStorageItemsAsync())
        {
            if (item is StorageFolder folder)
            {
                if (!Settings.Stacks.Contains(folder.Path, StringComparer.OrdinalIgnoreCase))
                {
                    Settings.Stacks.Add(folder.Path);
                }
            }
            else if (item is StorageFile file && !Settings.PinnedApps.Any(p => string.Equals(p.Path, file.Path, StringComparison.OrdinalIgnoreCase)))
            {
                Settings.PinnedApps.Add(new PinnedApp(_launcher.GetDisplayName(file.Path), file.Path));
            }
        }
    }
}
