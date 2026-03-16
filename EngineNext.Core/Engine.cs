namespace EngineNext.Core;

public static class Engine
{
    private static Scene? _currentScene;

    public static Scene? CurrentScene => _currentScene;
    public static Scene? Scene => _currentScene;
    public static UIManager UI { get; } = new();
    public static TimeState Time { get; } = new();
    public static ActionMap Actions { get; } = new();
    public static InputSnapshot Input { get; } = new();
    public static SoundSystem Sound { get; } = new();
    public static RenderSettings RenderSettings { get; } = new();
    public static InputMode InputMode { get; set; } = InputMode.GameAndUI;

    public static EngineAuthorityMode AuthorityMode { get; private set; } = EngineAuthorityMode.Standalone;
    public static bool IsAuthority => AuthorityMode == EngineAuthorityMode.Standalone || AuthorityMode == EngineAuthorityMode.Server;
    public static bool IsServer => AuthorityMode == EngineAuthorityMode.Server;
    public static bool IsClient => AuthorityMode == EngineAuthorityMode.Client;
    public static bool IsStandalone => AuthorityMode == EngineAuthorityMode.Standalone;
    private static int _tick;
    public static int Tick => _tick;
    public static double FixedDeltaSeconds { get; private set; } = 1.0 / 60.0;
    public static bool ExitRequested { get; private set; }

    public static void Configure(EngineAuthorityMode authorityMode, double fixedDeltaSeconds = 1.0 / 60.0)
    {
        AuthorityMode = authorityMode;
        FixedDeltaSeconds = fixedDeltaSeconds > 0.0 ? fixedDeltaSeconds : (1.0 / 60.0);
    }

    public static void Start(Scene firstScene)
    {
        ExitRequested = false;
        SetScene(firstScene);
    }

    public static void SetScene(Scene scene)
    {
        if (ReferenceEquals(_currentScene, scene)) return;
        _currentScene?.InternalEnd();
        _currentScene = scene;
        _tick = 0;
        if (_currentScene is not null)
            _currentScene.Engine = new EngineServices();
        _currentScene?.InternalBegin();
    }

    public static void RequestExit() => ExitRequested = true;

    public static void Update(double dt)
    {
        if (dt < 0.0) dt = 0.0;
        Time.Advance((float)dt);
        UI.ProcessOpenBindings();
        UI.Update((float)dt);

        if (InputMode is InputMode.GameOnly or InputMode.GameAndUI)
            _currentScene?.InternalUpdate(dt, FixedDeltaSeconds, ref _tick);
        else
            _currentScene?.InternalVisualOnlyUpdate(dt);
    }

    public static void TickFrame(float deltaSeconds) => Update(deltaSeconds);

    public static void Render(RenderList list, SizeI viewport)
    {
        list.Clear();
        list.FillRect(new RectF(0, 0, viewport.Width, viewport.Height), RenderSettings.ClearColor, 0f);
        _currentScene?.OnRenderBackground(list, viewport);
        _currentScene?.RenderActors(list, viewport);
        _currentScene?.OnRender(list, viewport);
        UI.Render(list, viewport);
    }
}
