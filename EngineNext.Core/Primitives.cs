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
    public static Vec2 operator /(Vec2 a, float b) => b == 0f ? Zero : new(a.X / b, a.Y / b);
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

public struct BlockScalar : IEquatable<BlockScalar>
{
    private const double UnitScale = 1.0 / 1024.0;
    private byte _flags;
    private byte _b0;
    private byte _b1;
    private byte _b2;
    private byte _b3;
    private byte _b4;
    private byte _b5;
    private byte _b6;
    private byte _b7;

    public bool IsNegative
    {
        get => (_flags & 1) != 0;
        set
        {
            if (value) _flags |= 1;
            else _flags &= 0xFE;
        }
    }

    public ulong ToRawUnsigned()
    {
        return (ulong)_b0
            | ((ulong)_b1 << 8)
            | ((ulong)_b2 << 16)
            | ((ulong)_b3 << 24)
            | ((ulong)_b4 << 32)
            | ((ulong)_b5 << 40)
            | ((ulong)_b6 << 48)
            | ((ulong)_b7 << 56);
    }

    public long ToRawSigned()
    {
        ulong u = ToRawUnsigned();
        if (u > long.MaxValue) u = long.MaxValue;
        long value = (long)u;
        return IsNegative ? -value : value;
    }

    public double ToDouble() => ToRawSigned() * UnitScale;
    public float ToFloat() => (float)ToDouble();

    public static BlockScalar FromRawSigned(long value)
    {
        BlockScalar s = default;
        ulong mag;
        if (value < 0)
        {
            s.IsNegative = true;
            mag = (ulong)(value == long.MinValue ? long.MaxValue : -value);
        }
        else
        {
            mag = (ulong)value;
        }

        s._b0 = (byte)(mag & 0xFF);
        s._b1 = (byte)((mag >> 8) & 0xFF);
        s._b2 = (byte)((mag >> 16) & 0xFF);
        s._b3 = (byte)((mag >> 24) & 0xFF);
        s._b4 = (byte)((mag >> 32) & 0xFF);
        s._b5 = (byte)((mag >> 40) & 0xFF);
        s._b6 = (byte)((mag >> 48) & 0xFF);
        s._b7 = (byte)((mag >> 56) & 0xFF);
        return s;
    }

    public static BlockScalar FromDouble(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) value = 0.0;
        double scaled = value / UnitScale;
        if (scaled > long.MaxValue) scaled = long.MaxValue;
        if (scaled < long.MinValue) scaled = long.MinValue;
        return FromRawSigned((long)Math.Round(scaled));
    }

    public bool Equals(BlockScalar other) => ToRawSigned() == other.ToRawSigned();
    public override bool Equals(object? obj) => obj is BlockScalar other && Equals(other);
    public override int GetHashCode() => ToRawSigned().GetHashCode();
    public override string ToString() => ToDouble().ToString("0.###");
    public static BlockScalar operator +(BlockScalar a, BlockScalar b) => FromRawSigned(a.ToRawSigned() + b.ToRawSigned());
    public static BlockScalar operator -(BlockScalar a, BlockScalar b) => FromRawSigned(a.ToRawSigned() - b.ToRawSigned());
    public static bool operator ==(BlockScalar a, BlockScalar b) => a.Equals(b);
    public static bool operator !=(BlockScalar a, BlockScalar b) => !a.Equals(b);
}

public struct BlockVector2 : IEquatable<BlockVector2>
{
    public BlockScalar X;
    public BlockScalar Y;

    public static readonly BlockVector2 Zero = new(BlockScalar.FromRawSigned(0), BlockScalar.FromRawSigned(0));
    public static readonly BlockVector2 One = new(BlockScalar.FromDouble(1), BlockScalar.FromDouble(1));

    public BlockVector2(BlockScalar x, BlockScalar y)
    {
        X = x;
        Y = y;
    }

    public static BlockVector2 FromDouble(double x, double y) => new(BlockScalar.FromDouble(x), BlockScalar.FromDouble(y));
    public static BlockVector2 FromVec2(Vec2 value) => FromDouble(value.X, value.Y);
    public Vec2 ToVec2() => new(X.ToFloat(), Y.ToFloat());

    public static BlockVector2 Lerp(BlockVector2 a, BlockVector2 b, double t)
    {
        return FromDouble(
            a.X.ToDouble() + ((b.X.ToDouble() - a.X.ToDouble()) * t),
            a.Y.ToDouble() + ((b.Y.ToDouble() - a.Y.ToDouble()) * t));
    }

    public bool Equals(BlockVector2 other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is BlockVector2 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";
    public static BlockVector2 operator +(BlockVector2 a, BlockVector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static BlockVector2 operator -(BlockVector2 a, BlockVector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static bool operator ==(BlockVector2 a, BlockVector2 b) => a.Equals(b);
    public static bool operator !=(BlockVector2 a, BlockVector2 b) => !a.Equals(b);
}
