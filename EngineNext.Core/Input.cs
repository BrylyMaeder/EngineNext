namespace EngineNext.Core;

public enum InputMode
{
    GameOnly,
    UIOnly,
    GameAndUI,
    UIBlocksGame,
}

public enum InputKey
{
    None,
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    Space,
    Enter,
    Escape,
    Tab,
    Backspace,
    LeftShift,
    RightShift,
    LeftControl,
    RightControl,
    LeftAlt,
    RightAlt,
    Up,
    Down,
    Left,
    Right,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    MouseLeft,
    MouseRight,
    MouseMiddle,
}

public sealed class InputSnapshot
{
    private readonly HashSet<InputKey> _down = new();
    private readonly HashSet<InputKey> _pressed = new();
    private readonly HashSet<InputKey> _released = new();

    public Vec2 MousePosition { get; private set; }
    public float MouseWheelDelta { get; private set; }

    public bool Down(InputKey key) => _down.Contains(key);
    public bool Pressed(InputKey key) => _pressed.Contains(key);
    public bool Released(InputKey key) => _released.Contains(key);

    public void BeginFrame()
    {
        _pressed.Clear();
        _released.Clear();
        MouseWheelDelta = 0f;
    }

    public void SetKey(InputKey key, bool isDown)
    {
        if (isDown)
        {
            if (_down.Add(key)) _pressed.Add(key);
        }
        else
        {
            if (_down.Remove(key)) _released.Add(key);
        }
    }

    public void SetMousePosition(float x, float y) => MousePosition = new Vec2(x, y);
    public void AddMouseWheel(float delta) => MouseWheelDelta += delta;

    public void Reset()
    {
        _down.Clear();
        _pressed.Clear();
        _released.Clear();
        MouseWheelDelta = 0f;
    }
}

public sealed class ActionMap
{
    private readonly Dictionary<string, List<InputKey>> _map = new(StringComparer.OrdinalIgnoreCase);

    public void Bind(string action, params InputKey[] keys)
    {
        if (!_map.TryGetValue(action, out var list))
        {
            list = new List<InputKey>();
            _map[action] = list;
        }
        list.Clear();
        list.AddRange(keys);
    }

    public bool Pressed(string action) => _map.TryGetValue(action, out var keys) && keys.Any(Engine.Input.Pressed);
    public bool Down(string action) => _map.TryGetValue(action, out var keys) && keys.Any(Engine.Input.Down);
}
