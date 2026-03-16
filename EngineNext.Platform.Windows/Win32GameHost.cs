using UIColor = EngineNext.Core.EngineColor;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using EngineNext.Core;
using EngineNext.Platform;

namespace EngineNext.Platform.Windows;

public sealed class Win32GameHost : IGameHost
{
    private readonly RenderList _renderList = new();
    private readonly Dictionary<int, SolidBrush> _brushes = new();
    private readonly Dictionary<string, Pen> _pens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Font> _fonts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Image?> _images = new(StringComparer.OrdinalIgnoreCase);
    private Bitmap? _backBuffer;
    private Graphics? _backGraphics;
    private IntPtr _hwnd;
    private bool _running;
    private SizeI _viewport;
    private WndProc? _wndProc;
    private readonly string _className = "EngineNextWindowClass";
    private int _fps;
    private int _fpsFrames;
    private TimeSpan _fpsWindow;
    private bool _showFps = true;

    public void Run(HostOptions options, Action startup)
    {
        try
        {
            _viewport = new SizeI(options.Width, options.Height);
            _wndProc = WindowProc;

            var hInstance = GetModuleHandle(null);
            var wc = new WNDCLASS
            {
                lpfnWndProc = _wndProc,
                hInstance = hInstance,
                lpszClassName = _className,
                hCursor = LoadCursor(IntPtr.Zero, (int)StandardCursor.Arrow),
                hbrBackground = IntPtr.Zero
            };

            RegisterClass(ref wc);

            _hwnd = CreateWindowEx(
                0,
                _className,
                options.Title,
                WindowStyles.WS_OVERLAPPEDWINDOW | WindowStyles.WS_VISIBLE,
                100,
                100,
                options.Width,
                options.Height,
                IntPtr.Zero,
                IntPtr.Zero,
                hInstance,
                IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
                throw new InvalidOperationException($"CreateWindowEx failed. Win32 error: {Marshal.GetLastWin32Error()}");

            ShowWindow(_hwnd, 5);
            UpdateWindow(_hwnd);

            startup();

            var stopwatch = Stopwatch.StartNew();
            var last = stopwatch.Elapsed;
            _running = true;

            while (_running && !Engine.ExitRequested)
            {
                Engine.Input.BeginFrame();

                while (PeekMessage(out var msg, IntPtr.Zero, 0, 0, 1))
                {
                    if (msg.message == WM_QUIT)
                    {
                        _running = false;
                        break;
                    }
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }

                var now = stopwatch.Elapsed;
                var dt = (float)Math.Clamp((now - last).TotalSeconds, 1.0 / 500.0, 1.0 / 20.0);
                last = now;

                _fpsFrames++;
                _fpsWindow += TimeSpan.FromSeconds(dt);
                if (_fpsWindow.TotalSeconds >= 1.0)
                {
                    _fps = (int)MathF.Round(_fpsFrames / (float)_fpsWindow.TotalSeconds);
                    _fpsFrames = 0;
                    _fpsWindow = TimeSpan.Zero;
                }

                Engine.Tick(dt);
                InvalidateRect(_hwnd, IntPtr.Zero, false);
                Sleep(1);
            }

            DisposeRenderResources();
        }
        catch (Exception ex)
        {
            ReportCrash(ex);
            throw;
        }
    }

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case WM_DESTROY:
                    PostQuitMessage(0);
                    return IntPtr.Zero;
                case WM_SIZE:
                    _viewport = new SizeI(Math.Max(1, LowWord(lParam)), Math.Max(1, HighWord(lParam)));
                    return IntPtr.Zero;
                case WM_MOUSEMOVE:
                    Engine.Input.SetMousePosition(LowWord(lParam), HighWord(lParam));
                    return IntPtr.Zero;
                case WM_MOUSEWHEEL:
                    Engine.Input.AddMouseWheel((short)HighWord(wParam) / 120f);
                    return IntPtr.Zero;
                case WM_LBUTTONDOWN:
                    Engine.Input.SetKey(InputKey.MouseLeft, true);
                    SetCapture(hWnd);
                    return IntPtr.Zero;
                case WM_LBUTTONUP:
                    Engine.Input.SetKey(InputKey.MouseLeft, false);
                    ReleaseCapture();
                    return IntPtr.Zero;
                case WM_RBUTTONDOWN:
                    Engine.Input.SetKey(InputKey.MouseRight, true);
                    return IntPtr.Zero;
                case WM_RBUTTONUP:
                    Engine.Input.SetKey(InputKey.MouseRight, false);
                    return IntPtr.Zero;
                case WM_MBUTTONDOWN:
                    Engine.Input.SetKey(InputKey.MouseMiddle, true);
                    return IntPtr.Zero;
                case WM_MBUTTONUP:
                    Engine.Input.SetKey(InputKey.MouseMiddle, false);
                    return IntPtr.Zero;
                case WM_KEYDOWN:
                case WM_SYSKEYDOWN:
                    var down = MapKey((int)wParam);
                    if (down != InputKey.None) Engine.Input.SetKey(down, true);
                    return IntPtr.Zero;
                case WM_KEYUP:
                case WM_SYSKEYUP:
                    if ((int)wParam == 0x72)
                        _showFps = !_showFps;
                    var up = MapKey((int)wParam);
                    if (up != InputKey.None) Engine.Input.SetKey(up, false);
                    return IntPtr.Zero;
                case WM_KILLFOCUS:
                    Engine.Input.Reset();
                    return IntPtr.Zero;
                case WM_ERASEBKGND:
                    return (IntPtr)1;
                case WM_PAINT:
                    Paint(hWnd);
                    return IntPtr.Zero;
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }
        catch (Exception ex)
        {
            ReportCrash(ex);
            PostQuitMessage(-1);
            return IntPtr.Zero;
        }
    }

    private void Paint(IntPtr hWnd)
    {
        var paintStruct = new PAINTSTRUCT();
        var hdc = BeginPaint(hWnd, ref paintStruct);
        try
        {
            EnsureBackBuffer();
            if (_backGraphics is null || _backBuffer is null)
                return;

            ConfigureGraphics(_backGraphics);
            Engine.Render(_renderList, _viewport);

            foreach (var cmd in _renderList.Commands)
                DrawCommand(_backGraphics, cmd);

            if (_showFps && Engine.RenderSettings.ShowFpsCounter)
                DrawFpsOverlay(_backGraphics);

            using var graphics = Graphics.FromHdc(hdc);
            ConfigureGraphics(graphics);
            graphics.DrawImageUnscaled(_backBuffer, 0, 0);
        }
        finally
        {
            EndPaint(hWnd, ref paintStruct);
        }
    }

    private void EnsureBackBuffer()
    {
        var width = Math.Max(1, _viewport.Width);
        var height = Math.Max(1, _viewport.Height);
        if (_backBuffer is not null && _backBuffer.Width == width && _backBuffer.Height == height && _backGraphics is not null)
            return;

        _backGraphics?.Dispose();
        _backBuffer?.Dispose();
        _backBuffer = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        _backGraphics = Graphics.FromImage(_backBuffer);
        ConfigureGraphics(_backGraphics);
    }

    private static void ConfigureGraphics(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
    }

    private void DrawCommand(Graphics graphics, DrawCommand cmd)
    {
        switch (cmd.Kind)
        {
            case DrawKind.FillRect:
                var fillBrush = GetBrush(cmd.Color);
                if (cmd.Radius > 0f) FillRounded(graphics, fillBrush, cmd.Rect, cmd.Radius);
                else graphics.FillRectangle(fillBrush, cmd.Rect.X, cmd.Rect.Y, cmd.Rect.Width, cmd.Rect.Height);
                break;

            case DrawKind.StrokeRect:
                var strokePen = GetPen(cmd.Color, cmd.Thickness);
                if (cmd.Radius > 0f) DrawRounded(graphics, strokePen, cmd.Rect, cmd.Radius);
                else graphics.DrawRectangle(strokePen, cmd.Rect.X, cmd.Rect.Y, cmd.Rect.Width, cmd.Rect.Height);
                break;

            case DrawKind.FillCircle:
                graphics.FillEllipse(GetBrush(cmd.Color), cmd.Rect.X, cmd.Rect.Y, cmd.Rect.Width, cmd.Rect.Height);
                break;

            case DrawKind.StrokeCircle:
                graphics.DrawEllipse(GetPen(cmd.Color, cmd.Thickness), cmd.Rect.X, cmd.Rect.Y, cmd.Rect.Width, cmd.Rect.Height);
                break;

            case DrawKind.DrawText:
                using (var format = new StringFormat())
                {
                    format.LineAlignment = StringAlignment.Center;
                    format.Alignment = cmd.Align switch
                    {
                        TextAlign.Center => StringAlignment.Center,
                        TextAlign.Right => StringAlignment.Far,
                        _ => StringAlignment.Near,
                    };

                    graphics.DrawString(
                        cmd.Content,
                        GetFont(cmd.FontSize),
                        GetBrush(cmd.Color),
                        new RectangleF(cmd.Rect.X, cmd.Rect.Y, cmd.Rect.Width, cmd.Rect.Height),
                        format);
                }
                break;

            case DrawKind.DrawImage:
                var image = GetImage(cmd.ImagePath);
                if (image is not null)
                {
                    graphics.DrawImage(image, cmd.Rect.X, cmd.Rect.Y, cmd.Rect.Width, cmd.Rect.Height);
                }
                else
                {
                    var fallbackBrush = GetBrush(cmd.Color);
                    graphics.FillRectangle(fallbackBrush, cmd.Rect.X, cmd.Rect.Y, cmd.Rect.Width, cmd.Rect.Height);
                }
                break;

            case DrawKind.DrawLine:
                graphics.DrawLine(GetPen(cmd.Color, cmd.Thickness), cmd.PointA.X, cmd.PointA.Y, cmd.PointB.X, cmd.PointB.Y);
                break;
        }
    }

    private void DrawFpsOverlay(Graphics graphics)
    {
        var rect = new RectF(_viewport.Width - 108f, 12f, 96f, 28f);
        graphics.FillRectangle(GetBrush(new UIColor(0, 0, 0, 150)), rect.X, rect.Y, rect.Width, rect.Height);
        graphics.DrawRectangle(GetPen(new UIColor(255, 255, 255, 28), 1f), rect.X, rect.Y, rect.Width, rect.Height);

        var shadowRect = new RectangleF(rect.X + 1f, rect.Y + 1f, rect.Width - 8f, rect.Height);
        var textRect = new RectangleF(rect.X, rect.Y, rect.Width - 8f, rect.Height);
        using var format = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        graphics.DrawString($"FPS {_fps}", GetFont(16f), GetBrush(Engine.RenderSettings.FpsShadowColor), shadowRect, format);
        graphics.DrawString($"FPS {_fps}", GetFont(16f), GetBrush(Engine.RenderSettings.FpsTextColor), textRect, format);
    }

    private SolidBrush GetBrush(UIColor color)
    {
        var key = (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
        if (_brushes.TryGetValue(key, out var brush))
            return brush;

        brush = new SolidBrush(ToDrawingColor(color));
        _brushes[key] = brush;
        return brush;
    }

    private Pen GetPen(UIColor color, float thickness)
    {
        var key = $"{color.A}:{color.R}:{color.G}:{color.B}:{thickness:0.###}";
        if (_pens.TryGetValue(key, out var pen))
            return pen;

        pen = new Pen(ToDrawingColor(color), thickness);
        _pens[key] = pen;
        return pen;
    }

    private Font GetFont(float size)
    {
        var key = $"Segoe UI:{size:0.###}";
        if (_fonts.TryGetValue(key, out var font))
            return font;

        font = new Font("Segoe UI", Math.Max(1f, size), FontStyle.Regular, GraphicsUnit.Pixel);
        _fonts[key] = font;
        return font;
    }

    private Image? GetImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (_images.TryGetValue(path, out var cached))
            return cached;

        try
        {
            if (!File.Exists(path))
            {
                _images[path] = null;
                return null;
            }

            cached = Image.FromFile(path);
            _images[path] = cached;
            return cached;
        }
        catch
        {
            _images[path] = null;
            return null;
        }
    }

    private static void FillRounded(Graphics graphics, Brush brush, RectF rect, float radius)
    {
        using var path = RoundedPath(rect, radius);
        graphics.FillPath(brush, path);
    }

    private static void DrawRounded(Graphics graphics, Pen pen, RectF rect, float radius)
    {
        using var path = RoundedPath(rect, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedPath(RectF rect, float radius)
    {
        var safeRadius = MathF.Max(0f, MathF.Min(radius, MathF.Min(rect.Width, rect.Height) * 0.5f));
        var d = safeRadius * 2f;
        var path = new GraphicsPath();
        if (safeRadius <= 0.01f)
        {
            path.AddRectangle(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height));
            return path;
        }

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void DisposeRenderResources()
    {
        _backGraphics?.Dispose();
        _backGraphics = null;
        _backBuffer?.Dispose();
        _backBuffer = null;

        foreach (var brush in _brushes.Values)
            brush.Dispose();
        _brushes.Clear();

        foreach (var pen in _pens.Values)
            pen.Dispose();
        _pens.Clear();

        foreach (var font in _fonts.Values)
            font.Dispose();
        _fonts.Clear();

        foreach (var image in _images.Values)
            image?.Dispose();
        _images.Clear();
    }

    private static void ReportCrash(Exception ex)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "EngineNext.crash.log");
            File.WriteAllText(logPath, $"[{DateTime.Now:O}]\n{ex}\n");
            MessageBox(IntPtr.Zero, ex.ToString(), "EngineNext Crash", 0x00000010);
        }
        catch
        {
        }
    }

    private static int LowWord(IntPtr value) => unchecked((short)((long)value & 0xFFFF));
    private static int HighWord(IntPtr value) => unchecked((short)(((long)value >> 16) & 0xFFFF));

    private static System.Drawing.Color ToDrawingColor(UIColor color) => System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);

    private static InputKey MapKey(int vk) => vk switch
    {
        0x41 => InputKey.A, 0x42 => InputKey.B, 0x43 => InputKey.C, 0x44 => InputKey.D, 0x45 => InputKey.E,
        0x46 => InputKey.F, 0x47 => InputKey.G, 0x48 => InputKey.H, 0x49 => InputKey.I, 0x4A => InputKey.J,
        0x4B => InputKey.K, 0x4C => InputKey.L, 0x4D => InputKey.M, 0x4E => InputKey.N, 0x4F => InputKey.O,
        0x50 => InputKey.P, 0x51 => InputKey.Q, 0x52 => InputKey.R, 0x53 => InputKey.S, 0x54 => InputKey.T,
        0x55 => InputKey.U, 0x56 => InputKey.V, 0x57 => InputKey.W, 0x58 => InputKey.X, 0x59 => InputKey.Y, 0x5A => InputKey.Z,
        0x30 => InputKey.D0, 0x31 => InputKey.D1, 0x32 => InputKey.D2, 0x33 => InputKey.D3, 0x34 => InputKey.D4,
        0x35 => InputKey.D5, 0x36 => InputKey.D6, 0x37 => InputKey.D7, 0x38 => InputKey.D8, 0x39 => InputKey.D9,
        0x20 => InputKey.Space,
        0x0D => InputKey.Enter,
        0x1B => InputKey.Escape,
        0x08 => InputKey.Backspace,
        0x09 => InputKey.Tab,
        0xA0 => InputKey.LeftShift,
        0xA1 => InputKey.RightShift,
        0xA2 => InputKey.LeftControl,
        0xA3 => InputKey.RightControl,
        0xA4 => InputKey.LeftAlt,
        0xA5 => InputKey.RightAlt,
        0x25 => InputKey.Left,
        0x27 => InputKey.Right,
        0x26 => InputKey.Up,
        0x28 => InputKey.Down,
        0x70 => InputKey.F1, 0x71 => InputKey.F2, 0x72 => InputKey.F3, 0x73 => InputKey.F4, 0x74 => InputKey.F5, 0x75 => InputKey.F6,
        0x76 => InputKey.F7, 0x77 => InputKey.F8, 0x78 => InputKey.F9, 0x79 => InputKey.F10, 0x7A => InputKey.F11, 0x7B => InputKey.F12,
        _ => InputKey.None,
    };

    private const uint WM_DESTROY = 0x0002;
    private const uint WM_PAINT = 0x000F;
    private const uint WM_ERASEBKGND = 0x0014;
    private const uint WM_SIZE = 0x0005;
    private const uint WM_MOUSEMOVE = 0x0200;
    private const uint WM_MOUSEWHEEL = 0x020A;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_MBUTTONDOWN = 0x0207;
    private const uint WM_MBUTTONUP = 0x0208;
    private const uint WM_RBUTTONDOWN = 0x0204;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const uint WM_SYSKEYDOWN = 0x0104;
    private const uint WM_SYSKEYUP = 0x0105;
    private const uint WM_KILLFOCUS = 0x0008;
    private const uint WM_QUIT = 0x0012;

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public WndProc? lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public int fErase;
        public RECT rcPaint;
        public int fRestore;
        public int fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
    }

    private enum StandardCursor { Arrow = 32512 }

    [Flags]
    private enum WindowStyles : uint
    {
        WS_VISIBLE = 0x10000000,
        WS_OVERLAPPEDWINDOW = 0x00CF0000,
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        WindowStyles dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max, uint removeMsg);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int nExitCode);
    [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);
    [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);
    [DllImport("user32.dll")] private static extern bool InvalidateRect(IntPtr hWnd, IntPtr rect, bool erase);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
    [DllImport("user32.dll")] private static extern IntPtr SetCapture(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("kernel32.dll")] private static extern void Sleep(uint milliseconds);
}
