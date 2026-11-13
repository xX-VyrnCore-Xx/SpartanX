namespace SpartanX.Core.Input;

[Flags]
public enum KeyModifiers
{
    None = 0,
    // Values match RegisterHotKey's MOD_* flags.
    Alt = 0x1,
    Ctrl = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>A global shortcut like "Win+Alt+D", convertible to Win32 virtual-key codes.</summary>
public sealed record KeyGesture(KeyModifiers Modifiers, int VirtualKey, string KeyName)
{
    private static readonly Dictionary<string, int> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20, ["Enter"] = 0x0D, ["Tab"] = 0x09, ["Esc"] = 0x1B, ["Escape"] = 0x1B,
        ["Backspace"] = 0x08, ["Delete"] = 0x2E, ["Insert"] = 0x2D, ["Home"] = 0x24, ["End"] = 0x23,
        ["PageUp"] = 0x21, ["PageDown"] = 0x22, ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28,
        ["PlayPause"] = 0xB3, ["NextTrack"] = 0xB0, ["PrevTrack"] = 0xB1,
        ["VolumeUp"] = 0xAF, ["VolumeDown"] = 0xAE, ["VolumeMute"] = 0xAD,
        [","] = 0xBC, ["."] = 0xBE, ["-"] = 0xBD, ["="] = 0xBB,
    };

    public static bool TryParse(string? text, out KeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var modifiers = KeyModifiers.None;
        string? key = null;
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= KeyModifiers.Ctrl; break;
                case "alt": modifiers |= KeyModifiers.Alt; break;
                case "shift": modifiers |= KeyModifiers.Shift; break;
                case "win" or "windows" or "meta": modifiers |= KeyModifiers.Win; break;
                default:
                    if (key is not null)
                    {
                        return false; // Two non-modifier keys.
                    }

                    key = part;
                    break;
            }
        }

        if (key is null || !TryGetVirtualKey(key, out var vk, out var normalized))
        {
            return false;
        }

        gesture = new KeyGesture(modifiers, vk, normalized);
        return true;
    }

    public static bool TryGetVirtualKey(string key, out int vk, out string normalized)
    {
        normalized = key;
        vk = 0;
        if (key.Length == 1 && char.IsAsciiLetterOrDigit(key[0]))
        {
            normalized = key.ToUpperInvariant();
            vk = normalized[0];
            return true;
        }

        if (key.Length is 2 or 3 && (key[0] is 'F' or 'f') && int.TryParse(key[1..], out var f) && f is >= 1 and <= 24)
        {
            normalized = $"F{f}";
            vk = 0x70 + f - 1;
            return true;
        }

        if (NamedKeys.TryGetValue(key, out vk))
        {
            normalized = NamedKeys.Keys.First(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            return true;
        }

        return false;
    }

    /// <summary>Canonical text form, modifiers in Windows order.</summary>
    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(KeyModifiers.Win)) parts.Add("Win");
        if (Modifiers.HasFlag(KeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        parts.Add(KeyName);
        return string.Join('+', parts);
    }
}
