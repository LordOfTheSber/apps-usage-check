using AppsUsageCheck.Core.Services;
using Xunit;

namespace AppsUsageCheck.Core.Tests;

public sealed class ProcessNameNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(".exe", "")]
    [InlineData("Code.exe", "code")]
    [InlineData("  Code.EXE  ", "code")]
    [InlineData("notepad", "notepad")]
    public void Normalize_ReturnsExpectedNormalizedName(string? input, string expected)
    {
        var result = ProcessNameNormalizer.Normalize(input);

        Assert.Equal(expected, result);
    }
}
