using Microsoft.UI.Xaml.Controls;
using SpartanX.Core.Localization;

namespace SpartanX.App.Helpers;

/// <summary>
/// Two-way binds a ComboBox to an enum property with localised item names
/// (key pattern: <c>Enum.{Type}.{Value}</c>).
/// </summary>
internal static class EnumBinder
{
    public static void Bind<T>(ComboBox box, Func<T> get, Action<T> set) where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        box.Items.Clear();
        foreach (var v in values)
        {
            box.Items.Add(new ComboBoxItem { Content = Localizer.Get($"Enum.{typeof(T).Name}.{v}"), Tag = v });
        }

        box.SelectedIndex = Array.IndexOf(values, get());
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is ComboBoxItem { Tag: T value } && !EqualityComparer<T>.Default.Equals(value, get()))
            {
                set(value);
            }
        };
    }
}
