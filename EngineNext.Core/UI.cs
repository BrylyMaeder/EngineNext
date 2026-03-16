using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EngineNext.Core;

public abstract class UIElement
{
    internal UIWindow? Window { get; set; }

    public virtual void OnOpened() { }
    public virtual void OnClosed() { }

    public void Close() => Window?.Close();
}

public abstract class UIWindow : UIElement
{
    private static readonly ConcurrentDictionary<Type, UITypeCache> _cache = new();

    internal RectF Rect { get; set; }
    internal List<UIButtonHitRegion> ButtonRegions { get; } = new();

    public virtual bool Visible => true;

    public void Open() => Engine.UI.Open(GetType());
    public new void Close() => Engine.UI.Close(GetType());

    internal virtual void BuildLayout(RenderList list, SizeI viewport)
    {
        ButtonRegions.Clear();
        if (!Visible) return;

        Rect = ResolveWindowRect(viewport);

        var bg = GetAttribute<BackgroundAttribute>()?.Hex;
        var radius = GetAttribute<RoundedAttribute>()?.Radius ?? 14f;
        var pad = GetAttribute<PaddingAttribute>()?.Value ?? 16f;
        var background = string.IsNullOrWhiteSpace(bg) ? EngineColor.FromHex("#11161DEE") : EngineColor.FromHex(bg!);

        list.FillRect(Rect, background, radius);
        list.StrokeRect(Rect, new UIColor(255, 255, 255, 24), 1f, radius);

        float cursorY = Rect.Y + pad;
        float contentX = Rect.X + pad;
        float contentWidth = Math.Max(1f, Rect.Width - (pad * 2f));

        foreach (var item in CollectRenderableMembers())
        {
            switch (item.Kind)
            {
                case UIMemberKind.Title:
                {
                    float fontSize = item.FontSize <= 0 ? 28f : item.FontSize;
                    float h = 44f;
                    var area = new RectF(contentX, cursorY, contentWidth, h);
                    list.DrawText(item.TextValue, area, UIColor.White, fontSize, item.Align);
                    cursorY += h + 8f;
                    break;
                }
                case UIMemberKind.Text:
                {
                    float fontSize = item.FontSize <= 0 ? 18f : item.FontSize;
                    float h = MathF.Max(26f, fontSize + 10f);
                    var area = new RectF(contentX, cursorY, contentWidth, h);
                    list.DrawText(item.TextValue, area, new UIColor(228, 232, 238, 255), fontSize, item.Align);
                    cursorY += h + 6f;
                    break;
                }
                case UIMemberKind.Button:
                {
                    float buttonWidth = item.FullWidth ? contentWidth : MathF.Min(220f, contentWidth);
                    float buttonHeight = 42f;
                    float buttonX = item.Align switch
                    {
                        TextAlign.Center => Rect.CenterX - (buttonWidth * 0.5f),
                        TextAlign.Right => Rect.Right - pad - buttonWidth,
                        _ => contentX
                    };

                    var buttonRect = new RectF(buttonX, cursorY, buttonWidth, buttonHeight);
                    var hovered = buttonRect.Contains(Engine.Input.MousePosition);
                    list.FillRect(buttonRect, hovered ? UIColor.FromHex("#3F6BEA") : UIColor.FromHex("#2D57D7"), 12f);
                    list.StrokeRect(buttonRect, new UIColor(255, 255, 255, hovered ? (byte)70 : (byte)40), 1f, 12f);
                    list.DrawText(item.TextValue, buttonRect, UIColor.White, 18f, TextAlign.Center);
                    if (item.Action is not null)
                        ButtonRegions.Add(new UIButtonHitRegion(buttonRect, item.Action));
                    cursorY += buttonHeight + 10f;
                    break;
                }
                case UIMemberKind.Child:
                {
                    if (item.Child is UIBlock block)
                    {
                        var childHeight = MathF.Max(40f, block.EstimateHeight(contentWidth));
                        var childRect = new RectF(contentX, cursorY, contentWidth, childHeight);
                        block.Window = this;
                        block.BuildWithin(list, childRect, this);
                        cursorY += childRect.Height + 8f;
                    }
                    break;
                }
            }
        }
    }

    internal virtual float EstimateHeight(float width)
    {
        float total = 32f;
        foreach (var item in CollectRenderableMembers())
        {
            total += item.Kind switch
            {
                UIMemberKind.Title => 52f,
                UIMemberKind.Text => 32f,
                UIMemberKind.Button => 52f,
                UIMemberKind.Child => item.Child is UIBlock block ? block.EstimateHeight(width) : 40f,
                _ => 0f
            };
        }
        return total;
    }

    internal static T? GetAttribute<T>(MemberInfo member) where T : Attribute => member.GetCustomAttribute<T>();
    internal T? GetAttribute<T>() where T : Attribute => GetType().GetCustomAttribute<T>();

    private RectF ResolveWindowRect(SizeI viewport)
    {
        var size = GetAttribute<SizeAttribute>();
        float width = MathF.Max(120f, size?.Width ?? 420f);
        float height = MathF.Max(120f, size?.Height ?? MathF.Max(240f, EstimateHeight(width)));

        var anchor = GetAttribute<AnchorAttribute>()?.Value ?? UIAnchor.Center;
        if (GetAttribute<CenterAttribute>() is not null)
            anchor = UIAnchor.Center;

        var alignment = GetAttribute<WindowAlignmentAttribute>();
        var horizontal = alignment?.Horizontal ?? UIAlignment.Center;
        var vertical = alignment?.Vertical ?? UIAlignment.Center;

        float margin = 40f;
        float anchorX = anchor switch
        {
            UIAnchor.TopRight or UIAnchor.BottomRight => viewport.Width - margin,
            UIAnchor.TopLeft or UIAnchor.BottomLeft => margin,
            _ => viewport.Width * 0.5f,
        };
        float anchorY = anchor switch
        {
            UIAnchor.BottomLeft or UIAnchor.BottomRight => viewport.Height - margin,
            UIAnchor.TopLeft or UIAnchor.TopRight => margin,
            _ => viewport.Height * 0.5f,
        };

        float x = horizontal switch
        {
            UIAlignment.Start => anchorX,
            UIAlignment.End => anchorX - width,
            _ => anchorX - (width * 0.5f),
        };
        float y = vertical switch
        {
            UIAlignment.Start => anchorY,
            UIAlignment.End => anchorY - height,
            _ => anchorY - (height * 0.5f),
        };

        x = Math.Clamp(x, 8f, Math.Max(8f, viewport.Width - width - 8f));
        y = Math.Clamp(y, 8f, Math.Max(8f, viewport.Height - height - 8f));
        return new RectF(x, y, width, height);
    }

    private List<UIMember> CollectRenderableMembers()
    {
        var type = GetType();
        var meta = _cache.GetOrAdd(type, CreateCache);
        var result = new List<UIMember>(meta.Entries.Count);

        foreach (var entry in meta.Entries)
        {
            switch (entry.Kind)
            {
                case UIMemberKind.Title:
                case UIMemberKind.Text:
                    result.Add(new UIMember
                    {
                        Kind = entry.Kind,
                        TextValue = entry.GetText?.Invoke(this) ?? string.Empty,
                        Align = entry.Align,
                        FullWidth = entry.FullWidth,
                        FontSize = entry.FontSize,
                        Order = entry.Order
                    });
                    break;

                case UIMemberKind.Button:
                    result.Add(new UIMember
                    {
                        Kind = UIMemberKind.Button,
                        TextValue = entry.Label ?? string.Empty,
                        Align = entry.Align,
                        FullWidth = entry.FullWidth,
                        FontSize = entry.FontSize,
                        Order = entry.Order,
                        Action = entry.Method is null ? null : (() => entry.Method.Invoke(this, null))
                    });
                    break;

                case UIMemberKind.Child:
                    var child = entry.GetChild?.Invoke(this);
                    if (child is not null)
                    {
                        result.Add(new UIMember
                        {
                            Kind = UIMemberKind.Child,
                            Child = child,
                            Order = entry.Order
                        });
                    }
                    break;
            }
        }

        return result
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Kind == UIMemberKind.Title ? 0 : 1)
            .ToList();
    }

    private static UITypeCache CreateCache(Type type)
    {
        var entries = new List<UICacheEntry>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        foreach (var field in type.GetFields(flags))
        {
            if (field.IsStatic) continue;
            if (field.IsDefined(typeof(CompilerGeneratedAttribute), false)) continue;
            if (field.Name.Contains("k__BackingField", StringComparison.Ordinal)) continue;

            if (typeof(UIElement).IsAssignableFrom(field.FieldType))
            {
                entries.Add(new UICacheEntry
                {
                    Kind = UIMemberKind.Child,
                    Order = field.GetCustomAttribute<OrderAttribute>()?.Value ?? 0,
                    GetChild = target => SafeGetFieldValue(field, target) as UIElement
                });
                continue;
            }

            if (field.GetCustomAttribute<TitleAttribute>() is not null)
            {
                entries.Add(CreateTextEntry(UIMemberKind.Title, field));
            }
            else if (field.GetCustomAttribute<TextAttribute>() is not null)
            {
                entries.Add(CreateTextEntry(UIMemberKind.Text, field));
            }
        }

        foreach (var property in type.GetProperties(flags))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
            if (property.IsDefined(typeof(CompilerGeneratedAttribute), false)) continue;

            if (typeof(UIElement).IsAssignableFrom(property.PropertyType))
            {
                entries.Add(new UICacheEntry
                {
                    Kind = UIMemberKind.Child,
                    Order = property.GetCustomAttribute<OrderAttribute>()?.Value ?? 0,
                    GetChild = target => SafeGetPropertyValue(property, target) as UIElement
                });
                continue;
            }

            if (property.GetCustomAttribute<TitleAttribute>() is not null)
            {
                entries.Add(CreateTextEntry(UIMemberKind.Title, property));
            }
            else if (property.GetCustomAttribute<TextAttribute>() is not null)
            {
                entries.Add(CreateTextEntry(UIMemberKind.Text, property));
            }
        }

        foreach (var method in type.GetMethods(flags))
        {
            var button = method.GetCustomAttribute<ButtonAttribute>();
            if (button is null) continue;
            if (method.IsSpecialName) continue;
            if (method.GetParameters().Length != 0) continue;

            entries.Add(new UICacheEntry
            {
                Kind = UIMemberKind.Button,
                Method = method,
                Label = string.IsNullOrWhiteSpace(button.Label) ? method.Name : button.Label!,
                Align = method.GetCustomAttribute<AlignAttribute>()?.TextAlign ?? TextAlign.Left,
                FullWidth = method.GetCustomAttribute<FullWidthAttribute>() is not null,
                FontSize = method.GetCustomAttribute<FontSizeAttribute>()?.Value ?? 18f,
                Order = method.GetCustomAttribute<OrderAttribute>()?.Value ?? 0
            });
        }

        return new UITypeCache(entries);
    }

    private static UICacheEntry CreateTextEntry(UIMemberKind kind, FieldInfo field)
        => new()
        {
            Kind = kind,
            Align = field.GetCustomAttribute<AlignAttribute>()?.TextAlign ?? (kind == UIMemberKind.Title ? TextAlign.Center : TextAlign.Left),
            FullWidth = field.GetCustomAttribute<FullWidthAttribute>() is not null,
            FontSize = field.GetCustomAttribute<FontSizeAttribute>()?.Value ?? 0f,
            Order = field.GetCustomAttribute<OrderAttribute>()?.Value ?? 0,
            GetText = target => ConvertMemberValueToText(SafeGetFieldValue(field, target))
        };

    private static UICacheEntry CreateTextEntry(UIMemberKind kind, PropertyInfo property)
        => new()
        {
            Kind = kind,
            Align = property.GetCustomAttribute<AlignAttribute>()?.TextAlign ?? (kind == UIMemberKind.Title ? TextAlign.Center : TextAlign.Left),
            FullWidth = property.GetCustomAttribute<FullWidthAttribute>() is not null,
            FontSize = property.GetCustomAttribute<FontSizeAttribute>()?.Value ?? 0f,
            Order = property.GetCustomAttribute<OrderAttribute>()?.Value ?? 0,
            GetText = target => ConvertMemberValueToText(SafeGetPropertyValue(property, target))
        };

    private static object? SafeGetFieldValue(FieldInfo field, object target)
    {
        try { return field.GetValue(target); }
        catch { return null; }
    }

    private static object? SafeGetPropertyValue(PropertyInfo property, object target)
    {
        try { return property.GetValue(target); }
        catch { return null; }
    }

    private static string ConvertMemberValueToText(object? value)
    {
        if (value is null) return string.Empty;
        if (value is UIElement) return string.Empty;
        if (value is string s) return s;
        if (value is char c) return c.ToString();
        if (value is bool b) return b ? "True" : "False";
        if (value is Enum e) return e.ToString();
        if (value is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
        var type = value.GetType();
        if (type.IsPrimitive) return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return string.Empty;
    }

    private sealed record UITypeCache(List<UICacheEntry> Entries);

    private sealed class UICacheEntry
    {
        public UIMemberKind Kind { get; init; }
        public string? Label { get; init; }
        public TextAlign Align { get; init; }
        public bool FullWidth { get; init; }
        public float FontSize { get; init; }
        public int Order { get; init; }
        public MethodInfo? Method { get; init; }
        public Func<object, string>? GetText { get; init; }
        public Func<object, UIElement?>? GetChild { get; init; }
    }
}

public abstract class UIBlock : UIElement
{
    internal virtual void BuildWithin(RenderList list, RectF rect, UIWindow window)
    {
        Window = window;
        const float pad = 8f;
        list.FillRect(rect, EngineColor.FromHex("#0F1318AA"), 10f);

        float y = rect.Y + pad;
        foreach (var row in CollectRows())
        {
            var textRect = new RectF(rect.X + pad, y, Math.Max(1f, rect.Width - (pad * 2f)), 22f);
            list.DrawText(row, textRect, new EngineColor(220, 224, 230, 255), 16f, TextAlign.Left);
            y += 24f;
        }
    }

    internal virtual float EstimateHeight(float width) => (CollectRows().Count * 24f) + 16f;

    protected abstract IReadOnlyList<string> CollectRows();
}

internal enum UIMemberKind
{
    Title,
    Text,
    Button,
    Child,
}

internal sealed class UIMember
{
    public UIMemberKind Kind { get; set; }
    public string TextValue { get; set; } = string.Empty;
    public TextAlign Align { get; set; } = TextAlign.Left;
    public bool FullWidth { get; set; }
    public float FontSize { get; set; }
    public int Order { get; set; }
    public Action? Action { get; set; }
    public UIElement? Child { get; set; }
}

internal readonly record struct UIButtonHitRegion(RectF Rect, Action Action);

public sealed class UIManager
{
    private static readonly Lazy<Type[]> _windowTypes = new(DiscoverWindowTypes, true);

    private readonly List<UIWindow> _open = new();
    private readonly Dictionary<Type, UIWindow> _singletons = new();

    public IReadOnlyList<UIWindow> OpenWindows => _open;

    public void ProcessOpenBindings()
    {
        foreach (var windowType in _windowTypes.Value)
        {
            var openWith = windowType.GetCustomAttribute<OpenWithAttribute>();
            if (openWith is not null && Engine.Actions.Pressed(openWith.Action))
            {
                bool toggle = windowType.GetCustomAttribute<ToggleOpenAttribute>() is not null;
                if (toggle && IsOpen(windowType)) Close(windowType);
                else Open(windowType);
            }

            var closeWith = windowType.GetCustomAttribute<CloseWithAttribute>();
            if (closeWith is not null && Engine.Actions.Pressed(closeWith.Action))
                Close(windowType);
        }
    }

    public void Update(float dt)
    {
        if (_open.Count == 0)
        {
            if (Engine.InputMode != InputMode.GameOnly)
                Engine.InputMode = InputMode.GameAndUI;
            return;
        }

        if (Engine.Input.Pressed(InputKey.MouseLeft))
        {
            var mouse = Engine.Input.MousePosition;
            for (int i = _open.Count - 1; i >= 0; i--)
            {
                var window = _open[i];
                if (!window.Visible) continue;

                for (int j = window.ButtonRegions.Count - 1; j >= 0; j--)
                {
                    var region = window.ButtonRegions[j];
                    if (!region.Rect.Contains(mouse)) continue;
                    Engine.Sound.Play("ui/click");
                    region.Action();
                    RefreshInputMode();
                    return;
                }
            }
        }

        RefreshInputMode();
    }

    public void Render(RenderList list, SizeI viewport)
    {
        foreach (var window in _open)
            window.BuildLayout(list, viewport);
    }

    public T Open<T>() where T : UIWindow, new() => (T)Open(typeof(T));

    public UIWindow Open(Type type)
    {
        if (!typeof(UIWindow).IsAssignableFrom(type))
            throw new InvalidOperationException($"{type.Name} is not a UIWindow.");

        if (type.GetCustomAttribute<SingletonAttribute>() is not null && _singletons.TryGetValue(type, out var existing))
        {
            if (!_open.Contains(existing))
                _open.Add(existing);
            RefreshInputMode();
            return existing;
        }

        var window = (UIWindow)Activator.CreateInstance(type)!;
        window.Window = window;

        if (type.GetCustomAttribute<SingletonAttribute>() is not null)
            _singletons[type] = window;

        _open.Add(window);
        window.OnOpened();
        RefreshInputMode();
        return window;
    }

    public void Close<T>() where T : UIWindow => Close(typeof(T));

    public void Close(Type type)
    {
        for (int i = _open.Count - 1; i >= 0; i--)
        {
            var window = _open[i];
            if (window.GetType() != type) continue;
            _open.RemoveAt(i);
            window.OnClosed();
        }
        RefreshInputMode();
    }

    public void CloseAll<T>() where T : UIWindow
    {
        var type = typeof(T);
        for (int i = _open.Count - 1; i >= 0; i--)
        {
            var window = _open[i];
            if (!type.IsAssignableFrom(window.GetType())) continue;
            _open.RemoveAt(i);
            window.OnClosed();
        }
        RefreshInputMode();
    }

    public void Toggle<T>() where T : UIWindow, new()
    {
        if (IsOpen(typeof(T))) Close(typeof(T));
        else Open<T>();
    }

    public bool IsOpen<T>() where T : UIWindow => IsOpen(typeof(T));
    public bool IsOpen(Type type) => _open.Any(x => x.GetType() == type);

    private void RefreshInputMode()
    {
        var top = _open.LastOrDefault();
        if (top is null)
        {
            Engine.InputMode = InputMode.GameAndUI;
            return;
        }

        var preferred = top.GetType().GetCustomAttribute<UIWindowInputModeAttribute>();
        Engine.InputMode = preferred?.Mode ?? InputMode.UIBlocksGame;
    }

    private static Type[] DiscoverWindowTypes()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(static assembly =>
            {
                try { return assembly.GetTypes(); }
                catch { return Array.Empty<Type>(); }
            })
            .Where(t => !t.IsAbstract && typeof(UIWindow).IsAssignableFrom(t))
            .ToArray();
    }
}
