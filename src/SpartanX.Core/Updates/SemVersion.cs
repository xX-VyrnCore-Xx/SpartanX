namespace SpartanX.Core.Updates;

/// <summary>Minimal SemVer 2.0 (major.minor.patch[-prerelease]) with correct precedence.</summary>
public sealed record SemVersion(int Major, int Minor, int Patch, string? Prerelease) : IComparable<SemVersion>
{
    public bool IsPrerelease => !string.IsNullOrEmpty(Prerelease);

    public static bool TryParse(string? text, out SemVersion version)
    {
        version = new SemVersion(0, 0, 0, null);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim().TrimStart('v', 'V');
        var plus = text.IndexOf('+');
        if (plus >= 0)
        {
            text = text[..plus];
        }

        string? pre = null;
        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            pre = text[(dash + 1)..];
            text = text[..dash];
        }

        var parts = text.Split('.');
        if (parts.Length is < 1 or > 3)
        {
            return false;
        }

        var nums = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out nums[i]) || nums[i] < 0)
            {
                return false;
            }
        }

        version = new SemVersion(nums[0], nums[1], nums[2], pre);
        return true;
    }

    public int CompareTo(SemVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var c = Major.CompareTo(other.Major);
        if (c == 0) c = Minor.CompareTo(other.Minor);
        if (c == 0) c = Patch.CompareTo(other.Patch);
        if (c != 0)
        {
            return c;
        }

        // A release has higher precedence than any of its pre-releases.
        if (IsPrerelease != other.IsPrerelease)
        {
            return IsPrerelease ? -1 : 1;
        }

        return IsPrerelease ? ComparePrerelease(Prerelease!, other.Prerelease!) : 0;
    }

    private static int ComparePrerelease(string a, string b)
    {
        var pa = a.Split('.');
        var pb = b.Split('.');
        for (var i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            var aNum = int.TryParse(pa[i], out var na);
            var bNum = int.TryParse(pb[i], out var nb);
            var c = (aNum, bNum) switch
            {
                (true, true) => na.CompareTo(nb),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(pa[i], pb[i]),
            };
            if (c != 0)
            {
                return c;
            }
        }

        return pa.Length.CompareTo(pb.Length);
    }

    public static bool operator >(SemVersion a, SemVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(SemVersion a, SemVersion b) => a.CompareTo(b) < 0;

    public override string ToString() => IsPrerelease ? $"{Major}.{Minor}.{Patch}-{Prerelease}" : $"{Major}.{Minor}.{Patch}";
}
