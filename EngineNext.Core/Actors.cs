namespace EngineNext.Core;

public sealed class Transform
{
    public BlockVector2 Position;
    public BlockVector2 Rotation;
    public BlockVector2 Scale;
    public BlockVector2 RenderPosition;
    public BlockVector2 RenderRotation;
    public BlockVector2 RenderScale;
    public double SmoothingStrength { get; set; } = 20.0;
    public Vec2 PreviousPosition { get; set; } = Vec2.Zero;
    public Vec2 VisualOffset { get; set; } = Vec2.Zero;

    public Transform()
    {
        Position = BlockVector2.Zero;
        Rotation = BlockVector2.Zero;
        Scale = BlockVector2.One;
        RenderPosition = Position;
        RenderRotation = Rotation;
        RenderScale = Scale;
        PreviousPosition = Position.ToVec2();
        VisualOffset = Vec2.Zero;
    }

    public Vec2 PositionVec2
    {
        get => Position.ToVec2();
        set => Position = BlockVector2.FromVec2(value);
    }

    public Vec2 RenderPositionVec2 => RenderPosition.ToVec2();
    public Vec2 ScaleVec2
    {
        get => Scale.ToVec2();
        set => Scale = BlockVector2.FromVec2(value);
    }

    public void TranslateBlocks(BlockVector2 delta)
    {
        Position = Position + delta;
    }

    public void SmoothToPhysics(double dt)
    {
        double t = 1.0 - Math.Exp(-SmoothingStrength * dt);
        var lastRender = RenderPosition.ToVec2();
        RenderPosition = BlockVector2.Lerp(RenderPosition, Position, t);
        RenderRotation = BlockVector2.Lerp(RenderRotation, Rotation, t);
        RenderScale = BlockVector2.Lerp(RenderScale, Scale, t);
        PreviousPosition = lastRender;
        VisualOffset = RenderPosition.ToVec2() - Position.ToVec2();
    }
}

public abstract class Component
{
    public Actor Actor { get; internal set; } = null!;
    public virtual void Start() { }
    public virtual void Update(float dt) { }
}

public abstract class Blueprint
{
    public PhysicsMapping Physics { get; } = new();
    internal abstract Actor CreateUntyped(Scene scene);
    internal abstract void InitializeUntyped(Scene scene, Actor actor);
}

public abstract class Blueprint<T> : Blueprint where T : Actor
{
    protected abstract T Create(Scene scene);
    protected virtual void Initialize(Scene scene, T actor) { }

    internal override Actor CreateUntyped(Scene scene) => Create(scene);

    internal override void InitializeUntyped(Scene scene, Actor actor)
    {
        if (actor is T typed) Initialize(scene, typed);
    }
}

public abstract class Prefab<TActor> : Blueprint<TActor> where TActor : Actor, new()
{
    public virtual void Build(TActor actor) { }

    protected override TActor Create(Scene scene)
    {
        var actor = new TActor { Scene = scene };
        Build(actor);
        return actor;
    }
}

public enum VisualAnchor
{
    Center = 0,
    TopLeft = 1
}

public abstract class Actor
{
    private readonly List<Component> _components = new();
    private Animator? _animator;

    internal long SpatialId { get; set; }

    public string Name { get; set; } = string.Empty;
    public Scene? Scene { get; internal set; }
    public Blueprint? SourceBlueprint { get; internal set; }
    public int RuntimeActorId { get; internal set; }
    public int NetworkActorId { get; set; }
    public int OwnerPeerId { get; set; }
    public bool IsLocallyControlled { get; set; }
    public ActorNetworkMode NetworkMode { get; set; } = ActorNetworkMode.None;
    public Transform Transform { get; } = new();
    public PhysicsBody Body { get; } = new();
    public PredictionBuffer Prediction { get; } = new();
    public VisualSpec Visual { get; } = new();
    public IActorRenderer? Renderer { get; set; } = DefaultActorRenderer.Instance;
    public bool UseCustomRendering { get; private set; }
    public string? SpritePath
    {
        get => (Visual.Asset as SpriteAsset)?.Path;
        set => Visual.Asset = string.IsNullOrWhiteSpace(value) ? null : new SpriteAsset(value!);
    }
    public TextureAsset? Texture { get; set; }
    public MeshAsset? Mesh { get; set; }
    public EngineColor Tint
    {
        get => Visual.Tint;
        set => Visual.Tint = value;
    }
    public int Layer
    {
        get => Visual.Layer;
        set => Visual.Layer = value;
    }
    public int SortOrder
    {
        get => Visual.SortOrder;
        set => Visual.SortOrder = value;
    }
    public bool IsSpatiallyStatic { get; set; }
    public bool ParticipatesInSpatialQueries { get; set; } = true;
    public bool IsCreated { get; private set; }
    public bool IsDestroyed { get; private set; }
    public bool IsEnabled { get; private set; } = true;
    public bool Enabled
    {
        get => IsEnabled;
        set => SetEnabled(value);
    }

    public BlockVector2 Position
    {
        get => Transform.Position;
        set => Transform.Position = value;
    }

    public Vec2 Size
    {
        get => Visual.Size;
        set => Visual.Size = value;
    }
    public VisualAnchor VisualAnchor
    {
        get => Visual.Anchor;
        set => Visual.Anchor = value;
    }
    public RectF Bounds => CreateWorldRect(Transform.Position.ToVec2(), Transform.Scale.ToVec2());
    public RectF VisualBounds => CreateWorldRect(Transform.RenderPosition.ToVec2(), Transform.RenderScale.ToVec2());
    public Animator? Animator => _animator;
    public Vec2 Velocity { get; set; } = Vec2.Zero;
    public bool IsGrounded { get; private set; }
    public bool IsOnWall { get; private set; }
    public Vec2 LastMoveDelta { get; internal set; } = Vec2.Zero;


    private RectF CreateWorldRect(Vec2 position, Vec2 scale)
    {
        float pixelsPerUnit = Scene?.PixelsPerUnit ?? 32f;
        var sizeWorld = new Vec2(
            (Size.X / pixelsPerUnit) * scale.X,
            (Size.Y / pixelsPerUnit) * scale.Y);

        return VisualAnchor == VisualAnchor.Center
            ? new RectF(position.X - (sizeWorld.X * 0.5f), position.Y - (sizeWorld.Y * 0.5f), sizeWorld.X, sizeWorld.Y)
            : new RectF(position.X, position.Y, sizeWorld.X, sizeWorld.Y);
    }

    public RectF GetScreenVisualBounds(SizeI viewport)
        => Scene?.WorldToScreenRect(VisualBounds, viewport) ?? VisualBounds;

    public T Add<T>(T component) where T : Component
    {
        component.Actor = this;
        _components.Add(component);
        if (IsCreated)
            component.Start();
        return component;
    }

    public T AttachAnimator<T>(T animator) where T : Animator
    {
        _animator = animator;
        animator.Actor = this;
        if (IsCreated)
            animator.Initialize();
        return animator;
    }

    public void PlayAnimation(string name) => _animator?.Play(name);

    public void SetGroundedState(bool grounded) => IsGrounded = grounded;
    public void SetWallState(bool onWall) => IsOnWall = onWall;

    public void EnableCustomRendering() => UseCustomRendering = true;
    public void DisableCustomRendering() => UseCustomRendering = false;

    public void RenderDefault(RenderList list, SizeI viewport)
        => DefaultActorRenderer.Instance.Render(this, list, viewport);

    internal void RenderInternal(RenderList list, SizeI viewport)
    {
        if (UseCustomRendering)
        {
            Render(list, viewport);
            return;
        }

        if (Renderer is not null)
        {
            Renderer.Render(this, list, viewport);
            return;
        }

        Render(list, viewport);
    }

    public void SetEnabled(bool enabled)
    {
        if (IsDestroyed) return;
        if (IsEnabled == enabled) return;
        IsEnabled = enabled;
        if (enabled) OnEnabled();
        else OnDisabled();

        if (Engine.IsAuthority && Scene != null)
            Scene.Network.RecordEnable(this, enabled);
    }

    public void Destroy()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;
        OnDestroyed();
    }

    public MotionResult MoveIntent(MotionIntent intent)
    {
        if (Scene == null) return MotionResult.Empty;
        return Scene.Physics.StepMotion(this, intent);
    }

    public MotionResult Move(Vec2 delta)
    {
        LastMoveDelta = delta;
        return MoveIntent(new MotionIntent(Engine.Tick, BlockVector2.FromVec2(delta)));
    }

    public virtual void ApplyStatePatch(string key, string value) { }

    public void ApplyAuthoritativeTransform(int tick, BlockVector2 position, BlockVector2 rotation, BlockVector2 scale)
    {
        if (NetworkMode == ActorNetworkMode.Predicted && IsLocallyControlled)
        {
            Reconciler.Reconcile(this, new MotionCommit
            {
                Tick = tick,
                NetworkActorId = NetworkActorId,
                Position = position,
                Rotation = rotation,
                Scale = scale
            });
        }
        else
        {
            Transform.Position = position;
            Transform.Rotation = rotation;
            Transform.Scale = scale;
            Transform.RenderPosition = position;
            Transform.RenderRotation = rotation;
            Transform.RenderScale = scale;
        }
    }

    internal void InternalCreate()
    {
        if (IsCreated) return;
        IsCreated = true;
        OnCreated();
        Start();
        for (int i = 0; i < _components.Count; i++)
            _components[i].Start();
        _animator?.Initialize();
        if (IsEnabled) OnEnabled();
        Transform.RenderPosition = Transform.Position;
        Transform.RenderRotation = Transform.Rotation;
        Transform.RenderScale = Transform.Scale;
        Transform.PreviousPosition = Transform.Position.ToVec2();
        Transform.VisualOffset = Vec2.Zero;
    }

    internal void InternalFixedTick(int tick, double dt)
    {
        OnFixedTick(tick, dt);
    }

    internal void InternalVisualUpdate(double dt)
    {
        Transform.PreviousPosition = Transform.RenderPosition.ToVec2();
        Transform.VisualOffset = Vec2.Zero;
        IsOnWall = false;
        Transform.SmoothToPhysics(dt);
        float fdt = (float)dt;
        for (int i = 0; i < _components.Count; i++)
            _components[i].Update(fdt);
        _animator?.Update(fdt);
        Update(fdt);
        OnVisualUpdate(dt);
    }

    public virtual void Start() { }
    public virtual void Update(float dt) { }

    public virtual void Render(RenderList list, SizeI viewport)
    {
    }

    public virtual void OnCreated() { }
    public virtual void OnDestroyed() { }
    public virtual void OnEnabled() { }
    public virtual void OnDisabled() { }
    public virtual void OnFixedTick(int tick, double dt) { }
    public virtual void OnVisualUpdate(double dt) { }
    public virtual void OnCollisionEnter(CollisionInfo info) { }
    public virtual void OnCollisionStay(CollisionInfo info) { }
    public virtual void OnCollisionExit(CollisionInfo info) { }
    public virtual void OnTriggerEnter(TriggerInfo info) { }
    public virtual void OnTriggerStay(TriggerInfo info) { }
    public virtual void OnTriggerExit(TriggerInfo info) { }
}
