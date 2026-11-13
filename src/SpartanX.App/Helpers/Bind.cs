using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SpartanX.Core.Localization;
using Windows.Storage.Streams;

namespace SpartanX.App.Helpers;

/// <summary>Static functions for x:Bind function bindings (cheaper and simpler than converters).</summary>
public static class Bind
{
    public static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility Collapsed(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility NotNull(object? value) => value is null ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility IsNull(object? value) => value is null ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility NotEmpty(string? value) => string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;

    public static bool Not(bool value) => !value;

    public static ImageSource? Image(object? value) => value as ImageSource;

    public static double Percent(double value) => value * 100;

    public static string T(string key) => Localizer.Get(key);

    public static string Format(string key, double value) => Localizer.Format(key, Math.Round(value, 2));

    public static string Round(double value) => Math.Round(value).ToString(System.Globalization.CultureInfo.CurrentCulture);

    public static string Multiplier(double value) => $"{value:0.00}×";

    public static string Seconds(double value) => $"{value:0.00} s";

    /// <summary>Converts raw image bytes to a BitmapImage (album art).</summary>
    public static async Task<ImageSource?> FromBytesAsync(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        var image = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        await image.SetSourceAsync(stream);
        return image;
    }
}
