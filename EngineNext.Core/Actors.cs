namespace EngineNext.Core;

public sealed class Transform2D
{
    public Vec2 Position { get; set; } = Vec2.Zero;
    public Vec2 Scale { get; set; } = Vec2.One;
    public float Rotation { get; set; }
    public Vec2 PreviousPosition { get; set; } = Vec2.Zero;
    public Vec2 VisualOffset { get; set; } = Vec2.Zero;
}

public abstract class Component
{
    public Actor Actor { get; internal set; } = null!;
    public virtual void Start() { }
    public virtual void Update(float dt) { }
}

public abstract class Actor
{
    private readonly List<Component> _components = new();
    private Animator? _animator;

    internal long SpatialId { get; set; }

    public Scene Scene { get; internal set; } = null!;
    public Transform2D Transform { get; } = new();
    public PhysicsBody2D Body { get; } = new();
    public string Name { get; set; } = string.Empty;
    public Vec2 Size { get; set; } = new(32f, 32f);
    public string? SpritePath { get; set; }
    public EngineColor Tint { get; set; } = new(100, 170, 255, 255);
    public bool Enabled { get; set; } = true;
    public bool IsSpatiallyStatic { get; set; }
    public bool ParticipatesInSpatialQueries { get; set; } = true;
    public int Layer { get; set; }
    public int SortOrder { get; set; }
    public Vec2 Velocity { get; set; } = Vec2.Zero;
    public bool IsGrounded { get; private set; }
    public bool IsOnWall { get; private set; }
    public Vec2 LastMoveDelta { get; internal set; } = Vec2.Zero;

    internal bool HasStarted { get; set; }

    public RectF Bounds => new(Transform.Position.X, Transform.Position.Y, Size.X * Transform.Scale.X, Size.Y * Transform.Scale.Y);
    public RectF VisualBounds => new(Bounds.X + Transform.VisualOffset.X, Bounds.Y + Transform.VisualOffset.Y, Bounds.Width, Bounds.Height);
    public Animator? Animator => _animator;

    public void SetGroundedState(bool grounded) => IsGrounded = grounded;
    public void SetWallState(bool onWall) => IsOnWall = onWall;

    public T Add<T>(T component) where T : Component
    {
        component.Actor = this;
        _components.Add(component);
        return component;
    }

    public T AttachAnimator<T>(T animator) where T : Animator
    {
        _animator = animator;
        animator.Actor = this;
        if (HasStarted)
            animator.Initialize();
        return animator;
    }

    public void PlayAnimation(string name) => _animator?.Play(name);

    public virtual void Start() { }

    public virtual void Update(float dt)
    {
        foreach (var component in _components)
            component.Update(dt);

        _animator?.Update(dt);
    }

    public virtual void Render(RenderList list, SizeI viewport)
    {
        if (_animator is not null)
        {
            _animator.Render(list, viewport);
            return;
        }

        var rect = Scene.WorldToScreenRect(VisualBounds, viewport);
        if (!string.IsNullOrWhiteSpace(SpritePath))
            list.DrawImage(SpritePath!, rect, Tint, 0f);
        else
            list.FillRect(rect, Tint, 6f);
    }

    public MoveResult Move(Vec2 delta)
    {
        LastMoveDelta = delta;
        return Scene.Physics.Move(this, delta);
    }

    internal void InternalStart()
    {
        if (HasStarted) return;
        HasStarted = true;
        Transform.PreviousPosition = Transform.Position;
        Start();
        foreach (var component in _components)
            component.Start();
        _animator?.Initialize();
    }

    internal void SyncPreviousTransform()
    {
        Transform.PreviousPosition = Transform.Position;
        Transform.VisualOffset = Vec2.Zero;
        IsOnWall = false;
    }
}

public abstract class Prefab<TActor> where TActor : Actor, new()
{
    public virtual void Build(TActor actor) { }

    public TActor Create(Scene scene)
    {
        var actor = new TActor { Scene = scene };
        Build(actor);
        actor.InternalStart();
        return actor;
    }
}