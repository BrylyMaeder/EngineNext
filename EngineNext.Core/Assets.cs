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

public abstract class RenderAsset : Asset
{
    protected RenderAsset(string path) : base(path) { }
}

public sealed class SpriteAsset : RenderAsset
{
    public SpriteAsset(string path) : base(path) { }
}

public sealed class SvgAsset : RenderAsset
{
    public SvgAsset(string path) : base(path) { }
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

public static class Assets
{
    public static SpriteAsset Sprite(string path) => new(path);
    public static SvgAsset Svg(string path) => new(path);
    public static TextureAsset Texture(string path) => new(path);
    public static MeshAsset Mesh(string path) => new(path);
    public static SoundAsset Sound(string path) => new(path);
}
