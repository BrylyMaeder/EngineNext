namespace EngineNext.Core;

public sealed class SoundSystem
{
    public event Action<string>? Played;

    public void Play(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            Played?.Invoke(path);
    }
}
