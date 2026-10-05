namespace QuietCapture.Core.Models;

public readonly record struct PixelSize
{
    public PixelSize(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                "Pixel width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                height,
                "Pixel height must be positive.");
        }

        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public bool HasEvenDimensions =>
        (Width & 1) == 0 &&
        (Height & 1) == 0;

    public PixelSize ToEvenDimensionsDown()
    {
        int width = Width & ~1;
        int height = Height & ~1;

        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException(
                "The pixel size is too small to normalize to positive even dimensions.");
        }

        return new PixelSize(width, height);
    }
}
