using SpartanX.Core.Dock;

namespace SpartanX.Core.Tests;

public class MagnificationTests
{
    private static readonly double[] Centers = [25, 75, 125, 175, 225, 275, 325];

    [Fact]
    public void No_cursor_means_rest_size()
    {
        Assert.All(MagnificationCalculator.ComputeScales(Centers, null, 50, 2), s => Assert.Equal(1, s));
    }

    [Fact]
    public void Icon_under_cursor_gets_max_scale_and_neighbours_fall_off()
    {
        var scales = MagnificationCalculator.ComputeScales(Centers, 175, 50, 2);

        Assert.Equal(2, scales[3], 6);
        Assert.True(scales[2] < scales[3] && scales[2] > 1);
        Assert.Equal(scales[2], scales[4], 6); // symmetric
        Assert.True(scales[1] < scales[2]);
        Assert.Equal(1, scales[0], 6);         // outside the 3-icon radius
    }

    [Fact]
    public void Disabled_when_max_scale_is_one()
    {
        Assert.All(MagnificationCalculator.ComputeScales(Centers, 175, 50, 1), s => Assert.Equal(1, s));
    }

    [Fact]
    public void Extra_length_sums_growth()
    {
        Assert.Equal(75, MagnificationCalculator.ExtraLength([1.5, 2, 1], 50), 6);
    }
}
