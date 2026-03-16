namespace EngineNext.Core;

public abstract class Scene
{
    private readonly List<Actor> _actors = new();
    private readonly List<Actor> _sortedActors = new();
    private long _nextSpatialId = 1;

    public IReadOnlyList<Actor> Actors => _actors;
    public EngineServices Engine { get; internal set; } = null!;
    public PhysicsWorld2D Physics { get; } = new();
    public ParticleSystem2D Particles { get; } = new();
    internal SpatialIndex2D SpatialIndex { get; } = new(128);
    public Vec2 CameraPosition { get; set; } = Vec2.Zero;
    public float CameraSmoothing { get; set; } = 8f;

    public virtual void OnStart() { }
    public virtual void OnUpdate(float dt) { }
    public virtual void OnRenderBackground(RenderList list, SizeI viewport) { }
    public virtual void OnRender(RenderList list, SizeI viewport) { }
    public virtual void OnEnd() { }

    public TActor Spawn<TActor>() where TActor : Actor, new()
    {
        var actor = new TActor { Scene = this, SpatialId = _nextSpatialId++ };
        _actors.Add(actor);
        actor.InternalStart();
        return actor;
    }

    public TActor Spawn<TActor>(Prefab<TActor> prefab) where TActor : Actor, new()
    {
        var actor = prefab.Create(this);
        actor.SpatialId = _nextSpatialId++;
        _actors.Add(actor);
        return actor;
    }

    public void Add(Actor actor)
    {
        actor.Scene = this;
        actor.SpatialId = actor.SpatialId == 0 ? _nextSpatialId++ : actor.SpatialId;
        _actors.Add(actor);
        actor.InternalStart();
    }

    public void Remove(Actor actor) => _actors.Remove(actor);

    internal void UpdateActors(float dt)
    {
        Physics.Step(this, dt);
        SpatialIndex.Rebuild(_actors);

        for (var i = 0; i < _actors.Count; i++)
        {
            var actor = _actors[i];
            if (!actor.Enabled) continue;
            actor.Update(dt);
        }

        SpatialIndex.Rebuild(_actors);
        Particles.Update(dt);
    }

    internal void RenderActors(RenderList list, SizeI viewport)
    {
        _sortedActors.Clear();
        foreach (var actor in SpatialIndex.QueryRender(GetWorldViewportBounds(viewport)))
        {
            if (actor.Enabled)
                _sortedActors.Add(actor);
        }

        _sortedActors.Sort(static (a, b) =>
        {
            var layer = a.Layer.CompareTo(b.Layer);
            if (layer != 0) return layer;
            return a.SortOrder.CompareTo(b.SortOrder);
        });

        for (var i = 0; i < _sortedActors.Count; i++)
            _sortedActors[i].Render(list, viewport);

        Particles.Render(list, this, viewport);
    }

    public RectF GetWorldViewportBounds(SizeI viewport)
        => new(
            CameraPosition.X - (viewport.Width * 0.5f),
            CameraPosition.Y - (viewport.Height * 0.5f),
            viewport.Width,
            viewport.Height);

    public Vec2 WorldToScreen(Vec2 world, SizeI viewport)
        => world - CameraPosition + new Vec2(viewport.Width * 0.5f, viewport.Height * 0.5f);

    public RectF WorldToScreenRect(RectF rect, SizeI viewport)
    {
        var topLeft = WorldToScreen(new Vec2(rect.X, rect.Y), viewport);
        return new RectF(topLeft.X, topLeft.Y, rect.Width, rect.Height);
    }
}

public sealed class EngineServices
{
    public UIManager UI => Engine.UI;
    public SoundSystem Sound => Engine.Sound;
    public InputSnapshot Input => Engine.Input;
    public ActionMap Actions => Engine.Actions;
}