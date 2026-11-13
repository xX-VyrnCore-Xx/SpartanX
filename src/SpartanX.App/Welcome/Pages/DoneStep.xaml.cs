using Microsoft.UI.Xaml.Controls;
using SpartanX.App.Animation;
using SpartanX.Core.Welcome;

namespace SpartanX.App.Welcome.Pages;

public sealed partial class DoneStep : UserControl
{
    public DoneStep(WelcomeViewModel vm)
    {
        InitializeComponent();
        Loaded += (_, _) => Motion.FadeScale(Logo, show: true, fromScale: 0.5, delayMs: 120);
    }
}
