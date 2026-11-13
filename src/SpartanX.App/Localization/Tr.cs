using Microsoft.UI.Xaml.Markup;
using SpartanX.Core.Localization;

namespace SpartanX.App.Localization;

/// <summary>
/// XAML markup extension: <c>Text="{l:Tr Key=Settings.Dock.Title}"</c>.
/// Strings are resolved when the element is created, so windows rebuilt after a language
/// change pick up the new language.
/// </summary>
[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed partial class Tr : MarkupExtension
{
    public string Key { get; set; } = "";

    protected override object ProvideValue() => Localizer.Get(Key);
}
