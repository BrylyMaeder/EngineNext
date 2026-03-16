namespace EngineNext.Core;

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class PopupAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class SingletonAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class OpenWithAttribute : Attribute
{
    public OpenWithAttribute(string action) => Action = action;
    public string Action { get; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class CloseWithAttribute : Attribute
{
    public CloseWithAttribute(string action) => Action = action;
    public string Action { get; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class ToggleOpenAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class CenterAttribute : Attribute { }

public enum UIAnchor
{
    Center,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public enum UIAlignment
{
    Start,
    Center,
    End,
}

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class AnchorAttribute : Attribute
{
    public AnchorAttribute(UIAnchor value) => Value = value;
    public UIAnchor Value { get; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class WindowAlignmentAttribute : Attribute
{
    public WindowAlignmentAttribute(UIAlignment horizontal, UIAlignment vertical)
    {
        Horizontal = horizontal;
        Vertical = vertical;
    }

    public UIAlignment Horizontal { get; }
    public UIAlignment Vertical { get; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class SizeAttribute : Attribute
{
    public SizeAttribute(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public float Width { get; }
    public float Height { get; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class BackgroundAttribute : Attribute
{
    public BackgroundAttribute(string hex) => Hex = hex;
    public string Hex { get; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class RoundedAttribute : Attribute
{
    public RoundedAttribute(float radius) => Radius = radius;
    public float Radius { get; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class PaddingAttribute : Attribute
{
    public PaddingAttribute(float value) => Value = value;
    public float Value { get; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class UIWindowInputModeAttribute : Attribute
{
    public UIWindowInputModeAttribute(InputMode mode) => Mode = mode;
    public InputMode Mode { get; }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class TitleAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class TextAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public sealed class ButtonAttribute : Attribute
{
    public ButtonAttribute(string? label = null) => Label = label;
    public string? Label { get; }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public sealed class FullWidthAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public sealed class AlignAttribute : Attribute
{
    public AlignAttribute(TextAlign textAlign) => TextAlign = textAlign;
    public TextAlign TextAlign { get; }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public sealed class FontSizeAttribute : Attribute
{
    public FontSizeAttribute(float value) => Value = value;
    public float Value { get; }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public sealed class OrderAttribute : Attribute
{
    public OrderAttribute(int value) => Value = value;
    public int Value { get; }
}
