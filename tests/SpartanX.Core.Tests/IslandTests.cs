using SpartanX.Core.Island;

namespace SpartanX.Core.Tests;

public class IslandTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Highest_priority_activity_wins_and_transients_expire()
    {
        var time = new ManualTime(T0);
        var m = new IslandActivityManager(time);

        m.SetPersistent(IslandActivityKind.Media);
        Assert.Equal(IslandActivityKind.Media, m.Current.Kind);

        m.ShowTransient(IslandActivityKind.Volume, TimeSpan.FromSeconds(2));
        Assert.Equal(IslandActivityKind.Volume, m.Current.Kind);

        time.Advance(TimeSpan.FromSeconds(3));
        m.Tick();
        Assert.Equal(IslandActivityKind.Media, m.Current.Kind);

        m.Remove(IslandActivityKind.Media);
        Assert.Equal(IslandActivityKind.Idle, m.Current.Kind);
    }

    [Fact]
    public void Reposting_a_transient_extends_it()
    {
        var time = new ManualTime(T0);
        var m = new IslandActivityManager(time);
        m.ShowTransient(IslandActivityKind.Volume, TimeSpan.FromSeconds(2));
        time.Advance(TimeSpan.FromSeconds(1.5));
        m.ShowTransient(IslandActivityKind.Volume, TimeSpan.FromSeconds(2));
        time.Advance(TimeSpan.FromSeconds(1.5));
        m.Tick();

        Assert.Equal(IslandActivityKind.Volume, m.Current.Kind);
        Assert.Single(m.All);
    }

    [Fact]
    public void Countdown_pauses_resumes_and_completes()
    {
        var time = new ManualTime(T0);
        var timer = new CountdownTimer(time);
        var completed = 0;
        timer.Completed += (_, _) => completed++;

        timer.Start(TimeSpan.FromMinutes(1));
        time.Advance(TimeSpan.FromSeconds(20));
        timer.Pause();
        time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromSeconds(40), timer.Remaining);

        timer.Resume();
        time.Advance(TimeSpan.FromSeconds(40));
        timer.Tick();
        Assert.Equal(1, completed);
        Assert.False(timer.IsActive);
    }

    [Theory]
    [InlineData(65, "1:05")]
    [InlineData(3725, "1:02:05")]
    public void Countdown_format(int seconds, string expected) =>
        Assert.Equal(expected, CountdownTimer.Format(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Expanded_is_larger_than_peek_is_larger_than_compact()
    {
        foreach (var kind in Enum.GetValues<IslandActivityKind>())
        {
            var c = IslandLayout.GetSize(IslandMode.Compact, kind);
            var p = IslandLayout.GetSize(IslandMode.Peek, kind);
            var e = IslandLayout.GetSize(IslandMode.Expanded, kind);
            Assert.True(c.Height < p.Height && p.Height <= e.Height, kind.ToString());
            Assert.True(e.Width <= IslandLayout.Max.Width && e.Height <= IslandLayout.Max.Height);
        }
    }
}
