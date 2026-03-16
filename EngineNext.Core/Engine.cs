namespace EngineNext.Core;

public static class Engine
{
    public static Scene? Scene { get; private set; }
    public static UIManager UI { get; } = new();
    public static TimeState Time { get; } = new();
    public static ActionMap Actions { get; } = new();
    public static InputSnapshot Input { get; } = new();
    public static SoundSystem Sound { get; } = new();
    public static RenderSettings RenderSettings { get; } = new();
    public static InputMode InputMode { get; set; } = InputMode.GameAndUI;

    public static bool ExitRequested { get; private set; }

    public static void Start(Scene firstScene)
    {
        ExitRequested = false;
        SetScene(firstScene);
    }

    public static void SetScene(Scene scene)
    {
        Scene?.OnEnd();
        Scene = scene;
        Scene.Engine = new EngineServices();
        Scene.OnStart();
    }

    public static void RequestExit() => ExitRequested = true;

    public static void Tick(float deltaSeconds)
    {
        if (Scene is not null)
        {
            foreach (var actor in Scene.Actors)
                actor.SyncPreviousTransform();
        }

        Time.Advance(deltaSeconds);
        UI.ProcessOpenBindings();
        UI.Update(deltaSeconds);

        if (InputMode is InputMode.GameOnly or InputMode.GameAndUI)
        {
            Scene?.OnUpdate(deltaSeconds);
            Scene?.UpdateActors(deltaSeconds);
        }
    }

    public static void Render(RenderList list, SizeI viewport)
    {
        list.Clear();
        list.FillRect(new RectF(0, 0, viewport.Width, viewport.Height), RenderSettings.ClearColor, 0f);
        Scene?.OnRenderBackground(list, viewport);
        Scene?.RenderActors(list, viewport);
        Scene?.OnRender(list, viewport);
        UI.Render(list, viewport);
    }
}
