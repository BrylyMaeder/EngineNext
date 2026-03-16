using EngineNext.Core;

namespace EngineNext.Demo;

public sealed class DemoScene : Scene
{
    public Player? LocalPlayer { get; private set; }

    public override void OnBegin()
    {
        PixelsPerUnit = 32f;
        CameraPosition = new Vec2(10f, 5.5f);

        Spawn(new WallBlueprint { Position = BlockVector2.FromDouble(10, 0.5), Size = BlockVector2.FromDouble(20, 1) });
        Spawn(new WallBlueprint { Position = BlockVector2.FromDouble(10, 10.5), Size = BlockVector2.FromDouble(20, 1) });
        Spawn(new WallBlueprint { Position = BlockVector2.FromDouble(0.5, 5.5), Size = BlockVector2.FromDouble(1, 11) });
        Spawn(new WallBlueprint { Position = BlockVector2.FromDouble(19.5, 5.5), Size = BlockVector2.FromDouble(1, 11) });

        Spawn(new PickupBlueprint { Position = BlockVector2.FromDouble(10, 5), Radius = 0.35, Tint = new EngineColor(255, 210, 90, 255) });
        Spawn(new PickupBlueprint { Position = BlockVector2.FromDouble(6, 7), Radius = 0.35, Tint = new EngineColor(110, 230, 140, 255) });
        Spawn(new PickupBlueprint { Position = BlockVector2.FromDouble(14, 3), Radius = 0.35, Tint = new EngineColor(110, 180, 255, 255) });

        LocalPlayer = Spawn(new PlayerBlueprint
        {
            SpawnPosition = BlockVector2.FromDouble(2, 2),
            Predicted = !Engine.IsAuthority,
            LocalControl = true
        });
    }

    public override void OnFrame(double dt)
    {
        if (LocalPlayer is not null)
        {
            var p = LocalPlayer.Transform.RenderPosition.ToVec2();
            CameraPosition = Vec2.Lerp(CameraPosition, p, 0.12f);
        }
    }

    public override void OnRenderBackground(RenderList list, SizeI viewport)
    {
        for (int y = 0; y < 11; y++)
        {
            for (int x = 0; x < 20; x++)
            {
                var tint = ((x + y) & 1) == 0
                    ? new EngineColor(30, 34, 46, 255)
                    : new EngineColor(36, 40, 54, 255);
                var world = new RectF(x, y, 1f, 1f);
                list.FillRect(WorldToScreenRect(world, viewport), tint, 0f);
            }
        }

        list.DrawText("WASD to move  •  Walk through pickups to collect them  •  F3 toggles FPS",
            new RectF(16, 16, viewport.Width - 32, 24), new EngineColor(235, 240, 255, 255), 16);
    }
}

public sealed class Player : Actor
{
    public double Speed = 6.0;

    public override void OnCreated()
    {
        Size = new Vec2(32, 32);
        VisualAnchor = VisualAnchor.Center;
        Tint = new EngineColor(100, 170, 255, 255);
    }

    public override void Render(RenderList list, SizeI viewport)
    {
        list.FillRect(GetScreenVisualBounds(viewport), Tint, 6f);
    }

    public override void OnFixedTick(int tick, double dt)
    {
        if (NetworkMode == ActorNetworkMode.RemoteInterpolated) return;

        sbyte mx = 0;
        sbyte my = 0;
        if (Engine.Input.Down(InputKey.A)) mx = -1;
        else if (Engine.Input.Down(InputKey.D)) mx = 1;
        if (Engine.Input.Down(InputKey.W)) my = -1;
        else if (Engine.Input.Down(InputKey.S)) my = 1;

        MotionIntent move = MotionIntent.FromDouble(tick, mx * Speed * dt, my * Speed * dt);

        if (Engine.IsAuthority)
            Scene!.Physics.StepMotion(this, move, false);
        else if (NetworkMode == ActorNetworkMode.Predicted && IsLocallyControlled)
            Scene!.Physics.StepMotion(this, move, true);
    }

    public override void OnTriggerEnter(TriggerInfo info)
    {
        if (Engine.IsAuthority && info.Other is Pickup pickup)
            Scene!.Destroy(pickup);
    }
}

public sealed class PlayerBlueprint : Blueprint<Player>
{
    public BlockVector2 SpawnPosition { get; set; } = BlockVector2.FromDouble(4, 4);
    public double Speed { get; set; } = 6.0;
    public bool Predicted { get; set; }
    public bool LocalControl { get; set; }

    public PlayerBlueprint()
    {
        Physics.CollideWith<WallBlueprint>();
        Physics.TriggerWith<PickupBlueprint>();
    }

    protected override Player Create(Scene scene)
    {
        return new Player
        {
            Name = "Player",
            Speed = Speed,
            NetworkMode = Predicted ? ActorNetworkMode.Predicted : (Engine.IsAuthority ? ActorNetworkMode.Authority : ActorNetworkMode.RemoteInterpolated),
            IsLocallyControlled = LocalControl,
            Position = SpawnPosition
        };
    }

    protected override void Initialize(Scene scene, Player actor)
    {
        actor.Body.MotionMode = KinematicMotionMode.Slide;
        actor.Body.AddBox(-0.5, -0.5, 1.0, 1.0, false);
        actor.Body.AddBox(-0.7, -0.7, 1.4, 1.4, true);
    }
}

public sealed class Wall : Actor
{
    public override void OnCreated()
    {
        Tint = new EngineColor(90, 96, 122, 255);
        VisualAnchor = VisualAnchor.Center;
        if (Size == Vec2.Zero)
            Size = new Vec2(32, 32);
    }

    public override void Render(RenderList list, SizeI viewport)
    {
        list.FillRect(GetScreenVisualBounds(viewport), Tint, 4f);
    }
}

public sealed class WallBlueprint : Blueprint<Wall>
{
    public BlockVector2 Position { get; set; } = BlockVector2.Zero;
    public BlockVector2 Size { get; set; } = BlockVector2.FromDouble(1, 1);

    protected override Wall Create(Scene scene)
    {
        return new Wall
        {
            Name = "Wall",
            Position = Position,
            NetworkMode = Engine.IsAuthority ? ActorNetworkMode.Authority : ActorNetworkMode.RemoteInterpolated,
            Size = new Vec2(Size.X.ToFloat() * scene.PixelsPerUnit, Size.Y.ToFloat() * scene.PixelsPerUnit)
        };
    }

    protected override void Initialize(Scene scene, Wall actor)
    {
        actor.Body.IsStatic = true;
        double w = Size.X.ToDouble();
        double h = Size.Y.ToDouble();
        actor.Body.AddBox(-w * 0.5, -h * 0.5, w, h, false);
    }
}

public sealed class Pickup : Actor
{
    public override void OnCreated()
    {
        Size = new Vec2(22, 22);
        VisualAnchor = VisualAnchor.Center;
    }

    public override void Render(RenderList list, SizeI viewport)
    {
        list.FillCircle(GetScreenVisualBounds(viewport), Tint);
    }
}

public sealed class PickupBlueprint : Blueprint<Pickup>
{
    public BlockVector2 Position { get; set; } = BlockVector2.Zero;
    public double Radius { get; set; } = 0.5;
    public EngineColor Tint { get; set; } = new EngineColor(255, 210, 90, 255);

    protected override Pickup Create(Scene scene)
    {
        return new Pickup
        {
            Name = "Pickup",
            Position = Position,
            NetworkMode = Engine.IsAuthority ? ActorNetworkMode.Authority : ActorNetworkMode.RemoteInterpolated,
            Tint = Tint,
            Size = new Vec2((float)(Radius * 2.0 * scene.PixelsPerUnit), (float)(Radius * 2.0 * scene.PixelsPerUnit))
        };
    }

    protected override void Initialize(Scene scene, Pickup actor)
    {
        actor.Body.AddCircle(0, 0, Radius, true);
    }
}
