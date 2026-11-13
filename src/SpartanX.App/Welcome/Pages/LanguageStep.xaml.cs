using Microsoft.UI.Xaml.Controls;
using SpartanX.Core.Localization;
using SpartanX.Core.Welcome;

namespace SpartanX.App.Welcome.Pages;

public sealed partial class LanguageStep : UserControl
{
    public LanguageStep(WelcomeViewModel vm)
    {
        ViewModel = vm;
        InitializeComponent();
        Languages.SelectedItem = vm.SelectedLanguage;
        UpdateTexts();
    }

    public WelcomeViewModel ViewModel { get; }

    private void Languages_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Languages.SelectedItem is LanguageOption option)
        {
            ViewModel.SelectedLanguage = option;
            UpdateTexts();
        }
    }

    // This page re-translates itself live; later pages are created after the choice.
    private void UpdateTexts()
    {
        TitleText.Text = Localizer.Get("Welcome.Language.Title");
        SubtitleText.Text = Localizer.Get("Welcome.Language.Subtitle");
    }
}
