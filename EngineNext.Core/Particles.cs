namespace EngineNext.Core;

public enum ParticleShape
{
    Rect,
    Circle,
}

public sealed class ParticlePrefab
{
    public string Name { get; set; } = "Particles";
    public int BurstCount { get; set; } = 8;
    public float LifetimeMin { get; set; } = 0.25f;
    public float LifetimeMax { get; set; } = 0.45f;
    public float SpeedMin { get; set; } = 40f;
    public float SpeedMax { get; set; } = 120f;
    public float SizeStartMin { get; set; } = 6f;
    public float SizeStartMax { get; set; } = 10f;
    public float SizeEndMin { get; set; } = 1f;
    public float SizeEndMax { get; set; } = 2f;
    public float Gravity { get; set; }
    public float Drag { get; set; }
    public float SpreadDegrees { get; set; } = 360f;
    public float DirectionDegrees { get; set; }
    public float SpawnRadius { get; set; }
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public ParticleShape Shape { get; set; } = ParticleShape.Rect;
    public EngineColor StartColor { get; set; } = EngineColor.White;
    public EngineColor EndColor { get; set; } = new(255, 255, 255, 0);
    public string? SpritePath { get; set; }
    public float RotationSpeedMin { get; set; } = -1.5f;
    public float RotationSpeedMax { get; set; } = 1.5f;

    public ParticlePrefab Count(int value) { BurstCount = value; return this; }
    public ParticlePrefab Lifetime(float min, float max) { LifetimeMin = min; LifetimeMax = max; return this; }
    public ParticlePrefab Speed(float min, float max) { SpeedMin = min; SpeedMax = max; return this; }
    public ParticlePrefab Size(float startMin, float startMax, float endMin, float endMax) { SizeStartMin = startMin; SizeStartMax = startMax; SizeEndMin = endMin; SizeEndMax = endMax; return this; }
    public ParticlePrefab Colors(EngineColor start, EngineColor end) { StartColor = start; EndColor = end; return this; }
    public ParticlePrefab Area(float radius) { SpawnRadius = radius; return this; }
    public ParticlePrefab Direction(float degrees, float spreadDegrees) { DirectionDegrees = degrees; SpreadDegrees = spreadDegrees; return this; }
    public ParticlePrefab Physics(float gravity, float drag = 0f) { Gravity = gravity; Drag = drag; return this; }
    public ParticlePrefab AsCircle() { Shape = ParticleShape.Circle; return this; }
    public ParticlePrefab AsRect() { Shape = ParticleShape.Rect; return this; }
    public ParticlePrefab UseSprite(string? path) { SpritePath = path; return this; }
}

public readonly record struct ParticleTrigger(ParticlePrefab Prefab, Vec2 Position, Vec2 InheritedVelocity);

internal sealed class ParticleState
{
    public ParticlePrefab Prefab = null!;
    public Vec2 Position;
    public Vec2 Velocity;
    public float Lifetime;
    public float Age;
    public float SizeStart;
    public float SizeEnd;
}

public sealed class ParticleSystem2D
{
    private readonly List<ParticleState> _particles = new(2048);
    private readonly Stack<ParticleState> _pool = new();
    private readonly Random _random = new(1337);

    public void Trigger(ParticlePrefab prefab, Vec2 position) => Trigger(new ParticleTrigger(prefab, position, Vec2.Zero));

    public void Trigger(ParticleTrigger trigger)
    {
        if (trigger.Prefab is null) return;
        for (var i = 0; i < Math.Max(1, trigger.Prefab.BurstCount); i++)
        {
            var p = _pool.Count > 0 ? _pool.Pop() : new ParticleState();
            p.Prefab = trigger.Prefab;
            p.Age = 0f;
            p.Lifetime = Range(trigger.Prefab.LifetimeMin, trigger.Prefab.LifetimeMax);
            p.SizeStart = Range(trigger.Prefab.SizeStartMin, trigger.Prefab.SizeStartMax);
            p.SizeEnd = Range(trigger.Prefab.SizeEndMin, trigger.Prefab.SizeEndMax);
            var baseDirection = DegreesToRadians(trigger.Prefab.DirectionDegrees);
            var halfSpread = DegreesToRadians(trigger.Prefab.SpreadDegrees) * 0.5f;
            var direction = baseDirection + Range(-halfSpread, halfSpread);
            var speed = Range(trigger.Prefab.SpeedMin, trigger.Prefab.SpeedMax);
            var spawnAngle = Range(0f, MathF.PI * 2f);
            var spawnDistance = trigger.Prefab.SpawnRadius <= 0f ? 0f : Range(0f, trigger.Prefab.SpawnRadius);
            p.Position = trigger.Position + new Vec2(MathF.Cos(spawnAngle) * spawnDistance, MathF.Sin(spawnAngle) * spawnDistance) + new Vec2(trigger.Prefab.OffsetX, trigger.Prefab.OffsetY);
            p.Velocity = new Vec2(MathF.Cos(direction) * speed, MathF.Sin(direction) * speed) + trigger.InheritedVelocity;
            _particles.Add(p);
        }
    }


    public void Clear()
    {
        while (_particles.Count > 0)
        {
            var p = _particles[_particles.Count - 1];
            _particles.RemoveAt(_particles.Count - 1);
            _pool.Push(p);
        }
    }

    public void Update(float dt)
    {
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Age += dt;
            if (p.Age >= p.Lifetime)
            {
                _particles.RemoveAt(i);
                _pool.Push(p);
                continue;
            }
            if (p.Prefab.Drag > 0f)
                p.Velocity = Vec2.Lerp(p.Velocity, Vec2.Zero, Math.Clamp(p.Prefab.Drag * dt, 0f, 1f));
            p.Velocity += new Vec2(0f, p.Prefab.Gravity * dt);
            p.Position += p.Velocity * dt;
        }
    }

    public void Render(RenderList list, Scene scene, SizeI viewport)
    {
        for (var i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            var t = p.Lifetime <= 0.0001f ? 1f : Math.Clamp(p.Age / p.Lifetime, 0f, 1f);
            var size = p.SizeStart + ((p.SizeEnd - p.SizeStart) * t);
            var color = EngineColor.Lerp(p.Prefab.StartColor, p.Prefab.EndColor, t);
            var screen = scene.WorldToScreen(p.Position, viewport);
            var rect = new RectF(screen.X - (size * 0.5f), screen.Y - (size * 0.5f), size, size);
            if (!string.IsNullOrWhiteSpace(p.Prefab.SpritePath))
                list.DrawImage(p.Prefab.SpritePath!, rect, color, 0f);
            else if (p.Prefab.Shape == ParticleShape.Circle)
                list.FillCircle(rect, color);
            else
                list.FillRect(rect, color, MathF.Min(6f, size * 0.3f));
        }
    }

    private float Range(float min, float max)
    {
        if (max < min) (min, max) = (max, min);
        return min + ((float)_random.NextDouble() * (max - min));
    }

    private static float DegreesToRadians(float degrees) => degrees * (MathF.PI / 180f);
}
