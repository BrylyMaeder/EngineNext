using EngineNext.Demo;
using EngineNext.Core;
using EngineNext.Platform;
using EngineNext.Platform.Windows;

var host = new Win32GameHost();
host.Run(new HostOptions
{
    Title = "EngineNext Demo",
    Width = 1280,
    Height = 720,
    AuthorityMode = EngineAuthorityMode.Standalone,
    FixedDeltaSeconds = 1.0 / 60.0
}, startup: static () =>
{
    Engine.RenderSettings.ClearColor = new EngineColor(18, 20, 28, 255);
    Engine.RenderSettings.ShowFpsCounter = true;
    Engine.Start(new DemoScene());
});
