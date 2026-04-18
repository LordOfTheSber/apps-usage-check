using AppsUsageCheck.Core.Services;
using Xunit;

namespace AppsUsageCheck.Core.Tests;

public sealed class TimeFormatterTests
{
    [Theory]
    [InlineData(0, "0s")]
    [InlineData(59, "59s")]
    [InlineData(60, "1m")]
    [InlineData(3661, "1h 1m 1s")]
    [InlineData(90061, "1d 1h 1m 1s")]
    public void FormatDuration_FormatsExpectedOutput(long seconds, string expected)
    {
        var formatted = TimeFormatter.FormatDuration(seconds);

        Assert.Equal(expected, formatted);
    }
}
