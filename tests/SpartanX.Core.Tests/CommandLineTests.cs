using SpartanX.Core.Lifecycle;

namespace SpartanX.Core.Tests;

public class CommandLineTests
{
    [Theory]
    [InlineData(new string[0], LaunchCommand.Run, false)]
    [InlineData(new[] { "--autostart" }, LaunchCommand.Run, false)]
    [InlineData(new[] { "--quit" }, LaunchCommand.Quit, false)]
    [InlineData(new[] { "/shutdown" }, LaunchCommand.Quit, false)]
    [InlineData(new[] { "--uninstall" }, LaunchCommand.Uninstall, false)]
    [InlineData(new[] { "--uninstall", "--purge" }, LaunchCommand.Uninstall, true)]
    [InlineData(new[] { "--quit", "--uninstall" }, LaunchCommand.Uninstall, false)]
    [InlineData(new[] { "--uninstall", "--quit" }, LaunchCommand.Uninstall, false)]
    [InlineData(new[] { "--quit", "--purge" }, LaunchCommand.Quit, false)]
    [InlineData(new[] { "--UNINSTALL", "--PURGE" }, LaunchCommand.Uninstall, true)]
    public void Parses_commands(string[] args, LaunchCommand command, bool purge)
    {
        var o = CommandLineOptions.Parse(args);
        Assert.Equal(command, o.Command);
        Assert.Equal(purge, o.Purge);
    }

    [Theory]
    [InlineData("--restarted", true)]
    [InlineData("--updated", true)]
    [InlineData("--autostart", false)]
    public void Wait_for_previous(string arg, bool expected) =>
        Assert.Equal(expected, CommandLineOptions.Parse([arg]).WaitForPrevious);

    [Fact]
    public void Silent_flag() => Assert.True(CommandLineOptions.Parse(["--quit", "--silent"]).Silent);
}
