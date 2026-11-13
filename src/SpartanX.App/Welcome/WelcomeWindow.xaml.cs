using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SpartanX.App.Animation;
using SpartanX.App.Interop;
using SpartanX.App.Services;
using SpartanX.App.Welcome.Pages;
using SpartanX.Core.Abstractions;
using SpartanX.Core.Localization;
using SpartanX.Core.Welcome;

namespace SpartanX.App.Welcome;

/// <summary>
/// First-run Setup Assistant in the spirit of macOS: a multilingual "hello", then a few
/// focused pages with spring-y slide transitions.
/// </summary>
public sealed partial class WelcomeWindow : Window
{
    private readonly WelcomeViewModel _vm;
    private readonly IStartupService _startup;
    private bool _completed;

    internal WelcomeWindow(WelcomeViewModel viewModel, ThemeService theme, IStartupService startup)
    {
        _vm = viewModel;
        _startup = startup;
        InitializeComponent();

        Title = "SpartanX";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarArea);
        WindowHelper.SetIcon(this);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        theme.Register(this);
        WindowHelper.CenterOnPrimary(this, 920, 660);

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WelcomeViewModel.Step))
            {
                ShowStep();
            }
        };
        _vm.Finished += (_, _) => Complete();
        Closed += (_, _) =>
        {
            if (!_completed)
            {
                // Closing the assistant early still leaves a working shell with defaults.
                _completed = true;
                _vm.FinishCommand.Execute(null);
                Completed?.Invoke(this, EventArgs.Empty);
            }
        };

        // The Language page switches language live: keep the footer in sync.
        EventHandler onLanguage = (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            BackText.Text = Localizer.Get("Welcome.Back");
            NextText.Text = Localizer.Get(_vm.IsLast ? "Welcome.Start" : "Welcome.Continue");
        });
        Localizer.LanguageChanged += onLanguage;
        Closed += (_, _) => Localizer.LanguageChanged -= onLanguage;

        Root.Loaded += (_, _) =>
        {
            StartAurora();
            ShowStep();
        };
    }

    public event EventHandler? Completed;

    private void Complete()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        try
        {
            _startup.SetEnabled(_vm.Settings.General.LaunchAtStartup);
        }
        catch (Exception e)
        {
            App.Log(e);
        }

        Completed?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void ShowStep()
    {
        UIElement page = _vm.Step switch
        {
            WelcomeStep.Hello => new HelloStep(_vm),
            WelcomeStep.Language => new LanguageStep(_vm),
            WelcomeStep.Appearance => new AppearanceStep(_vm),
            WelcomeStep.Dock => new DockStep(_vm),
            WelcomeStep.Island => new IslandStep(_vm),
            WelcomeStep.Privacy => new PrivacyStep(_vm),
            _ => new DoneStep(_vm),
        };

        Stage.Children.Clear();
        Stage.Children.Add(page);
        Motion.SlideIn(page, 70 * _vm.LastDirection, 0);

        var isHello = _vm.IsFirst;
        Footer.Visibility = isHello ? Visibility.Collapsed : Visibility.Visible;
        Stage.Margin = isHello ? new Thickness(0, 40, 0, 0) : new Thickness(0, 40, 0, 96);
        Motion.FadeScale(Aurora, show: isHello || _vm.IsLast, fromScale: 1.05);

        BackButton.Visibility = _vm.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
        BackText.Text = Localizer.Get("Welcome.Back");
        NextText.Text = Localizer.Get(_vm.IsLast ? "Welcome.Start" : "Welcome.Continue");
        UpdateDots();
    }

    private void UpdateDots()
    {
        Dots.Children.Clear();
        for (var i = 1; i < WelcomeViewModel.StepCount; i++)
        {
            var active = i == _vm.StepIndex;
            Dots.Children.Add(new Rectangle
            {
                Width = active ? 22 : 7,
                Height = 7,
                RadiusX = 3.5,
                RadiusY = 3.5,
                Fill = active
                    ? (Brush)Application.Current.Resources["SpartanAccentBrush"]
                    : (Brush)Application.Current.Resources["ControlStrongFillColorDisabledBrush"],
            });
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e) => _vm.NextCommand.Execute(null);

    private void Back_Click(object sender, RoutedEventArgs e) => _vm.BackCommand.Execute(null);

    /// <summary>Endless, slow drift of the three colour blobs on the compositor thread.</summary>
    private void StartAurora()
    {
        if (Motion.Reduced)
        {
            return;
        }

        Drift(Blob1, new Vector3(120, 90, 0), 9);
        Drift(Blob2, new Vector3(-140, -70, 0), 11);
        Drift(Blob3, new Vector3(-90, 120, 0), 13);
    }

    private static void Drift(UIElement element, Vector3 by, double seconds)
    {
        var visual = Motion.VisualOf(element);
        var compositor = visual.Compositor;
        Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.45f, 0f), new Vector2(0.55f, 1f));
        var animation = compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(0f, Vector3.Zero);
        animation.InsertKeyFrame(1f, by, ease);
        animation.Duration = TimeSpan.FromSeconds(seconds);
        animation.IterationBehavior = AnimationIterationBehavior.Forever;
        animation.Direction = AnimationDirection.Alternate;
        visual.StartAnimation("Translation", animation);
    }
}
