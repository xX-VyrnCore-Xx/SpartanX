using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;

namespace SpartanX.Core.Localization;

/// <summary>
/// Tiny JSON-based localisation. Strings live in Localization/Strings/{lang}.json as embedded
/// resources; missing keys fall back to English, then to the key itself so gaps are visible.
/// Unlike .resw this allows switching language at runtime (needed by the Welcome flow).
/// </summary>
public static class Localizer
{
    public const string FallbackLanguage = "en";

    public static readonly IReadOnlyList<LanguageOption> SupportedLanguages =
    [
        new("en", "English"),
        new("it", "Italiano"),
    ];

    private static readonly Lazy<FrozenDictionary<string, string>> Fallback = new(() => LoadTable(FallbackLanguage));
    private static FrozenDictionary<string, string> _table = FrozenDictionary<string, string>.Empty;

    static Localizer() => SetLanguage("");

    public static string CurrentLanguage { get; private set; } = FallbackLanguage;

    public static event EventHandler? LanguageChanged;

    /// <summary>Selects a language; empty or unsupported tags follow the OS UI language.</summary>
    public static void SetLanguage(string? tag)
    {
        var resolved = Resolve(tag);
        CurrentLanguage = resolved;
        _table = resolved == FallbackLanguage ? Fallback.Value : LoadTable(resolved);
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key) =>
        _table.TryGetValue(key, out var value) || Fallback.Value.TryGetValue(key, out value) ? value : key;

    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    internal static string Resolve(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            tag = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }

        var two = tag.Split('-', '_')[0].ToLowerInvariant();
        return SupportedLanguages.Any(l => l.Tag == two) ? two : FallbackLanguage;
    }

    internal static FrozenDictionary<string, string> LoadTable(string language)
    {
        using var stream = typeof(Localizer).Assembly.GetManifestResourceStream($"SpartanX.Strings.{language}.json");
        if (stream is null)
        {
            return FrozenDictionary<string, string>.Empty;
        }

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
        return dict.ToFrozenDictionary(StringComparer.Ordinal);
    }
}

public sealed record LanguageOption(string Tag, string NativeName)
{
    public override string ToString() => NativeName;
}
