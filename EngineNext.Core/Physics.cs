namespace EngineNext.Core;

public sealed class PhysicsMapping
{
    private readonly HashSet<Type> _collidesWith = new();
    private readonly HashSet<Type> _triggersWith = new();
    private readonly HashSet<Type> _ignores = new();

    public PhysicsMapping CollideWith<TBlueprint>() where TBlueprint : Blueprint
    {
        _collidesWith.Add(typeof(TBlueprint));
        return this;
    }

    public PhysicsMapping TriggerWith<TBlueprint>() where TBlueprint : Blueprint
    {
        _triggersWith.Add(typeof(TBlueprint));
        return this;
    }

    public PhysicsMapping Ignore<TBlueprint>() where TBlueprint : Blueprint
    {
        _ignores.Add(typeof(TBlueprint));
        return this;
    }

    public bool CanCollide(Type? otherType)
    {
        if (otherType == null) return false;
        if (_ignores.Contains(otherType)) return false;
        return _collidesWith.Contains(otherType);
    }

    public bool CanTrigger(Type? otherType)
    {
        if (otherType == null) return false;
        if (_ignores.Contains(otherType)) return false;
        return _triggersWith.Contains(otherType);
    }
}

public enum PhysicsShapeKind
{
    Box = 1,
    Circle = 2
}

public abstract class PhysicsShape
{
    public abstract PhysicsShapeKind Kind { get; }
    public BlockVector2 Offset;
    public bool IsTrigger;
    public abstract Bounds2D GetWorldBounds(Transform transform);
    public abstract bool Overlaps(Transform a, PhysicsShape other, Transform b);
}

public sealed class BoxShape : PhysicsShape
{
    public override PhysicsShapeKind Kind => PhysicsShapeKind.Box;
    public BlockVector2 Size;

    public BoxShape(BlockVector2 offset, BlockVector2 size, bool trigger)
    {
        Offset = offset;
        Size = size;
        IsTrigger = trigger;
    }

    public override Bounds2D GetWorldBounds(Transform transform)
    {
        var pos = transform.Position + Offset;
        return new Bounds2D(pos.X.ToDouble(), pos.Y.ToDouble(), Size.X.ToDouble() * transform.Scale.X.ToDouble(), Size.Y.ToDouble() * transform.Scale.Y.ToDouble());
    }

    public override bool Overlaps(Transform a, PhysicsShape other, Transform b)
    {
        if (other is BoxShape box) return Bounds2D.Overlaps(GetWorldBounds(a), box.GetWorldBounds(b));
        if (other is CircleShape circle) return Bounds2D.OverlapsCircle(GetWorldBounds(a), circle.GetWorldCircle(b));
        return false;
    }
}

public sealed class CircleShape : PhysicsShape
{
    public override PhysicsShapeKind Kind => PhysicsShapeKind.Circle;
    public BlockScalar Radius;

    public CircleShape(BlockVector2 offset, BlockScalar radius, bool trigger)
    {
        Offset = offset;
        Radius = radius;
        IsTrigger = trigger;
    }

    public Circle2D GetWorldCircle(Transform transform)
    {
        var center = transform.Position + Offset;
        double r = Radius.ToDouble() * ((transform.Scale.X.ToDouble() + transform.Scale.Y.ToDouble()) * 0.5);
        return new Circle2D(center.X.ToDouble(), center.Y.ToDouble(), r);
    }

    public override Bounds2D GetWorldBounds(Transform transform)
    {
        var c = GetWorldCircle(transform);
        return new Bounds2D(c.X - c.Radius, c.Y - c.Radius, c.Radius * 2.0, c.Radius * 2.0);
    }

    public override bool Overlaps(Transform a, PhysicsShape other, Transform b)
    {
        if (other is CircleShape circle) return Circle2D.Overlaps(GetWorldCircle(a), circle.GetWorldCircle(b));
        if (other is BoxShape box) return Bounds2D.OverlapsCircle(box.GetWorldBounds(b), GetWorldCircle(a));
        return false;
    }
}

public sealed class PhysicsBody
{
    private readonly List<PhysicsShape> _shapes = new(4);
    public bool Enabled { get; set; } = true;
    public bool IsStatic { get; set; }
    public bool IsSolid { get; set; } = true;
    public bool UseGravity { get; set; }
    public float GravityScale { get; set; } = 1f;
    public int Layer { get; set; }
    public KinematicMotionMode MotionMode { get; set; } = KinematicMotionMode.Slide;
    public IReadOnlyList<PhysicsShape> Shapes => _shapes;

    public BoxShape AddBox(double offsetX, double offsetY, double width, double height, bool trigger = false)
    {
        var shape = new BoxShape(BlockVector2.FromDouble(offsetX, offsetY), BlockVector2.FromDouble(width, height), trigger);
        _shapes.Add(shape);
        return shape;
    }

    public CircleShape AddCircle(double offsetX, double offsetY, double radius, bool trigger = false)
    {
        var shape = new CircleShape(BlockVector2.FromDouble(offsetX, offsetY), BlockScalar.FromDouble(radius), trigger);
        _shapes.Add(shape);
        return shape;
    }

    public Bounds2D GetTotalBounds(Transform transform)
    {
        if (_shapes.Count == 0)
            return new Bounds2D(transform.Position.X.ToDouble(), transform.Position.Y.ToDouble(), 0.0, 0.0);

        Bounds2D result = _shapes[0].GetWorldBounds(transform);
        for (int i = 1; i < _shapes.Count; i++)
            result = Bounds2D.Union(result, _shapes[i].GetWorldBounds(transform));
        return result;
    }
}

public enum KinematicMotionMode
{
    Stop = 0,
    Slide = 1,
    PassThrough = 2
}

public readonly struct Bounds2D
{
    public readonly double X;
    public readonly double Y;
    public readonly double Width;
    public readonly double Height;
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public Bounds2D(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width < 0.0 ? 0.0 : width;
        Height = height < 0.0 ? 0.0 : height;
    }

    public static Bounds2D Union(Bounds2D a, Bounds2D b)
    {
        double l = Math.Min(a.Left, b.Left);
        double t = Math.Min(a.Top, b.Top);
        double r = Math.Max(a.Right, b.Right);
        double bot = Math.Max(a.Bottom, b.Bottom);
        return new Bounds2D(l, t, r - l, bot - t);
    }

    public static bool Overlaps(Bounds2D a, Bounds2D b)
    {
        if (a.Right <= b.Left) return false;
        if (a.Left >= b.Right) return false;
        if (a.Bottom <= b.Top) return false;
        if (a.Top >= b.Bottom) return false;
        return true;
    }

    public static bool OverlapsCircle(Bounds2D box, Circle2D circle)
    {
        double cx = Math.Max(box.Left, Math.Min(circle.X, box.Right));
        double cy = Math.Max(box.Top, Math.Min(circle.Y, box.Bottom));
        double dx = circle.X - cx;
        double dy = circle.Y - cy;
        return (dx * dx) + (dy * dy) <= (circle.Radius * circle.Radius);
    }
}

public readonly struct Circle2D
{
    public readonly double X;
    public readonly double Y;
    public readonly double Radius;

    public Circle2D(double x, double y, double radius)
    {
        X = x;
        Y = y;
        Radius = radius < 0.0 ? 0.0 : radius;
    }

    public static bool Overlaps(Circle2D a, Circle2D b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        double rr = a.Radius + b.Radius;
        return (dx * dx) + (dy * dy) <= (rr * rr);
    }
}

public readonly struct CollisionInfo
{
    public readonly Actor Self;
    public readonly Actor Other;
    public readonly PhysicsShape? SelfShape;
    public readonly PhysicsShape? OtherShape;

    public CollisionInfo(Actor self, Actor other, PhysicsShape? selfShape, PhysicsShape? otherShape)
    {
        Self = self;
        Other = other;
        SelfShape = selfShape;
        OtherShape = otherShape;
    }
}

public readonly struct TriggerInfo
{
    public readonly Actor Self;
    public readonly Actor Other;
    public readonly PhysicsShape? SelfShape;
    public readonly PhysicsShape? OtherShape;

    public TriggerInfo(Actor self, Actor other, PhysicsShape? selfShape, PhysicsShape? otherShape)
    {
        Self = self;
        Other = other;
        SelfShape = selfShape;
        OtherShape = otherShape;
    }
}

public readonly struct MotionIntent
{
    public readonly int Tick;
    public readonly BlockVector2 Delta;

    public MotionIntent(int tick, BlockVector2 delta)
    {
        Tick = tick;
        Delta = delta;
    }

    public static MotionIntent FromDouble(int tick, double dx, double dy)
    {
        return new MotionIntent(tick, BlockVector2.FromDouble(dx, dy));
    }
}

public readonly struct MotionResult
{
    public static readonly MotionResult Empty = new(0, BlockVector2.Zero, BlockVector2.Zero, BlockVector2.Zero, false, false, false, Array.Empty<int>());

    public readonly int Tick;
    public readonly BlockVector2 Start;
    public readonly BlockVector2 IntendedEnd;
    public readonly BlockVector2 ResolvedEnd;
    public readonly bool BlockedX;
    public readonly bool BlockedY;
    public readonly bool HadCollision;
    public readonly IReadOnlyList<int> HitActors;

    public MotionResult(int tick, BlockVector2 start, BlockVector2 intendedEnd, BlockVector2 resolvedEnd, bool blockedX, bool blockedY, bool hadCollision, IReadOnlyList<int> hitActors)
    {
        Tick = tick;
        Start = start;
        IntendedEnd = intendedEnd;
        ResolvedEnd = resolvedEnd;
        BlockedX = blockedX;
        BlockedY = blockedY;
        HadCollision = hadCollision;
        HitActors = hitActors ?? Array.Empty<int>();
    }
}

public sealed class SpatialClaim
{
    public long Id { get; set; }
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int Layer { get; set; }
    public int ActorRuntimeId { get; set; }
    public int ActorNetworkId { get; set; }
    public int ShapeIndex { get; set; }
    public bool IsTrigger { get; set; }
}

internal static class EmptyHashSet<T>
{
    public static readonly HashSet<T> Instance = new();
}

public sealed class PhysicsWorld
{
    private readonly Dictionary<int, Actor> _actorsByRuntime = new(256);
    private readonly Dictionary<(int CellX, int CellY, int Layer), HashSet<int>> _cells = new();
    private readonly Dictionary<Actor, List<(int CellX, int CellY, int Layer)>> _claimKeysByActor = new();
    private readonly Dictionary<Actor, HashSet<Actor>> _lastCollisionPairs = new();
    private readonly Dictionary<Actor, HashSet<Actor>> _lastTriggerPairs = new();
    private readonly int _cellSize = 32;
    private long _nextClaimId = 1;

    public PhysicsWorld(long population = 2_000_000, int modulus = 4096)
    {
    }

    public void Clear()
    {
        _actorsByRuntime.Clear();
        _cells.Clear();
        _claimKeysByActor.Clear();
        _lastCollisionPairs.Clear();
        _lastTriggerPairs.Clear();
        _nextClaimId = 1;
    }

    public void BeginTick(int tick) { }

    public void EndTick(Scene scene, int tick)
    {
        for (int i = 0; i < scene.Actors.Count; i++)
        {
            var actor = scene.Actors[i];
            if (actor.IsDestroyed || !actor.IsEnabled) continue;
            RefreshContacts(actor);
        }
    }

    public void Register(Actor actor)
    {
        _actorsByRuntime[actor.RuntimeActorId] = actor;
        Sync(actor);
    }

    public void Unregister(Actor actor)
    {
        RemoveClaims(actor);
        _actorsByRuntime.Remove(actor.RuntimeActorId);
        _claimKeysByActor.Remove(actor);
        _lastCollisionPairs.Remove(actor);
        _lastTriggerPairs.Remove(actor);
    }

    public void Sync(Actor actor)
    {
        RemoveClaims(actor);
        AddClaims(actor);
    }

    public MotionResult StepMotion(Actor actor, MotionIntent intent, bool recordPrediction = true)
    {
        if (!actor.Body.Enabled)
        {
            actor.Transform.TranslateBlocks(intent.Delta);
            return new MotionResult(intent.Tick, actor.Transform.Position - intent.Delta, actor.Transform.Position, actor.Transform.Position, false, false, false, Array.Empty<int>());
        }

        BlockVector2 start = actor.Transform.Position;
        BlockVector2 intended = start + intent.Delta;

        if (actor.Body.MotionMode == KinematicMotionMode.PassThrough)
        {
            actor.Transform.Position = intended;
            Sync(actor);
            var pass = new MotionResult(intent.Tick, start, intended, intended, false, false, false, Array.Empty<int>());
            if (recordPrediction && actor.NetworkMode == ActorNetworkMode.Predicted && actor.IsLocallyControlled)
                actor.Prediction.Add(new BufferedIntent { Tick = intent.Tick, Intent = intent, Predicted = pass });
            return pass;
        }

        Transform trial = CloneTransform(actor.Transform);
        trial.Position = intended;
        var candidates = QueryCandidates(actor, SweptBounds(actor, actor.Transform, trial));
        bool blockedX = false;
        bool blockedY = false;
        bool collided = false;
        var hits = new List<int>();

        if (OverlapsAnySolid(actor, trial, candidates, hits))
        {
            collided = hits.Count > 0;
            if (actor.Body.MotionMode == KinematicMotionMode.Stop)
            {
                blockedX = true;
                blockedY = true;
                trial.Position = start;
            }
            else if (actor.Body.MotionMode == KinematicMotionMode.Slide)
            {
                Transform tx = CloneTransform(actor.Transform);
                tx.Position = new BlockVector2(intended.X, start.Y);
                bool xBlocked = OverlapsAnySolid(actor, tx, candidates, null);
                if (!xBlocked) actor.Transform.Position = tx.Position; else blockedX = true;

                Transform ty = CloneTransform(actor.Transform);
                ty.Position = new BlockVector2(actor.Transform.Position.X, intended.Y);
                bool yBlocked = OverlapsAnySolid(actor, ty, candidates, null);
                if (!yBlocked) actor.Transform.Position = ty.Position; else blockedY = true;

                trial.Position = actor.Transform.Position;
            }
        }
        else
        {
            actor.Transform.Position = intended;
            trial.Position = intended;
        }

        actor.Transform.Position = trial.Position;
        Sync(actor);

        var result = new MotionResult(intent.Tick, start, intended, actor.Transform.Position, blockedX, blockedY, collided, hits);
        if (recordPrediction && actor.NetworkMode == ActorNetworkMode.Predicted && actor.IsLocallyControlled)
            actor.Prediction.Add(new BufferedIntent { Tick = intent.Tick, Intent = intent, Predicted = result });
        return result;
    }

    private void RefreshContacts(Actor actor)
    {
        var currentCollisions = new HashSet<Actor>();
        var currentTriggers = new HashSet<Actor>();
        var candidates = QueryCandidates(actor, actor.Body.GetTotalBounds(actor.Transform));

        for (int i = 0; i < candidates.Count; i++)
        {
            var other = candidates[i];
            if (ReferenceEquals(actor, other)) continue;
            if (other.IsDestroyed || !other.IsEnabled) continue;

            bool anyCollision = false;
            bool anyTrigger = false;
            PhysicsShape? selfShape = null;
            PhysicsShape? otherShape = null;

            for (int a = 0; a < actor.Body.Shapes.Count; a++)
            {
                var s1 = actor.Body.Shapes[a];
                for (int b = 0; b < other.Body.Shapes.Count; b++)
                {
                    var s2 = other.Body.Shapes[b];
                    if (!s1.Overlaps(actor.Transform, s2, other.Transform)) continue;
                    selfShape = s1;
                    otherShape = s2;
                    if (s1.IsTrigger || s2.IsTrigger) anyTrigger = CanTrigger(actor, other);
                    else anyCollision = CanCollide(actor, other);
                    if (anyCollision || anyTrigger) goto found;
                }
            }

        found:
            if (anyCollision)
            {
                currentCollisions.Add(other);
                DispatchCollision(actor, other, selfShape, otherShape);
            }
            if (anyTrigger)
            {
                currentTriggers.Add(other);
                DispatchTrigger(actor, other, selfShape, otherShape);
            }
        }

        DispatchExits(actor, currentCollisions, currentTriggers);
        _lastCollisionPairs[actor] = currentCollisions;
        _lastTriggerPairs[actor] = currentTriggers;
    }

    private void DispatchCollision(Actor actor, Actor other, PhysicsShape? selfShape, PhysicsShape? otherShape)
    {
        if (!_lastCollisionPairs.TryGetValue(actor, out var last)) last = EmptyHashSet<Actor>.Instance;
        var info = new CollisionInfo(actor, other, selfShape, otherShape);
        if (last.Contains(other)) actor.OnCollisionStay(info);
        else actor.OnCollisionEnter(info);
    }

    private void DispatchTrigger(Actor actor, Actor other, PhysicsShape? selfShape, PhysicsShape? otherShape)
    {
        if (!_lastTriggerPairs.TryGetValue(actor, out var last)) last = EmptyHashSet<Actor>.Instance;
        var info = new TriggerInfo(actor, other, selfShape, otherShape);
        if (last.Contains(other)) actor.OnTriggerStay(info);
        else actor.OnTriggerEnter(info);
    }

    private void DispatchExits(Actor actor, HashSet<Actor> currentCollisions, HashSet<Actor> currentTriggers)
    {
        if (_lastCollisionPairs.TryGetValue(actor, out var lastCollisions))
        {
            foreach (var old in lastCollisions)
                if (!currentCollisions.Contains(old)) actor.OnCollisionExit(new CollisionInfo(actor, old, null, null));
        }

        if (_lastTriggerPairs.TryGetValue(actor, out var lastTriggers))
        {
            foreach (var old in lastTriggers)
                if (!currentTriggers.Contains(old)) actor.OnTriggerExit(new TriggerInfo(actor, old, null, null));
        }
    }

    private bool OverlapsAnySolid(Actor actor, Transform trial, List<Actor> candidates, List<int>? hits)
    {
        bool overlap = false;
        for (int i = 0; i < candidates.Count; i++)
        {
            var other = candidates[i];
            if (ReferenceEquals(actor, other)) continue;
            if (!CanCollide(actor, other)) continue;

            for (int a = 0; a < actor.Body.Shapes.Count; a++)
            {
                var s1 = actor.Body.Shapes[a];
                if (s1.IsTrigger) continue;
                for (int b = 0; b < other.Body.Shapes.Count; b++)
                {
                    var s2 = other.Body.Shapes[b];
                    if (s2.IsTrigger) continue;
                    if (!s1.Overlaps(trial, s2, other.Transform)) continue;
                    overlap = true;
                    if (hits != null) hits.Add(other.RuntimeActorId);
                    goto nextOther;
                }
            }

        nextOther:
            ;
        }
        return overlap;
    }

    private Bounds2D SweptBounds(Actor actor, Transform start, Transform end)
    {
        Bounds2D a = actor.Body.GetTotalBounds(start);
        Bounds2D b = actor.Body.GetTotalBounds(end);
        return Bounds2D.Union(a, b);
    }

    private List<Actor> QueryCandidates(Actor actor, Bounds2D bounds)
    {
        var result = new List<Actor>(16);
        var seen = new HashSet<int>();
        const double edgeEpsilon = 1e-9;
        int minX = ToCell(bounds.Left);
        int maxX = ToCell(bounds.Right - edgeEpsilon);
        int minY = ToCell(bounds.Top);
        int maxY = ToCell(bounds.Bottom - edgeEpsilon);

        for (int cy = minY; cy <= maxY; cy++)
        {
            for (int cx = minX; cx <= maxX; cx++)
            {
                if (!_cells.TryGetValue((cx, cy, actor.Body.Layer), out var bucket))
                    continue;
                foreach (var runtimeId in bucket)
                {
                    if (runtimeId == actor.RuntimeActorId) continue;
                    if (!seen.Add(runtimeId)) continue;
                    if (_actorsByRuntime.TryGetValue(runtimeId, out var other)) result.Add(other);
                }
            }
        }

        return result;
    }

    private void AddClaims(Actor actor)
    {
        if (actor.IsDestroyed || !actor.Body.Enabled || actor.Body.Shapes.Count == 0) return;

        if (!_claimKeysByActor.TryGetValue(actor, out var keys))
        {
            keys = new List<(int CellX, int CellY, int Layer)>();
            _claimKeysByActor[actor] = keys;
        }

        for (int i = 0; i < actor.Body.Shapes.Count; i++)
        {
            var shape = actor.Body.Shapes[i];
            Bounds2D bounds = shape.GetWorldBounds(actor.Transform);
            const double edgeEpsilon = 1e-9;
            int minX = ToCell(bounds.Left);
            int maxX = ToCell(bounds.Right - edgeEpsilon);
            int minY = ToCell(bounds.Top);
            int maxY = ToCell(bounds.Bottom - edgeEpsilon);

            for (int cy = minY; cy <= maxY; cy++)
            {
                for (int cx = minX; cx <= maxX; cx++)
                {
                    var key = (cx, cy, actor.Body.Layer);
                    if (!_cells.TryGetValue(key, out var bucket))
                    {
                        bucket = new HashSet<int>();
                        _cells[key] = bucket;
                    }
                    bucket.Add(actor.RuntimeActorId);
                    keys.Add(key);
                    _nextClaimId++;
                }
            }
        }
    }

    private void RemoveClaims(Actor actor)
    {
        if (!_claimKeysByActor.TryGetValue(actor, out var keys)) return;
        for (int i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            if (!_cells.TryGetValue(key, out var bucket)) continue;
            bucket.Remove(actor.RuntimeActorId);
            if (bucket.Count == 0)
                _cells.Remove(key);
        }
        keys.Clear();
    }

    private int ToCell(double world) => (int)Math.Floor(world / _cellSize);

    private static Transform CloneTransform(Transform t)
    {
        return new Transform
        {
            Position = t.Position,
            Rotation = t.Rotation,
            Scale = t.Scale,
            RenderPosition = t.RenderPosition,
            RenderRotation = t.RenderRotation,
            RenderScale = t.RenderScale,
            SmoothingStrength = t.SmoothingStrength
        };
    }

    private static bool CanCollide(Actor a, Actor b)
    {
        Type? bt = b.SourceBlueprint?.GetType();
        Type? at = a.SourceBlueprint?.GetType();
        return a.SourceBlueprint?.Physics.CanCollide(bt) == true || b.SourceBlueprint?.Physics.CanCollide(at) == true;
    }

    private static bool CanTrigger(Actor a, Actor b)
    {
        Type? bt = b.SourceBlueprint?.GetType();
        Type? at = a.SourceBlueprint?.GetType();
        return a.SourceBlueprint?.Physics.CanTrigger(bt) == true || b.SourceBlueprint?.Physics.CanTrigger(at) == true;
    }
}
