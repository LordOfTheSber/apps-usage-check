using AppsUsageCheck.Infrastructure.Services;
using Xunit;

namespace AppsUsageCheck.Core.Tests;

public sealed class AutoStartServiceTests
{
    [Fact]
    public void BuildRunCommand_QuotesPathAndAppendsMinimizedFlag()
    {
        var executablePath = @"C:\Program Files\Apps Usage Check\AppsUsageCheck.App.exe";

        var result = AutoStartService.BuildRunCommand(executablePath);

        Assert.Equal("\"C:\\Program Files\\Apps Usage Check\\AppsUsageCheck.App.exe\" --minimized", result);
    }

    [Fact]
    public void BuildRunCommand_TrimmedWhitespaceAroundPath()
    {
        var result = AutoStartService.BuildRunCommand("  C:\\AppsUsageCheck\\AppsUsageCheck.App.exe  ");

        Assert.Equal("\"C:\\AppsUsageCheck\\AppsUsageCheck.App.exe\" --minimized", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildRunCommand_BlankPath_ThrowsArgumentException(string? executablePath)
    {
        Assert.Throws<ArgumentException>(() => AutoStartService.BuildRunCommand(executablePath!));
    }
}
