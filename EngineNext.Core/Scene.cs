namespace EngineNext.Core;

public abstract class Scene
{
    private readonly List<Actor> _actors = new(256);
    private readonly List<Actor> _sortedActors = new(256);
    private readonly Dictionary<int, Actor> _actorsByRuntimeId = new(256);
    private readonly List<Actor> _pendingDestroy = new(64);
    private bool _begun;
    private double _fixedAccumulator;
    private int _nextRuntimeActorId = 1;

    protected Scene(long physicsPopulation = 2_000_000, int physicsModulus = 4096)
    {
        Physics = new PhysicsWorld(physicsPopulation, physicsModulus);
        Network = new SceneNetworkState();
        Particles = new ParticleSystem2D();
    }

    public IReadOnlyList<Actor> Actors => _actors;
    public EngineServices Engine { get; internal set; } = null!;
    public PhysicsWorld Physics { get; }
    public ParticleSystem2D Particles { get; }
    public SceneNetworkState Network { get; }
    public string SceneName => GetType().Name;
    public Vec2 CameraPosition { get; set; } = Vec2.Zero;
    public float CameraSmoothing { get; set; } = 8f;
    public float PixelsPerUnit { get; set; } = 32f;

    public virtual void OnBegin() { }
    public virtual void OnStart() { }
    public virtual void OnEnd() { }
    public virtual void OnFrame(double dt) { }
    public virtual void OnUpdate(float dt) { }
    public virtual void OnFixedTick(int tick, double dt) { }
    public virtual void OnRenderBackground(RenderList list, SizeI viewport) { }
    public virtual void OnRender(RenderList list, SizeI viewport) { }

    public T Spawn<T>(Blueprint<T> blueprint) where T : Actor
    {
        ArgumentNullException.ThrowIfNull(blueprint);

        var created = blueprint.CreateUntyped(this);
        if (created is not T actor)
            throw new InvalidOperationException($"Blueprint '{blueprint.GetType().Name}' returned an incompatible actor type.");

        actor.Scene = this;
        actor.SourceBlueprint = blueprint;
        actor.RuntimeActorId = _nextRuntimeActorId++;
        actor.NetworkActorId = actor.NetworkActorId == 0 ? actor.RuntimeActorId : actor.NetworkActorId;
        actor.Name = string.IsNullOrWhiteSpace(actor.Name)
            ? blueprint.GetType().Name.Replace("Blueprint", string.Empty, StringComparison.Ordinal)
            : actor.Name;
        actor.SpatialId = actor.RuntimeActorId;

        blueprint.InitializeUntyped(this, actor);

        _actors.Add(actor);
        _actorsByRuntimeId[actor.RuntimeActorId] = actor;
        actor.InternalCreate();
        Physics.Register(actor);

        if (EngineNext.Core.Engine.IsAuthority)
            Network.RecordSpawn(actor);

        return actor;
    }

    public TActor Spawn<TActor>() where TActor : Actor, new()
    {
        return Spawn(new SceneSpawnBlueprint<TActor>());
    }

    public TActor Spawn<TActor>(Prefab<TActor> prefab) where TActor : Actor, new()
    {
        return Spawn<TActor>((Blueprint<TActor>)prefab);
    }

    public void Add(Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        actor.Scene = this;
        actor.SourceBlueprint ??= new ExistingActorBlueprint(actor.GetType());
        actor.RuntimeActorId = actor.RuntimeActorId == 0 ? _nextRuntimeActorId++ : actor.RuntimeActorId;
        actor.NetworkActorId = actor.NetworkActorId == 0 ? actor.RuntimeActorId : actor.NetworkActorId;
        actor.SpatialId = actor.RuntimeActorId;
        _actors.Add(actor);
        _actorsByRuntimeId[actor.RuntimeActorId] = actor;
        actor.InternalCreate();
        Physics.Register(actor);
    }

    public void Destroy(Actor actor)
    {
        if (actor.Scene != this) return;
        if (actor.IsDestroyed) return;
        actor.Destroy();
        _pendingDestroy.Add(actor);
        if (EngineNext.Core.Engine.IsAuthority)
            Network.RecordDestroy(actor);
    }

    public void Remove(Actor actor)
    {
        if (actor.Scene != this) return;
        Destroy(actor);
        FlushDestroyed();
    }

    public virtual void ApplyWorldCommit(WorldCommit? commit)
    {
        if (commit == null) return;
        if (!string.Equals(commit.SceneName, SceneName, StringComparison.Ordinal)) return;

        for (int i = 0; i < commit.Spawns.Count; i++) ApplySpawn(commit.Spawns[i]);
        for (int i = 0; i < commit.Destroys.Count; i++) ApplyDestroy(commit.Destroys[i]);
        for (int i = 0; i < commit.Enables.Count; i++) ApplyEnable(commit.Enables[i], true);
        for (int i = 0; i < commit.Disables.Count; i++) ApplyEnable(commit.Disables[i], false);
        for (int i = 0; i < commit.Transforms.Count; i++) ApplyTransform(commit.Transforms[i]);
        for (int i = 0; i < commit.States.Count; i++) ApplyStatePatch(commit.States[i]);
    }

    protected virtual void ApplySpawn(WorldSpawn op)
    {
        if (op.Blueprint == null) return;

        var actor = op.Blueprint.CreateUntyped(this);
        actor.Scene = this;
        actor.SourceBlueprint = op.Blueprint;
        actor.RuntimeActorId = _nextRuntimeActorId++;
        actor.NetworkActorId = op.NetworkActorId;
        actor.SpatialId = actor.RuntimeActorId;
        actor.Name = op.Name;
        actor.Transform.Position = op.Position;
        actor.Transform.Rotation = op.Rotation;
        actor.Transform.Scale = op.Scale;

        op.Blueprint.InitializeUntyped(this, actor);

        _actors.Add(actor);
        _actorsByRuntimeId[actor.RuntimeActorId] = actor;
        actor.InternalCreate();
        Physics.Register(actor);
    }

    protected virtual void ApplyDestroy(int networkActorId)
    {
        var actor = FindNetworkActor(networkActorId);
        if (actor != null) Destroy(actor);
    }

    protected virtual void ApplyEnable(int networkActorId, bool enabled)
    {
        var actor = FindNetworkActor(networkActorId);
        actor?.SetEnabled(enabled);
    }

    protected virtual void ApplyTransform(WorldTransform op)
    {
        var actor = FindNetworkActor(op.NetworkActorId);
        if (actor == null) return;
        actor.ApplyAuthoritativeTransform(op.Tick, op.Position, op.Rotation, op.Scale);
        Physics.Sync(actor);
    }

    protected virtual void ApplyStatePatch(WorldStatePatch op)
    {
        var actor = FindNetworkActor(op.NetworkActorId);
        actor?.ApplyStatePatch(op.Key, op.Value);
    }

    public Actor? FindNetworkActor(int networkActorId)
    {
        for (int i = 0; i < _actors.Count; i++)
            if (_actors[i].NetworkActorId == networkActorId)
                return _actors[i];
        return null;
    }

    internal void InternalBegin()
    {
        if (_begun) return;
        _begun = true;
        Network.Reset();
        OnBegin();
        OnStart();
    }

    internal void InternalEnd()
    {
        for (int i = 0; i < _actors.Count; i++)
        {
            var actor = _actors[i];
            Physics.Unregister(actor);
            if (!actor.IsDestroyed)
                actor.Destroy();
        }

        _actors.Clear();
        _actorsByRuntimeId.Clear();
        _pendingDestroy.Clear();
        Network.Reset();
        Physics.Clear();
        Particles.Clear();
        _fixedAccumulator = 0.0;
        _begun = false;
        OnEnd();
    }

    internal void InternalUpdate(double dt, double fixedDelta, ref int globalTick)
    {
        OnFrame(dt);
        OnUpdate((float)dt);
        _fixedAccumulator += dt;

        while (_fixedAccumulator >= fixedDelta)
        {
            globalTick++;
            Network.BeginTick(globalTick);
            Physics.BeginTick(globalTick);
            OnFixedTick(globalTick, fixedDelta);

            for (int i = 0; i < _actors.Count; i++)
            {
                var actor = _actors[i];
                if (actor.IsDestroyed || !actor.IsEnabled) continue;
                actor.InternalFixedTick(globalTick, fixedDelta);
                Physics.Sync(actor);
                if (EngineNext.Core.Engine.IsAuthority)
                    Network.RecordTransform(actor);
            }

            Physics.EndTick(this, globalTick);
            FlushDestroyed();
            _fixedAccumulator -= fixedDelta;
            Network.EndTick();
        }

        Particles.Update((float)dt);
        for (int i = 0; i < _actors.Count; i++)
        {
            var actor = _actors[i];
            if (actor.IsDestroyed) continue;
            actor.InternalVisualUpdate(dt);
        }
    }

    internal void InternalVisualOnlyUpdate(double dt)
    {
        Particles.Update((float)dt);
        for (int i = 0; i < _actors.Count; i++)
        {
            var actor = _actors[i];
            if (actor.IsDestroyed) continue;
            actor.InternalVisualUpdate(dt);
        }
    }

    private void FlushDestroyed()
    {
        if (_pendingDestroy.Count == 0) return;

        for (int i = 0; i < _pendingDestroy.Count; i++)
        {
            var actor = _pendingDestroy[i];
            Physics.Unregister(actor);
            _actors.Remove(actor);
            _actorsByRuntimeId.Remove(actor.RuntimeActorId);
        }

        _pendingDestroy.Clear();
    }

    internal void RenderActors(RenderList list, SizeI viewport)
    {
        _sortedActors.Clear();
        var worldBounds = GetWorldViewportBounds(viewport);
        foreach (var actor in _actors)
        {
            if (!actor.IsEnabled || actor.IsDestroyed) continue;
            if (actor.ParticipatesInSpatialQueries && !actor.VisualBounds.Intersects(worldBounds)) continue;
            _sortedActors.Add(actor);
        }

        _sortedActors.Sort(static (a, b) =>
        {
            var layer = a.Layer.CompareTo(b.Layer);
            if (layer != 0) return layer;
            return a.SortOrder.CompareTo(b.SortOrder);
        });

        for (var i = 0; i < _sortedActors.Count; i++)
            _sortedActors[i].RenderInternal(list, viewport);

        Particles.Render(list, this, viewport);
    }

    public RectF GetWorldViewportBounds(SizeI viewport)
        => new(
            CameraPosition.X - ((viewport.Width * 0.5f) / PixelsPerUnit),
            CameraPosition.Y - ((viewport.Height * 0.5f) / PixelsPerUnit),
            viewport.Width / PixelsPerUnit,
            viewport.Height / PixelsPerUnit);

    public Vec2 WorldToScreen(Vec2 world, SizeI viewport)
        => new(
            ((world.X - CameraPosition.X) * PixelsPerUnit) + (viewport.Width * 0.5f),
            ((world.Y - CameraPosition.Y) * PixelsPerUnit) + (viewport.Height * 0.5f));

    public RectF WorldToScreenRect(RectF rect, SizeI viewport)
    {
        var topLeft = WorldToScreen(new Vec2(rect.X, rect.Y), viewport);
        return new RectF(topLeft.X, topLeft.Y, rect.Width * PixelsPerUnit, rect.Height * PixelsPerUnit);
    }

    private sealed class SceneSpawnBlueprint<TActor> : Prefab<TActor> where TActor : Actor, new()
    {
    }

    private sealed class ExistingActorBlueprint : Blueprint<Actor>
    {
        private readonly Type _type;

        public ExistingActorBlueprint(Type type)
        {
            _type = type;
        }

        protected override Actor Create(Scene scene)
        {
            return (Actor)Activator.CreateInstance(_type)!;
        }
    }
}

public sealed class EngineServices
{
    public UIManager UI => Engine.UI;
    public SoundSystem Sound => Engine.Sound;
    public InputSnapshot Input => Engine.Input;
    public ActionMap Actions => Engine.Actions;
    public TimeState Time => Engine.Time;
    public RenderSettings RenderSettings => Engine.RenderSettings;
    public EngineAuthorityMode AuthorityMode => Engine.AuthorityMode;
    public bool IsAuthority => Engine.IsAuthority;
    public bool IsServer => Engine.IsServer;
    public bool IsClient => Engine.IsClient;
    public bool IsStandalone => Engine.IsStandalone;
    public int Tick => Engine.Tick;
    public double FixedDeltaSeconds => Engine.FixedDeltaSeconds;
}
