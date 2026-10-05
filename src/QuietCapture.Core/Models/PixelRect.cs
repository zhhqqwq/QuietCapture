namespace QuietCapture.Core.Models;

public readonly record struct PixelRect
{
    public PixelRect(
        int x,
        int y,
        int width,
        int height)
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

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public int Right => checked(X + Width);

    public int Bottom => checked(Y + Height);

    public PixelSize Size => new(Width, Height);

    public bool HasEvenDimensions =>
        Size.HasEvenDimensions;

    public PixelRect ToEvenDimensionsDown()
    {
        PixelSize even = Size.ToEvenDimensionsDown();
        return new PixelRect(
            X,
            Y,
            even.Width,
            even.Height);
    }
}
