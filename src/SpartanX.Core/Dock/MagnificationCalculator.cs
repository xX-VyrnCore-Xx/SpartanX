namespace SpartanX.Core.Dock;

/// <summary>
/// macOS-style Dock magnification. Each icon scales with a cosine falloff based on its
/// distance from the cursor, so neighbours grow smoothly instead of in steps.
/// </summary>
public static class MagnificationCalculator
{
    /// <param name="restCenters">Centre of each icon along the dock axis, at rest size.</param>
    /// <param name="cursor">Cursor coordinate on the same axis, or null when outside the dock.</param>
    /// <param name="baseSize">Icon size at rest.</param>
    /// <param name="maxScale">Scale of the icon directly under the cursor.</param>
    /// <param name="influenceIcons">How many icon-widths away the effect reaches on each side.</param>
    public static double[] ComputeScales(
        IReadOnlyList<double> restCenters,
        double? cursor,
        double baseSize,
        double maxScale,
        double influenceIcons = 3.0)
    {
        var scales = new double[restCenters.Count];
        if (cursor is null || maxScale <= 1.0 || baseSize <= 0)
        {
            Array.Fill(scales, 1.0);
            return scales;
        }

        var radius = baseSize * influenceIcons;
        for (var i = 0; i < scales.Length; i++)
        {
            var distance = Math.Abs(restCenters[i] - cursor.Value);
            if (distance >= radius)
            {
                scales[i] = 1.0;
                continue;
            }

            // (cos(pi * d / r) + 1) / 2 goes 1 -> 0 smoothly with zero slope at both ends.
            var falloff = (Math.Cos(Math.PI * distance / radius) + 1) / 2;
            scales[i] = 1.0 + (maxScale - 1.0) * falloff;
        }

        return scales;
    }

    /// <summary>Total extra length the dock gains for the given scales.</summary>
    public static double ExtraLength(IEnumerable<double> scales, double baseSize) =>
        scales.Sum(s => (s - 1.0) * baseSize);
}
