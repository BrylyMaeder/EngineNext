using EngineNext.Core;

namespace EngineNext.Platform;

public sealed class HostOptions
{
    public string Title { get; set; } = "EngineNext";
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
}

public interface IGameHost
{
    void Run(HostOptions options, Action startup);
}
