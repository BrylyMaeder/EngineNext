namespace EngineNext.Core;

public sealed class BonePose
{
    public string Name { get; init; } = string.Empty;
    public string? Parent { get; init; }
    public Vec2 LocalOffset { get; set; }
    public float LocalRotation { get; set; }
    public float Length { get; set; }
    public float Thickness { get; set; } = 4f;
    public EngineColor Color { get; set; } = EngineColor.White;
    public float Radius { get; set; } = 4f;

    public Vec2 WorldStart;
    public Vec2 WorldEnd;
    public float WorldRotation;
    public Vec2 RestOffset;
    public float RestRotation;
}

public sealed class AnimationClip
{
    public string Name { get; init; } = string.Empty;
    public bool IsLooping { get; set; }
    public Func<bool>? Condition { get; set; }
    public Action<float>? ApplyPose { get; set; }
    public float Time;
}

public sealed class ClipBuilder
{
    private readonly AnimationClip _clip;

    internal ClipBuilder(AnimationClip clip) => _clip = clip;

    public ClipBuilder Loop() { _clip.IsLooping = true; return this; }
    public ClipBuilder When(Func<bool> condition) { _clip.Condition = condition; return this; }
    public ClipBuilder Pose(Action<float> apply) { _clip.ApplyPose = apply; return this; }
}

public abstract class Animator
{
    private readonly Dictionary<string, BonePose> _bones = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AnimationClip> _clips = new();
    private AnimationClip? _current;

    public Actor Actor { get; internal set; } = null!;
    public float TimeScale { get; set; } = 1f;
    protected IEnumerable<BonePose> Bones => _bones.Values;

    internal void Initialize()
    {
        if (_bones.Count == 0)
        {
            BuildSkeleton();
            foreach (var bone in _bones.Values)
            {
                bone.RestOffset = bone.LocalOffset;
                bone.RestRotation = bone.LocalRotation;
            }
            BuildAnimations();
        }
    }

    protected abstract void BuildSkeleton();
    protected abstract void BuildAnimations();

    protected void AddBone(string name, string? parent, Vec2 localOffset, float length, float thickness, EngineColor color, float radius = 4f)
    {
        _bones[name] = new BonePose
        {
            Name = name,
            Parent = parent,
            LocalOffset = localOffset,
            Length = length,
            Thickness = thickness,
            Color = color,
            Radius = radius,
            RestOffset = localOffset
        };
    }

    protected ClipBuilder Clip(string name)
    {
        var clip = new AnimationClip { Name = name };
        _clips.Add(clip);
        return new ClipBuilder(clip);
    }

    public void Play(string name)
    {
        _current = _clips.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (_current is not null)
            _current.Time = 0f;
    }

    public void Update(float dt)
    {
        if (_bones.Count == 0)
            return;

        foreach (var bone in _bones.Values)
        {
            bone.LocalOffset = bone.RestOffset;
            bone.LocalRotation = bone.RestRotation;
        }

        var auto = _clips.FirstOrDefault(x => x.Condition?.Invoke() == true);
        if (auto is not null && !ReferenceEquals(auto, _current))
        {
            _current = auto;
            _current.Time = 0f;
        }

        if (_current is not null)
        {
            _current.Time += dt * MathF.Max(0.01f, TimeScale);
            _current.ApplyPose?.Invoke(_current.Time);
            if (!_current.IsLooping)
                _current.Time = MathF.Min(_current.Time, 9999f);
        }

        SolveWorldPose();
    }

    public void Render(RenderList list, SizeI viewport)
    {
        foreach (var bone in _bones.Values)
        {
            var a = Actor.Scene.WorldToScreen(bone.WorldStart, viewport);
            var b = Actor.Scene.WorldToScreen(bone.WorldEnd, viewport);
            list.DrawLine(a, b, bone.Color, bone.Thickness);
            var nodeSize = bone.Radius * 2f;
            list.FillCircle(new RectF(a.X - bone.Radius, a.Y - bone.Radius, nodeSize, nodeSize), bone.Color);
        }
    }

    protected void SetBoneRotation(string name, float degrees)
    {
        if (_bones.TryGetValue(name, out var bone))
            bone.LocalRotation = degrees;
    }

    protected void AddBoneRotation(string name, float degrees)
    {
        if (_bones.TryGetValue(name, out var bone))
            bone.LocalRotation += degrees;
    }

    protected void SetBoneOffset(string name, Vec2 offset)
    {
        if (_bones.TryGetValue(name, out var bone))
            bone.LocalOffset = offset;
    }

    protected void AddBoneOffset(string name, Vec2 offset)
    {
        if (_bones.TryGetValue(name, out var bone))
            bone.LocalOffset += offset;
    }

    protected static float Wave(float time, float speed, float min, float max)
    {
        var t = (MathF.Sin(time * speed) + 1f) * 0.5f;
        return min + ((max - min) * t);
    }

    private void SolveWorldPose()
    {
        var root = new Vec2(Actor.Bounds.CenterX, Actor.Bounds.CenterY);
        foreach (var bone in _bones.Values)
        {
            if (string.IsNullOrWhiteSpace(bone.Parent))
            {
                bone.WorldStart = root + bone.LocalOffset + Actor.Transform.VisualOffset;
                bone.WorldRotation = bone.LocalRotation;
            }
            else if (_bones.TryGetValue(bone.Parent, out var parent))
            {
                var rotated = Rotate(bone.LocalOffset, parent.WorldRotation);
                bone.WorldStart = parent.WorldEnd + rotated;
                bone.WorldRotation = parent.WorldRotation + bone.LocalRotation;
            }
            else
            {
                bone.WorldStart = root + bone.LocalOffset;
                bone.WorldRotation = bone.LocalRotation;
            }

            var dir = FromAngleDegrees(bone.WorldRotation) * bone.Length;
            bone.WorldEnd = bone.WorldStart + dir;
        }
    }

    private static Vec2 Rotate(Vec2 value, float degrees)
    {
        var r = degrees * (MathF.PI / 180f);
        var c = MathF.Cos(r);
        var s = MathF.Sin(r);
        return new Vec2((value.X * c) - (value.Y * s), (value.X * s) + (value.Y * c));
    }

    private static Vec2 FromAngleDegrees(float degrees)
    {
        var r = degrees * (MathF.PI / 180f);
        return new Vec2(MathF.Cos(r), MathF.Sin(r));
    }
}
