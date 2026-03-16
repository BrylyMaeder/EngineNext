# EngineNext

**EngineNext is a network-first, code-first game engine built for fast iteration.**  
It is designed so you can move from an idea to a playable result with very little ceremony:

- write a `Scene`
- spawn actors with **Blueprints**
- add **physics bodies**
- draw with the built-in **render list**
- build tools and menus with **attribute-driven UI**
- add **particles**
- ship single-player, listen-server, dedicated-server, or custom networking flows without changing your engine architecture

The big idea is simple:

> **Game code should feel direct.**  
> You should be able to prototype movement, UI, pickups, collisions, particles, and multiplayer behavior in one small codebase without fighting a giant editor or a transport-locked networking stack.

---

## Why EngineNext feels easy

A lot of engines are powerful, but expensive in mental overhead.

EngineNext stays easy because the core model is tiny and consistent:

- **Scenes** own world state
- **Actors** are the things in the world
- **Blueprints** describe how actors are spawned and initialized
- **Physics bodies** define collisions and triggers
- **UI windows** are plain C# classes with attributes
- **Particles** are one prefab + one trigger call
- **Networking** is authority/prediction/interpolation at the actor level
- **Rendering** is explicit draw commands

That gives you a fast loop:

1. create a scene
2. spawn some blueprints
3. write actor logic
4. run the game
5. iterate immediately

No giant setup phase. No transport lock-in. No hidden graph magic required for basic gameplay.

---

## What “network first” means here

EngineNext does **not** treat multiplayer as a bolt-on afterthought.

The engine already has:

- authority modes: `Standalone`, `Server`, `Client`
- actor replication modes: `Authority`, `Predicted`, `RemoteInterpolated`
- per-tick world commits
- local prediction buffers
- reconciliation support
- scene-level commit application

At the same time, EngineNext is **not tied to one transport**.

You can take the commits produced by the scene and move them over:

- UDP
- ENet
- Steam Networking
- WebSockets
- relay services
- custom IPC
- your own protocol

That means the engine gives you the hard part—the gameplay model—without forcing your wire format or networking vendor.

---

# Table of contents

- [Quick start](#quick-start)
- [Core concepts](#core-concepts)
- [A complete example scene](#a-complete-example-scene)
- [Actors](#actors)
- [Blueprints and prefabs](#blueprints-and-prefabs)
- [Physics, collisions, and triggers](#physics-collisions-and-triggers)
- [Particles](#particles)
- [UI and blueprints-like workflow for tools](#ui-and-blueprints-like-workflow-for-tools)
- [Animation](#animation)
- [Input and actions](#input-and-actions)
- [Rendering](#rendering)
- [Networking without transport lock-in](#networking-without-transport-lock-in)
- [Why rapid prototyping is unusually fast](#why-rapid-prototyping-is-unusually-fast)

---

# Quick start

A minimal EngineNext app looks like this:

```csharp
using EngineNext.Core;
using EngineNext.Platform;
using EngineNext.Platform.Windows;

var host = new Win32GameHost();

host.Run(new HostOptions
{
    Title = "My EngineNext Game",
    Width = 1280,
    Height = 720,
    AuthorityMode = EngineAuthorityMode.Standalone,
    FixedDeltaSeconds = 1.0 / 60.0
}, startup: static () =>
{
    Engine.RenderSettings.ClearColor = new EngineColor(18, 20, 28, 255);
    Engine.RenderSettings.ShowFpsCounter = true;
    Engine.Start(new MyGameScene());
});
```

That is enough to boot the engine, configure the simulation tick, and start a scene.

---

# Core concepts

## Scene

A `Scene` owns the running world:

- actors
- physics world
- particles
- network state
- camera position
- rendering hooks

You typically override these methods:

```csharp
public sealed class MyGameScene : Scene
{
    public override void OnBegin() { }
    public override void OnStart() { }
    public override void OnFrame(double dt) { }
    public override void OnUpdate(float dt) { }
    public override void OnFixedTick(int tick, double dt) { }
    public override void OnRenderBackground(RenderList list, SizeI viewport) { }
    public override void OnRender(RenderList list, SizeI viewport) { }
}
```

### When to use each

- `OnBegin()`  
  Set up the scene and spawn initial actors.

- `OnFrame(double dt)`  
  Great for camera follow, visual behavior, and per-frame logic.

- `OnFixedTick(int tick, double dt)`  
  Great for deterministic gameplay and physics-oriented logic.

- `OnRenderBackground(...)`  
  Draw the world background.

- `OnRender(...)`  
  Draw scene-level overlays, debug text, or custom effects after actors.

---

## Actor

An `Actor` is the basic gameplay object.

Actors already have:

- `Transform`
- `PhysicsBody`
- networking mode
- owner/local-control flags
- size / visual anchor
- tint / sprite / texture / mesh references
- optional components
- optional animator
- collision / trigger callbacks

Typical overrides look like this:

```csharp
public sealed class Coin : Actor
{
    public override void OnCreated()
    {
        Size = new Vec2(20, 20);
        VisualAnchor = VisualAnchor.Center;
        Tint = new EngineColor(255, 220, 90, 255);
    }

    public override void Render(RenderList list, SizeI viewport)
    {
        list.FillCircle(GetScreenVisualBounds(viewport), Tint);
    }

    public override void OnTriggerEnter(TriggerInfo info)
    {
        // react when another actor enters the trigger
    }
}
```

---

# A complete example scene

This example shows why EngineNext prototypes so quickly: in one file you can define the scene, player, walls, pickups, and their blueprints.

```csharp
using EngineNext.Core;

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

        Spawn(new PickupBlueprint { Position = BlockVector2.FromDouble(10, 5), Radius = 0.35 });
        Spawn(new PickupBlueprint { Position = BlockVector2.FromDouble(6, 7), Radius = 0.35 });
        Spawn(new PickupBlueprint { Position = BlockVector2.FromDouble(14, 3), Radius = 0.35 });

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
        list.DrawText("WASD to move", new RectF(16, 16, 400, 24),
            new EngineColor(235, 240, 255, 255), 16);
    }
}
```

### Why this matters

That scene is already doing several important jobs:

- world setup
- actor spawning
- camera tracking
- a pixel/unit scale
- a render layer for background and HUD text
- player prediction setup depending on authority mode

This is the EngineNext pattern in practice: **small classes, direct logic, fast iteration**.

---

# Actors

Actors are intentionally lightweight.

## Actor lifecycle

You can hook into:

```csharp
public override void OnCreated() { }
public override void Start() { }
public override void Update(float dt) { }
public override void OnFixedTick(int tick, double dt) { }
public override void OnVisualUpdate(double dt) { }
public override void OnDestroyed() { }
public override void OnEnabled() { }
public override void OnDisabled() { }
```

## Visuals

By default, an actor can render from:

- `SpritePath`
- `Texture`
- a fallback filled rectangle

Or you can fully override `Render(...)`.

```csharp
public override void Render(RenderList list, SizeI viewport)
{
    list.FillRect(GetScreenVisualBounds(viewport), Tint, 6f);
}
```

## Components

Actors can hold plain components:

```csharp
public sealed class BobComponent : Component
{
    private float _time;

    public override void Update(float dt)
    {
        _time += dt;
        Actor.Transform.VisualOffset = new Vec2(0, MathF.Sin(_time * 4f) * 0.1f);
    }
}

public sealed class Decoration : Actor
{
    public override void OnCreated()
    {
        Add(new BobComponent());
    }
}
```

This is useful for reusable behavior without inventing a giant entity framework.

---

# Blueprints and prefabs

This is one of the most important parts of EngineNext.

## Blueprint = spawn recipe

A `Blueprint<T>` describes:

1. how to create the actor
2. how to initialize it
3. what it collides/triggers with

That makes spawning expressive and easy to read.

```csharp
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
            NetworkMode = Engine.IsAuthority
                ? ActorNetworkMode.Authority
                : ActorNetworkMode.RemoteInterpolated,
            Tint = Tint,
            Size = new Vec2(
                (float)(Radius * 2.0 * scene.PixelsPerUnit),
                (float)(Radius * 2.0 * scene.PixelsPerUnit))
        };
    }

    protected override void Initialize(Scene scene, Pickup actor)
    {
        actor.Body.AddCircle(0, 0, Radius, true);
    }
}
```

Then spawning it is just:

```csharp
var pickup = Spawn(new PickupBlueprint
{
    Position = BlockVector2.FromDouble(10, 5),
    Radius = 0.35,
    Tint = new EngineColor(255, 210, 90, 255)
});
```

### Why blueprints are powerful

They keep creation logic out of random call sites.

Instead of scattering setup across the project, the blueprint becomes the **single source of truth** for:

- physics shape creation
- network mode defaults
- spawn-time parameters
- visual defaults
- interaction rules

That makes them perfect for rapid prototyping.

---

## Collision mapping inside blueprints

A blueprint can also declare interaction rules:

```csharp
public sealed class PlayerBlueprint : Blueprint<Player>
{
    public PlayerBlueprint()
    {
        Physics.CollideWith<WallBlueprint>();
        Physics.TriggerWith<PickupBlueprint>();
    }

    protected override Player Create(Scene scene)
    {
        return new Player();
    }

    protected override void Initialize(Scene scene, Player actor)
    {
        actor.Body.MotionMode = KinematicMotionMode.Slide;
        actor.Body.AddBox(-0.5, -0.5, 1.0, 1.0, false);
        actor.Body.AddBox(-0.7, -0.7, 1.4, 1.4, true);
    }
}
```

This is excellent for clarity because the actor's intended gameplay contract is right next to its spawn definition.

---

## Prefab = simpler default builder

If you want a lighter pattern than a full custom blueprint, use `Prefab<TActor>`.

```csharp
public sealed class TorchPrefab : Prefab<Actor>
{
    public override void Build(Actor actor)
    {
        actor.Name = "Torch";
        actor.Tint = new EngineColor(255, 180, 80, 255);
        actor.Size = new Vec2(16, 32);
        actor.VisualAnchor = VisualAnchor.Center;
    }
}
```

Spawn it like this:

```csharp
var torch = Spawn(new TorchPrefab());
```

Use a full `Blueprint<T>` when you want custom construction and initialization logic.  
Use `Prefab<TActor>` when you just want a quick build recipe.

---

# Physics, collisions, and triggers

EngineNext physics is built for gameplay programming.

## Add shapes directly

You can define bodies with boxes and circles:

```csharp
actor.Body.AddBox(-0.5, -0.5, 1.0, 1.0, false); // solid
actor.Body.AddBox(-0.7, -0.7, 1.4, 1.4, true);  // trigger
actor.Body.AddCircle(0, 0, 0.35, true);         // trigger circle
```

The final `bool` decides whether the shape is a **trigger**.

---

## Motion modes

EngineNext supports simple kinematic behavior through:

```csharp
actor.Body.MotionMode = KinematicMotionMode.Stop;
actor.Body.MotionMode = KinematicMotionMode.Slide;
actor.Body.MotionMode = KinematicMotionMode.PassThrough;
```

### When to use them

- `Stop`  
  The actor stops entirely if blocked.

- `Slide`  
  The actor tries axis-separated motion, which is perfect for top-down movement and many action games.

- `PassThrough`  
  Movement ignores solids, useful for ghosts, debug cameras, or some projectile logic.

---

## Move with intent

The core movement API is direct:

```csharp
MotionIntent move = MotionIntent.FromDouble(tick, dx, dy);
MotionResult result = Scene!.Physics.StepMotion(this, move);
```

### Why `MotionResult` is useful

It tells you:

- start position
- intended end position
- resolved end position
- whether X or Y was blocked
- whether there was a collision
- what actors were hit

That is enough to build:

- walking
- dashing
- knockback
- projectiles
- platforming
- top-down action movement
- prediction + reconciliation

---

## Collision callbacks

Actors get both collision and trigger hooks:

```csharp
public override void OnCollisionEnter(CollisionInfo info) { }
public override void OnCollisionStay(CollisionInfo info) { }
public override void OnCollisionExit(CollisionInfo info) { }

public override void OnTriggerEnter(TriggerInfo info) { }
public override void OnTriggerStay(TriggerInfo info) { }
public override void OnTriggerExit(TriggerInfo info) { }
```

### Pickup example

```csharp
public override void OnTriggerEnter(TriggerInfo info)
{
    if (Engine.IsAuthority && info.Other is Pickup pickup)
        Scene!.Destroy(pickup);
}
```

That one callback is enough to make server-authoritative pickups work.

---

## A full movement example

```csharp
public sealed class Player : Actor
{
    public double Speed = 6.0;

    public override void OnCreated()
    {
        Size = new Vec2(32, 32);
        VisualAnchor = VisualAnchor.Center;
        Tint = new EngineColor(100, 170, 255, 255);
    }

    public override void OnFixedTick(int tick, double dt)
    {
        if (NetworkMode == ActorNetworkMode.RemoteInterpolated)
            return;

        sbyte mx = 0;
        sbyte my = 0;

        if (Engine.Input.Down(InputKey.A)) mx = -1;
        else if (Engine.Input.Down(InputKey.D)) mx = 1;

        if (Engine.Input.Down(InputKey.W)) my = -1;
        else if (Engine.Input.Down(InputKey.S)) my = 1;

        MotionIntent move = MotionIntent.FromDouble(
            tick,
            mx * Speed * dt,
            my * Speed * dt);

        if (Engine.IsAuthority)
            Scene!.Physics.StepMotion(this, move, false);
        else if (NetworkMode == ActorNetworkMode.Predicted && IsLocallyControlled)
            Scene!.Physics.StepMotion(this, move, true);
    }
}
```

This is a good example of EngineNext's philosophy:
the code that describes the behavior is the same code that is easy to read.

---

# Particles

Particles in EngineNext are extremely fast to prototype.

## Define a prefab

```csharp
var pickupBurst = new ParticlePrefab()
    .Count(14)
    .Lifetime(0.15f, 0.45f)
    .Speed(40f, 120f)
    .Size(8f, 14f, 1f, 3f)
    .Colors(
        new EngineColor(255, 220, 90, 255),
        new EngineColor(255, 180, 50, 0))
    .Direction(-90f, 360f)
    .Physics(gravity: 160f, drag: 2f)
    .Area(6f)
    .AsCircle();
```

This configures:

- how many particles spawn
- lifetime range
- speed range
- start/end size
- start/end color
- launch direction and spread
- gravity and drag
- spawn radius
- shape type

---

## Trigger particles

Once you have a prefab, spawning it is one line:

```csharp
Scene!.Particles.Trigger(pickupBurst, Transform.RenderPosition.ToVec2());
```

Or if you want particles to inherit velocity:

```csharp
Scene!.Particles.Trigger(new ParticleTrigger(
    pickupBurst,
    Transform.RenderPosition.ToVec2(),
    Velocity));
```

---

## Practical example: particles on pickup destroy

```csharp
public sealed class Pickup : Actor
{
    private static readonly ParticlePrefab Burst = new ParticlePrefab()
        .Count(18)
        .Lifetime(0.2f, 0.5f)
        .Speed(50f, 140f)
        .Size(6f, 10f, 1f, 2f)
        .Colors(
            new EngineColor(255, 230, 120, 255),
            new EngineColor(255, 180, 30, 0))
        .Direction(-90f, 360f)
        .Physics(120f, 1.2f)
        .Area(4f)
        .AsCircle();

    public override void OnDestroyed()
    {
        Scene?.Particles.Trigger(Burst, Transform.RenderPosition.ToVec2());
    }

    public override void Render(RenderList list, SizeI viewport)
    {
        list.FillCircle(GetScreenVisualBounds(viewport), Tint);
    }
}
```

That is a full particles workflow with almost no setup cost.

### Why this is good for iteration

Particle systems often get stuck behind editor tooling, graph assets, or custom inspectors.  
In EngineNext, you can tune a burst directly in code while testing gameplay.

That is very powerful for:

- pickups
- impacts
- bullets
- magic effects
- environment feedback
- UI celebration effects

---

# UI and blueprints-like workflow for tools

EngineNext UI is code-first and attribute-driven.

That means you can build windows and tools very quickly without a heavy widget framework.

## A simple window

```csharp
using EngineNext.Core;

[Singleton]
[Center]
[Size(420, 220)]
[Background("#11161DEE")]
[Rounded(14)]
[Padding(16)]
[OpenWith("pause")]
[ToggleOpen]
[UIWindowInputMode(InputMode.UIBlocksGame)]
public sealed class PauseMenu : UIWindow
{
    [Title]
    [FontSize(28)]
    public string Heading => "Paused";

    [Text]
    public string Hint => "Choose an option below.";

    [Button("Resume")]
    [FullWidth]
    [Order(10)]
    public void Resume()
    {
        Close();
    }

    [Button("Quit")]
    [FullWidth]
    [Order(20)]
    public void Quit()
    {
        Engine.RequestExit();
    }
}
```

### What each attribute does

- `[Singleton]`  
  Reuse one instance of the window.

- `[Center]` or `[Anchor(...)]`  
  Decide where the window appears.

- `[Size(w, h)]`  
  Set window size.

- `[Background(...)]`, `[Rounded(...)]`, `[Padding(...)]`  
  Control styling.

- `[OpenWith("pause")]`  
  Bind opening to an action.

- `[ToggleOpen]`  
  Pressing the action again closes the window.

- `[UIWindowInputMode(...)]`  
  Control whether UI blocks gameplay input.

- `[Title]`, `[Text]`, `[Button]`  
  Mark members for automatic layout.

This is one of the easiest UI workflows in the engine because it turns a normal C# class into a live window immediately.

---

## Bind the action

```csharp
Engine.Actions.Bind("pause", InputKey.Escape);
```

Once that is bound, the UI manager can open the window automatically.

---

## Open manually

You can also open windows directly:

```csharp
Engine.UI.Open<PauseMenu>();
```

Or close them:

```csharp
Engine.UI.Close<PauseMenu>();
```

---

## Build reusable UI blocks

`UIBlock` lets you build sub-panels for stats, inventories, or debug tools.

```csharp
public sealed class StatsBlock : UIBlock
{
    protected override IReadOnlyList<string> CollectRows()
    {
        return new[]
        {
            "Health: 100",
            "Armor: 25",
            "Ammo: 60"
        };
    }
}
```

You can use blocks inside window compositions when you want reusable chunks of interface.

---

## Why this UI model is fast

It is very easy to build:

- pause menus
- inventories
- settings
- tool panels
- debug overlays
- admin windows
- live prototyping controls

without a separate editor or a verbose widget hierarchy.

That makes EngineNext unusually good for rapid tool creation as well as game UI.

---

# Animation

EngineNext has a procedural animation path through `Animator`.

## Attach an animator

```csharp
public sealed class Robot : Actor
{
    public override void OnCreated()
    {
        AttachAnimator(new RobotAnimator());
    }
}
```

## Create the skeleton and clips

```csharp
public sealed class RobotAnimator : Animator
{
    protected override void BuildSkeleton()
    {
        AddBone("body", null, new Vec2(0, 0), 16, 6, new EngineColor(220, 220, 230, 255));
        AddBone("armL", "body", new Vec2(-6, 0), 12, 4, new EngineColor(120, 170, 255, 255));
        AddBone("armR", "body", new Vec2(6, 0), 12, 4, new EngineColor(120, 170, 255, 255));
    }

    protected override void BuildAnimations()
    {
        Clip("idle")
            .Loop()
            .Pose(time =>
            {
                AddBoneRotation("armL", Wave(time, 3f, -10f, 10f));
                AddBoneRotation("armR", Wave(time, 3f, 10f, -10f));
            });
    }
}
```

### Why this is useful

This gives you a direct path for:

- prototyping procedural rigs
- simple characters
- creatures
- mechanical animation
- debug visualization
- stylized line-based characters

Again, the common EngineNext pattern appears here too:
**the API is small enough to understand in one pass.**

---

# Input and actions

EngineNext gives you two levels of input:

## Direct key polling

```csharp
if (Engine.Input.Down(InputKey.W))
{
    // move up
}

if (Engine.Input.Pressed(InputKey.Space))
{
    // jump
}
```

## Action mapping

```csharp
Engine.Actions.Bind("jump", InputKey.Space);
Engine.Actions.Bind("pause", InputKey.Escape);

if (Engine.Actions.Pressed("jump"))
{
    // jump
}
```

Action maps are especially useful for UI bindings and gameplay prototypes that may change controls often.

---

# Rendering

Rendering is intentionally explicit.

The `RenderList` supports commands like:

- `FillRect`
- `StrokeRect`
- `FillCircle`
- `StrokeCircle`
- `DrawText`
- `DrawImage`
- `DrawLine`

## Example

```csharp
public override void OnRender(RenderList list, SizeI viewport)
{
    list.DrawText(
        "Prototype Build",
        new RectF(16, 16, 300, 24),
        new EngineColor(255, 255, 255, 255),
        18);

    list.StrokeRect(
        new RectF(12, 12, 180, 40),
        new EngineColor(255, 255, 255, 60),
        1f,
        8f);
}
```

This works well for:

- HUDs
- debug graphics
- menus
- quick visual effects
- editor overlays
- gameplay indicators

---

# Networking without transport lock-in

This is where EngineNext stands out.

## Authority modes

At startup, you pick:

```csharp
AuthorityMode = EngineAuthorityMode.Standalone;
AuthorityMode = EngineAuthorityMode.Server;
AuthorityMode = EngineAuthorityMode.Client;
```

That changes how the engine thinks about simulation ownership.

---

## Actor network modes

Per actor, you choose behavior such as:

```csharp
actor.NetworkMode = ActorNetworkMode.Authority;
actor.NetworkMode = ActorNetworkMode.Predicted;
actor.NetworkMode = ActorNetworkMode.RemoteInterpolated;
```

### Meaning

- `Authority`  
  The actor is simulated authoritatively.

- `Predicted`  
  The local player can move immediately, then reconcile later.

- `RemoteInterpolated`  
  The actor follows authoritative transforms smoothly.

---

## Prediction + reconciliation example

The built-in pattern looks like this:

```csharp
if (Engine.IsAuthority)
    Scene!.Physics.StepMotion(this, move, false);
else if (NetworkMode == ActorNetworkMode.Predicted && IsLocallyControlled)
    Scene!.Physics.StepMotion(this, move, true);
```

When authoritative transforms arrive later, the engine can reconcile:

```csharp
actor.ApplyAuthoritativeTransform(tick, position, rotation, scale);
```

Under the hood, predicted inputs are buffered and replayed after mismatches.

That is a huge advantage for multiplayer prototyping because the engine is already structured around the problem.

---

## Scene-level commit flow

A scene produces world commits every fixed tick:

```csharp
while (scene.Network.TryDequeue(out var commit))
{
    // serialize commit however you want
    // send it over your transport
}
```

And remote scenes can apply them:

```csharp
scene.ApplyWorldCommit(commit);
```

### Why this matters

This means EngineNext handles the gameplay-side replication model, while **you keep full control over transport**.

A very typical architecture would be:

```csharp
// server side
while (scene.Network.TryDequeue(out var commit))
{
    byte[] packet = MySerializer.Write(commit);
    myTransport.Broadcast(packet);
}

// client side
myTransport.OnPacket += packet =>
{
    WorldCommit commit = MySerializer.Read(packet);
    Engine.Scene?.ApplyWorldCommit(commit);
};
```

This is exactly what “network first without transport dependence” should mean.

You get:

- a world replication model
- prediction hooks
- interpolation support
- authority separation

without surrendering control over:

- packet layout
- reliability strategy
- relay architecture
- peer/server topology
- compression
- encryption
- backend provider choice

---

# Why rapid prototyping is unusually fast

EngineNext shines when you want to build gameplay **now**, not after hours of setup.

## Example: add a collectible system

You need:

- a `Pickup` actor
- a `PickupBlueprint`
- a trigger shape
- an `OnTriggerEnter` callback
- maybe particles in `OnDestroyed`

That is a complete feature in a tiny amount of code.

## Example: add a pause menu

You need:

- one window class
- a few attributes
- one action binding

That is all.

## Example: add multiplayer-aware movement

You need:

- actor network mode
- authority-mode check
- `StepMotion(...)`
- remote commit application

Again, very little code.

---

# A practical “all systems together” example

This shows how naturally the systems compose.

```csharp
public sealed class Chest : Actor
{
    private static readonly ParticlePrefab OpenBurst = new ParticlePrefab()
        .Count(20)
        .Lifetime(0.2f, 0.6f)
        .Speed(60f, 160f)
        .Size(8f, 12f, 2f, 4f)
        .Colors(
            new EngineColor(255, 220, 120, 255),
            new EngineColor(255, 180, 40, 0))
        .Direction(-90f, 300f)
        .Physics(140f, 1.5f)
        .Area(8f)
        .AsCircle();

    public bool IsOpen { get; private set; }

    public override void OnCreated()
    {
        Size = new Vec2(32, 24);
        VisualAnchor = VisualAnchor.Center;
        Tint = new EngineColor(150, 100, 60, 255);
    }

    public override void Render(RenderList list, SizeI viewport)
    {
        list.FillRect(GetScreenVisualBounds(viewport), Tint, 4f);
    }

    public override void OnTriggerEnter(TriggerInfo info)
    {
        if (!Engine.IsAuthority || IsOpen || info.Other is not Player)
            return;

        IsOpen = true;
        Scene!.Particles.Trigger(OpenBurst, Transform.RenderPosition.ToVec2());
        Scene.Spawn(new PickupBlueprint
        {
            Position = Transform.Position + BlockVector2.FromDouble(0.75, 0),
            Radius = 0.35,
            Tint = new EngineColor(255, 220, 90, 255)
        });
    }
}

public sealed class ChestBlueprint : Blueprint<Chest>
{
    public BlockVector2 Position { get; set; }

    public ChestBlueprint()
    {
        Physics.TriggerWith<PlayerBlueprint>();
    }

    protected override Chest Create(Scene scene)
    {
        return new Chest
        {
            Name = "Chest",
            Position = Position,
            NetworkMode = Engine.IsAuthority
                ? ActorNetworkMode.Authority
                : ActorNetworkMode.RemoteInterpolated
        };
    }

    protected override void Initialize(Scene scene, Chest actor)
    {
        actor.Body.IsStatic = true;
        actor.Body.AddBox(-0.5, -0.4, 1.0, 0.8, true);
    }
}
```

That combines:

- actor creation
- blueprint-based setup
- triggers
- particles
- authority-only gameplay changes
- spawning another actor

This is the EngineNext style at its best.

---

# What about “Blueprints”?

If you are coming from engines where blueprints mean graph scripting, EngineNext uses the term differently.

Here, a blueprint is a **strongly typed C# spawn recipe**.

That gives you a lot of the same practical value:

- reusable object definitions
- configurable spawn parameters
- consistent initialization
- readable gameplay assembly
- clear composition of scene content

but with the advantages of normal code:

- refactoring support
- compile-time checks
- easy source control diffs
- no giant visual graph dependency
- fast searchability across the project

So if your goal is rapid iteration, EngineNext blueprints are very powerful because they stay **simple, explicit, and scalable**.

---

# Why EngineNext is one of the easiest engines to prototype in

Because nearly every major system can be expressed in plain, direct code:

- **world setup** → `Scene`
- **gameplay objects** → `Actor`
- **spawn recipes** → `Blueprint<T>`
- **quick reusable recipes** → `Prefab<T>`
- **movement and collision** → `Physics.StepMotion(...)`
- **VFX** → `ParticlePrefab` + `Scene.Particles.Trigger(...)`
- **UI** → attributes on `UIWindow`
- **animation** → `Animator`
- **multiplayer architecture** → authority modes + commits + prediction

That keeps the entire engine legible.

You do not need to memorize ten different editors, asset graph systems, and hidden rules just to make a player move, open a menu, trigger a pickup, and test a network-aware prototype.

---

# Recommended next steps

A strong way to explore EngineNext is to build these in order:

1. a player blueprint with movement
2. a wall blueprint with collisions
3. a pickup with trigger-based collection
4. a particle burst on collection
5. a pause menu using `UIWindow`
6. a predicted local player + remote interpolated ghosts
7. a commit serializer over your preferred transport

By the time you finish that sequence, you will have touched nearly every core system in a very small amount of code.

---

# Final takeaway

**EngineNext is powerful because it does not bury its power under complexity.**

It gives you:

- a clear gameplay model
- quick UI creation
- direct physics hooks
- practical particle workflows
- procedural animation support
- network-aware actor simulation
- transport-agnostic multiplayer architecture

If your priority is **rapid prototyping**, **clean gameplay code**, and **multiplayer-ready design without transport lock-in**, EngineNext is an unusually strong foundation.
