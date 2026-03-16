namespace EngineNext.Core;

public abstract class Asset
{
    protected Asset(string path)
    {
        Path = path ?? string.Empty;
    }

    public string Path { get; }
    public override string ToString() => Path;
}

public sealed class TextureAsset : Asset
{
    public TextureAsset(string path) : base(path) { }
}

public sealed class MeshAsset : Asset
{
    public MeshAsset(string path) : base(path) { }
}

public sealed class SoundAsset : Asset
{
    public SoundAsset(string path) : base(path) { }
}
