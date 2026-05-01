using UsageTracker.Infrastructure.Services;
using Xunit;

namespace UsageTracker.Core.Tests;

public sealed class AutoStartServiceTests
{
    [Fact]
    public void BuildRunCommand_QuotesPathAndAppendsMinimizedFlag()
    {
        var executablePath = @"C:\Program Files\Usage Tracker\UsageTracker.App.exe";

        var result = AutoStartService.BuildRunCommand(executablePath);

        Assert.Equal("\"C:\\Program Files\\Usage Tracker\\UsageTracker.App.exe\" --minimized", result);
    }

    [Fact]
    public void BuildRunCommand_TrimmedWhitespaceAroundPath()
    {
        var result = AutoStartService.BuildRunCommand("  C:\\UsageTracker\\UsageTracker.App.exe  ");

        Assert.Equal("\"C:\\UsageTracker\\UsageTracker.App.exe\" --minimized", result);
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
