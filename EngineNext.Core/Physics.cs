namespace EngineNext.Core;

public readonly record struct MoveResult(bool HitX, bool HitY, bool HitBottom, bool HitTop, Actor? HitActor)
{
    public bool HitAny => HitX || HitY;
}

public sealed class PhysicsBody2D
{
    public bool Enabled { get; set; } = true;
    public bool IsStatic { get; set; }
    public bool IsSolid { get; set; }
    public bool UseGravity { get; set; }
    public float GravityScale { get; set; } = 1f;
}

public sealed class PhysicsWorld2D
{
    public float Gravity { get; set; } = 1800f;

    public void Step(Scene scene, float dt)
    {
        for (var i = 0; i < scene.Actors.Count; i++)
        {
            var actor = scene.Actors[i];
            if (!actor.Enabled || actor.Body.IsStatic || !actor.Body.Enabled)
                continue;
            if (!actor.Body.UseGravity)
                continue;

            actor.Velocity = new Vec2(actor.Velocity.X, actor.Velocity.Y + (Gravity * actor.Body.GravityScale * dt));
        }
    }

    public MoveResult Move(Actor actor, Vec2 delta)
    {
        if (actor.Scene is null || delta == Vec2.Zero)
        {
            actor.Transform.Position += delta;
            return default;
        }

        var position = actor.Transform.Position;
        var hitX = false;
        var hitY = false;
        var hitBottom = false;
        var hitTop = false;
        Actor? hitActor = null;

        if (MathF.Abs(delta.X) > 0.0001f)
        {
            actor.Transform.Position = new Vec2(position.X + delta.X, position.Y);
            foreach (var other in actor.Scene.SpatialIndex.QueryCollision(actor.Bounds, actor))
            {
                if (!actor.Bounds.Intersects(other.Bounds)) continue;
                hitX = true;
                hitActor ??= other;
                actor.SetWallState(true);
                actor.Transform.Position = new Vec2(delta.X > 0f ? other.Bounds.Left - actor.Bounds.Width : other.Bounds.Right, actor.Transform.Position.Y);
            }
            position = actor.Transform.Position;
        }

        actor.SetGroundedState(false);
        if (MathF.Abs(delta.Y) > 0.0001f)
        {
            actor.Transform.Position = new Vec2(position.X, position.Y + delta.Y);
            foreach (var other in actor.Scene.SpatialIndex.QueryCollision(actor.Bounds, actor))
            {
                if (!actor.Bounds.Intersects(other.Bounds)) continue;
                hitY = true;
                hitActor ??= other;
                if (delta.Y > 0f)
                {
                    hitBottom = true;
                    actor.SetGroundedState(true);
                    actor.Velocity = new Vec2(actor.Velocity.X, 0f);
                    actor.Transform.Position = new Vec2(actor.Transform.Position.X, other.Bounds.Top - actor.Bounds.Height);
                }
                else
                {
                    hitTop = true;
                    actor.Velocity = new Vec2(actor.Velocity.X, 0f);
                    actor.Transform.Position = new Vec2(actor.Transform.Position.X, other.Bounds.Bottom);
                }
            }
        }

        return new MoveResult(hitX, hitY, hitBottom, hitTop, hitActor);
    }
}