using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SpartanX.App.Animation;
using SpartanX.Core.Welcome;

namespace SpartanX.App.Welcome.Pages;

public sealed partial class HelloStep : UserControl
{
    private readonly WelcomeViewModel _vm;
    private readonly DispatcherQueueTimer _timer;
    private int _index;

    public HelloStep(WelcomeViewModel vm)
    {
        _vm = vm;
        InitializeComponent();

        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(2.2);
        _timer.Tick += (_, _) => NextGreeting();
        Loaded += (_, _) =>
        {
            Motion.FadeScale(Greeting, show: true, fromScale: 0.85, delayMs: 150);
            Motion.FadeScale(StartButton, show: true, fromScale: 0.6, delayMs: 700);
            _timer.Start();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    private async void NextGreeting()
    {
        Motion.FadeScale(Greeting, show: false, fromScale: 1.06);
        await Task.Delay(Motion.Duration(260));
        _index = (_index + 1) % WelcomeViewModel.Greetings.Count;
        Greeting.Text = WelcomeViewModel.Greetings[_index];
        Motion.FadeScale(Greeting, show: true, fromScale: 0.92);
    }

    private void Start_Click(object sender, RoutedEventArgs e) => _vm.NextCommand.Execute(null);
}
