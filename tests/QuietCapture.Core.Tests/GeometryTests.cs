using QuietCapture.Core.Models;

namespace QuietCapture.Core.Tests;

public sealed class GeometryTests
{
    [Fact]
    public void PixelRect_AllowsNegativeCoordinates()
    {
        var rect = new PixelRect(
            -1920,
            -200,
            1280,
            720);

        Assert.Equal(-1920, rect.X);
        Assert.Equal(-200, rect.Y);
        Assert.Equal(-640, rect.Right);
        Assert.Equal(520, rect.Bottom);
    }

    [Fact]
    public void ToEvenDimensionsDown_ReducesOddDimensionsWithoutMovingOrigin()
    {
        var rect = new PixelRect(
            -101,
            37,
            1919,
            1079);

        PixelRect normalized =
            rect.ToEvenDimensionsDown();

        Assert.Equal(-101, normalized.X);
        Assert.Equal(37, normalized.Y);
        Assert.Equal(1918, normalized.Width);
        Assert.Equal(1078, normalized.Height);
        Assert.True(normalized.HasEvenDimensions);
    }

    [Fact]
    public void ToEvenDimensionsDown_DoesNotChangeAlreadyEvenSize()
    {
        var size = new PixelSize(
            1920,
            1080);

        Assert.Equal(
            size,
            size.ToEvenDimensionsDown());
    }

    [Fact]
    public void PixelSize_RejectsNonPositiveDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PixelSize(0, 1080));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PixelSize(1920, -1));
    }
}
