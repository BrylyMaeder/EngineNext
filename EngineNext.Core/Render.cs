namespace EngineNext.Core;

public enum DrawKind
{
    FillRect,
    StrokeRect,
    FillCircle,
    StrokeCircle,
    DrawText,
    DrawImage,
    DrawLine,
}

public readonly record struct DrawCommand(
    DrawKind Kind,
    RectF Rect,
    EngineColor Color,
    string Content,
    string ImagePath,
    float Thickness,
    float Radius,
    float FontSize,
    TextAlign Align,
    Vec2 PointA,
    Vec2 PointB)
{
    public static DrawCommand FillRect(RectF rect, EngineColor color, float radius = 0f) =>
        new(DrawKind.FillRect, rect, color, string.Empty, string.Empty, 0f, radius, 0f, TextAlign.Left, Vec2.Zero, Vec2.Zero);

    public static DrawCommand StrokeRect(RectF rect, EngineColor color, float thickness = 1f, float radius = 0f) =>
        new(DrawKind.StrokeRect, rect, color, string.Empty, string.Empty, thickness, radius, 0f, TextAlign.Left, Vec2.Zero, Vec2.Zero);

    public static DrawCommand FillCircle(RectF rect, EngineColor color) =>
        new(DrawKind.FillCircle, rect, color, string.Empty, string.Empty, 0f, 0f, 0f, TextAlign.Left, Vec2.Zero, Vec2.Zero);

    public static DrawCommand StrokeCircle(RectF rect, EngineColor color, float thickness = 1f) =>
        new(DrawKind.StrokeCircle, rect, color, string.Empty, string.Empty, thickness, 0f, 0f, TextAlign.Left, Vec2.Zero, Vec2.Zero);

    public static DrawCommand TextCommand(string content, RectF rect, EngineColor color, float size = 18f, TextAlign align = TextAlign.Left) =>
        new(DrawKind.DrawText, rect, color, content ?? string.Empty, string.Empty, 0f, 0f, size, align, Vec2.Zero, Vec2.Zero);

    public static DrawCommand ImageCommand(string imagePath, RectF rect, EngineColor tint, float radius = 0f) =>
        new(DrawKind.DrawImage, rect, tint, string.Empty, imagePath ?? string.Empty, 0f, radius, 0f, TextAlign.Left, Vec2.Zero, Vec2.Zero);

    public static DrawCommand LineCommand(Vec2 a, Vec2 b, EngineColor color, float thickness = 1f) =>
        new(DrawKind.DrawLine, default, color, string.Empty, string.Empty, thickness, 0f, 0f, TextAlign.Left, a, b);
}

public sealed class RenderList
{
    public List<DrawCommand> Commands { get; } = new();

    public void Clear() => Commands.Clear();
    public void FillRect(RectF rect, EngineColor color, float radius = 0f) => Commands.Add(DrawCommand.FillRect(rect, color, radius));
    public void StrokeRect(RectF rect, EngineColor color, float thickness = 1f, float radius = 0f) => Commands.Add(DrawCommand.StrokeRect(rect, color, thickness, radius));
    public void FillCircle(RectF rect, EngineColor color) => Commands.Add(DrawCommand.FillCircle(rect, color));
    public void StrokeCircle(RectF rect, EngineColor color, float thickness = 1f) => Commands.Add(DrawCommand.StrokeCircle(rect, color, thickness));
    public void DrawText(string content, RectF rect, EngineColor color, float size = 18f, TextAlign align = TextAlign.Left) => Commands.Add(DrawCommand.TextCommand(content, rect, color, size, align));
    public void DrawImage(string imagePath, RectF rect, EngineColor tint, float radius = 0f) => Commands.Add(DrawCommand.ImageCommand(imagePath, rect, tint, radius));
    public void DrawLine(Vec2 a, Vec2 b, EngineColor color, float thickness = 1f) => Commands.Add(DrawCommand.LineCommand(a, b, color, thickness));
}

public sealed class RenderSettings
{
    public EngineColor ClearColor { get; set; } = EngineColor.FromHex("#171A20");
    public bool ShowFpsCounter { get; set; } = true;
    public EngineColor FpsTextColor { get; set; } = EngineColor.White;
    public EngineColor FpsShadowColor { get; set; } = new EngineColor(0, 0, 0, 180);
}
