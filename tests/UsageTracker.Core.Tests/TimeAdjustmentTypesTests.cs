using UsageTracker.Core.Enums;
using UsageTracker.Core.Models;
using Xunit;

namespace UsageTracker.Core.Tests;

public sealed class TimeAdjustmentTypesTests
{
    [Theory]
    [InlineData(TimeAdjustmentTarget.Running, TimeAdjustmentTypes.Running)]
    [InlineData(TimeAdjustmentTarget.Foreground, TimeAdjustmentTypes.Foreground)]
    public void ToStorageValue_SupportedTarget_ReturnsExpectedValue(TimeAdjustmentTarget target, string expected)
    {
        var result = TimeAdjustmentTypes.ToStorageValue(target);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToStorageValue_UnsupportedTarget_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeAdjustmentTypes.ToStorageValue((TimeAdjustmentTarget)999));
    }
}
