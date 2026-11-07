namespace MajorMode.Core.Layout;

public readonly record struct PointD(double X, double Y);

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public double CenterX => X + Width / 2;

    public double CenterY => Y + Height / 2;

    public static RectD FromEdges(double left, double top, double right, double bottom) =>
        new(left, top, right - left, bottom - top);

    public bool Contains(RectD other, double tolerance = 0.001) =>
        other.X >= X - tolerance && other.Y >= Y - tolerance &&
        other.Right <= Right + tolerance && other.Bottom <= Bottom + tolerance;

    public bool Contains(PointD p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    public RectD Union(RectD other) => FromEdges(
        Math.Min(X, other.X), Math.Min(Y, other.Y), Math.Max(Right, other.Right), Math.Max(Bottom, other.Bottom));

    public RectD Inflate(double dx, double dy) => new(X - dx, Y - dy, Width + 2 * dx, Height + 2 * dy);
}

public readonly record struct LineD(PointD From, PointD To);

public enum TextAnchor
{
    /// <summary>Точка задает правый край текста, по вертикали центр.</summary>
    RightMiddle,

    /// <summary>Точка задает верх текста, по горизонтали центр.</summary>
    TopCenter,
}
