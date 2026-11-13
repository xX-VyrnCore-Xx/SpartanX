using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpartanX.Core.Localization;
using SpartanX.Core.Settings;

namespace SpartanX.Core.Welcome;

/// <summary>Pages of the first-run Setup Assistant, in order.</summary>
public enum WelcomeStep
{
    Hello,
    Language,
    Appearance,
    Dock,
    Island,
    Privacy,
    Done,
}

/// <summary>Drives the macOS-style Setup Assistant. Choices are written straight into settings
/// so the live Dock/Island preview reacts as the user picks.</summary>
public sealed partial class WelcomeViewModel(ISettingsService settings) : ObservableObject
{
    /// <summary>"Hello" in many languages, cycled on the first screen.</summary>
    public static readonly IReadOnlyList<string> Greetings =
    [
        "hello", "ciao", "bonjour", "hola", "hallo", "olá", "こんにちは", "привет", "你好", "안녕하세요", "merhaba", "hej", "γεια σου", "salve",
    ];

    public AppSettings Settings => settings.Current;

    public IReadOnlyList<LanguageOption> Languages => Localizer.SupportedLanguages;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack), nameof(StepIndex), nameof(Progress), nameof(IsFirst), nameof(IsLast))]
    public partial WelcomeStep Step { get; set; } = WelcomeStep.Hello;

    /// <summary>+1 when moving forward, -1 when going back. The view uses it for slide direction.</summary>
    public int LastDirection { get; private set; } = 1;

    public bool CanGoBack => Step > WelcomeStep.Language;
    public bool IsFirst => Step == WelcomeStep.Hello;
    public bool IsLast => Step == WelcomeStep.Done;
    public int StepIndex => (int)Step;
    public static int StepCount => Enum.GetValues<WelcomeStep>().Length;
    public double Progress => (double)Step / (StepCount - 1);

    public event EventHandler? Finished;

    public LanguageOption SelectedLanguage
    {
        get => Languages.FirstOrDefault(l => l.Tag == Localizer.CurrentLanguage) ?? Languages[0];
        set
        {
            if (value is null || value.Tag == Localizer.CurrentLanguage)
            {
                return;
            }

            Settings.General.Language = value.Tag;
            Localizer.SetLanguage(value.Tag);
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (Step == WelcomeStep.Done)
        {
            Finish();
            return;
        }

        LastDirection = 1;
        Step += 1;
    }

    [RelayCommand]
    private void Back()
    {
        if (!CanGoBack)
        {
            return;
        }

        LastDirection = -1;
        Step -= 1;
    }

    [RelayCommand]
    private void Finish()
    {
        if (string.IsNullOrEmpty(Settings.General.Language))
        {
            Settings.General.Language = Localizer.CurrentLanguage;
        }

        Settings.General.FirstRunCompleted = true;
        _ = settings.SaveAsync();
        Finished?.Invoke(this, EventArgs.Empty);
    }

}
