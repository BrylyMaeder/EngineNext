namespace EngineNext.Core;

public readonly record struct Vec2(float X, float Y)
{
    public static readonly Vec2 Zero = new(0f, 0f);
    public static readonly Vec2 One = new(1f, 1f);

    public float Length() => MathF.Sqrt((X * X) + (Y * Y));

    public Vec2 Normalized()
    {
        var len = Length();
        return len <= 0.0001f ? Zero : new Vec2(X / len, Y / len);
    }

    public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => new(
        a.X + ((b.X - a.X) * t),
        a.Y + ((b.Y - a.Y) * t));

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, float b) => new(a.X * b, a.Y * b);
}

public readonly record struct SizeI(int Width, int Height);

public readonly record struct EngineColor(byte R, byte G, byte B, byte A = 255)
{
    public static readonly EngineColor White = new(255, 255, 255, 255);
    public static readonly EngineColor Black = new(0, 0, 0, 255);
    public static readonly EngineColor Transparent = new(0, 0, 0, 0);
    public static readonly EngineColor Gray = new(140, 140, 140, 255);

    public static EngineColor FromHex(string hex)
    {
        var raw = (hex ?? string.Empty).Trim().TrimStart('#');
        if (raw.Length == 6)
            return new(
                Convert.ToByte(raw.Substring(0, 2), 16),
                Convert.ToByte(raw.Substring(2, 2), 16),
                Convert.ToByte(raw.Substring(4, 2), 16),
                255);
        if (raw.Length == 8)
            return new(
                Convert.ToByte(raw.Substring(0, 2), 16),
                Convert.ToByte(raw.Substring(2, 2), 16),
                Convert.ToByte(raw.Substring(4, 2), 16),
                Convert.ToByte(raw.Substring(6, 2), 16));
        return White;
    }

    public static EngineColor Lerp(EngineColor a, EngineColor b, float t) => new(
        (byte)(a.R + ((b.R - a.R) * t)),
        (byte)(a.G + ((b.G - a.G) * t)),
        (byte)(a.B + ((b.B - a.B) * t)),
        (byte)(a.A + ((b.A - a.A) * t)));
}

public readonly record struct RectF(float X, float Y, float Width, float Height)
{
    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public float CenterX => X + (Width * 0.5f);
    public float CenterY => Y + (Height * 0.5f);

    public bool Contains(Vec2 point) =>
        point.X >= Left && point.X <= Right &&
        point.Y >= Top && point.Y <= Bottom;

    public RectF Inflate(float amount) => new(X - amount, Y - amount, Width + amount + amount, Height + amount + amount);

    public bool Intersects(RectF other) =>
        Right > other.Left && Left < other.Right &&
        Bottom > other.Top && Top < other.Bottom;
}

public enum TextAlign
{
    Left,
    Center,
    Right,
}

public enum UIHorizontalAlign
{
    Left,
    Center,
    Right,
    Stretch,
}
