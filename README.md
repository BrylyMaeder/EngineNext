````markdown
# EngineNext

EngineNext is a lightweight, code-first 2D engine for developers who want the shortest path from an idea to a running game.

It is built around a small set of clear primitives:

- **Scenes** define the world
- **Actors** define behavior
- **Physics** handles movement and collision
- **Rendering** is driven by an explicit draw list
- **Input** is action-based
- **UI** is code-defined and attribute-driven
- **Particles** are fast to trigger and easy to shape
- **Animation** is procedural and skeleton-based
- **Platform hosts** run the engine on real windows

The goal is not to bury game logic behind tooling. The goal is to let you write plain C#, stay close to the frame loop, and still have enough structure to build something real.

---

## Why EngineNext

EngineNext is designed to make the common path feel obvious.

Instead of requiring a large editor workflow, deep object graphs, or a large amount of startup code, it gives you a minimal runtime model:

1. Start the engine with a scene
2. Spawn actors
3. Update them every frame
4. Render only what matters
5. Use built-in systems when needed

That makes it a strong fit for:

- gameplay prototypes
- custom 2D games
- jam projects
- engine experiments
- deterministic-feeling code-first workflows
- teams who want source-level control instead of editor-driven abstraction

---

## What’s in this repository

This solution is organized into three main projects:

- **EngineNext.Core** — scenes, actors, rendering, physics, particles, input, UI, animation, primitives, sound hooks, and core engine state
- **EngineNext.Platform** — hosting abstractions such as `IGameHost` and `HostOptions`
- **EngineNext.Platform.Windows** — a native Win32 host with a software-backed renderer

---

## Core ideas

EngineNext is easiest to understand if you look at its main pieces one at a time.

### Engine

`Engine` is the runtime entry point and the owner of the global engine services.

It exposes:

- `Scene`
- `UI`
- `Time`
- `Actions`
- `Input`
- `Sound`
- `RenderSettings`

It also owns the top-level lifecycle:

- `Start(Scene firstScene)`
- `SetScene(Scene scene)`
- `Tick(float deltaSeconds)`
- `Render(RenderList list, SizeI viewport)`
- `RequestExit()`

This makes the engine easy to reason about: there is one current scene, one frame tick, and one render pass.

---

### Scenes

A `Scene` is the world container.

A scene owns:

- actors
- physics world
- particle system
- spatial index
- camera position
- camera smoothing

A scene is where you set up the world, spawn game objects, and control high-level flow.

Lifecycle hooks:

- `OnStart()`
- `OnUpdate(float dt)`
- `OnRenderBackground(RenderList list, SizeI viewport)`
- `OnRender(RenderList list, SizeI viewport)`
- `OnEnd()`

A scene can also:

- `Spawn<TActor>()`
- `Spawn<TActor>(Prefab<TActor> prefab)`
- `Add(Actor actor)`
- `Remove(Actor actor)`

This keeps scene code focused on orchestration rather than low-level plumbing.

---

### Actors

An `Actor` is the main gameplay unit.

Every actor comes with:

- a `Transform`
- a `PhysicsBody2D`
- a `Name`
- `Size`
- `SpritePath`
- `Tint`
- `Velocity`
- layer and sort values
- enabled state
- spatial participation flags

Actor hooks:

- `Start()`
- `Update(float dt)`
- `Render(RenderList list, SizeI viewport)`

Actors also support:

- components through `Add<T>(T component)`
- animator attachment through `AttachAnimator<T>(T animator)`
- collision-aware motion through `Move(Vec2 delta)`

This gives each actor a strong default shape without forcing inheritance-heavy design.

---

### Rendering

Rendering is explicit.

The engine builds a `RenderList`, and the platform host interprets that list to draw:

- filled rectangles
- stroked rectangles
- circles
- text
- images
- lines

Instead of hiding rendering behind a retained UI tree or an opaque graphics abstraction, EngineNext collects draw commands each frame. That makes it straightforward to debug and easy to extend.

The engine render order is:

1. clear screen
2. scene background render hook
3. visible actors
4. scene foreground render hook
5. UI

Actors are spatially queried before rendering, then sorted by:

- `Layer`
- `SortOrder`

That means only actors in the world viewport are considered for draw.

---

### Physics

Physics is intentionally simple and practical.

`PhysicsWorld2D` currently provides:

- gravity application
- axis-separated movement
- solid collision resolution
- grounded and wall contact state updates

Each actor has a `PhysicsBody2D` with:

- `Enabled`
- `IsStatic`
- `IsSolid`
- `UseGravity`
- `GravityScale`

When an actor moves, collision is resolved against nearby actors from the scene’s spatial index.

This is a good model for platformers, top-down collision, and custom gameplay movement where you want predictable behavior.

---

### Input

Input is split into two levels:

#### Raw input

`InputSnapshot` tracks:

- keys currently down
- keys pressed this frame
- keys released this frame
- mouse position
- mouse wheel delta

#### Action mapping

`ActionMap` lets you bind named actions to keys:

- `Bind(string action, params InputKey[] keys)`
- `Pressed(string action)`
- `Down(string action)`

That means gameplay code can read actions like `"Jump"` or `"Shoot"` instead of hardcoding physical keys everywhere.

Engine input modes:

- `GameOnly`
- `UIOnly`
- `GameAndUI`
- `UIBlocksGame`

This gives you a clean way to switch input behavior between menus and gameplay.

---

### UI

The UI system is code-defined and reflection-driven.

`UIWindow` and `UIElement` provide a lightweight framework for immediate-feeling interface work without requiring separate markup files.

The UI system supports:

- open/close behavior
- window attributes for layout and style
- text
- titles
- buttons
- nested UI blocks
- mouse hit regions

The system is especially useful for:

- pause menus
- title screens
- overlays
- debug windows
- editor-like in-game tools

Because UI lives in code, it stays close to gameplay logic.

---

### Particles

The particle system is designed for fast, expressive effects without heavy setup.

A `ParticlePrefab` can define:

- burst count
- lifetime range
- speed range
- size over lifetime
- start and end colors
- direction and spread
- spawn radius
- gravity
- drag
- shape
- optional sprite path

A scene owns a `ParticleSystem2D`, and particles can be triggered instantly.

This makes effects like sparks, dust, muzzle flashes, debris, smoke, and magical bursts easy to author in code.

---

### Animation

EngineNext includes a procedural skeletal animation system.

An `Animator` can:

- define bones
- define clips
- auto-switch clips based on conditions
- apply animated pose logic over time
- render a live bone hierarchy

This is different from sprite-sheet-driven animation. It is especially useful for:

- stylized stick-figure or bone-driven characters
- procedural motion
- dynamic enemies
- game feel experiments

Because the animation system is built from code and pose logic, it is very flexible.

---

### Spatial indexing

Scenes use `SpatialIndex2D` internally to accelerate:

- collision lookups
- render visibility queries

That means the engine does not have to brute-force every actor against every other actor every frame.

Static and dynamic spatial buckets are handled separately, and the scene rebuilds indexes as part of actor updates. This is one of the reasons the engine stays simple at the gameplay level while still remaining practical under load.

---

### Sound

The sound system is intentionally minimal at the core level.

`SoundSystem` currently exposes:

- `Play(string path)`
- a `Played` event

This keeps the core engine decoupled from platform-specific sound playback while still giving gameplay code a single place to trigger audio.

---

### Platform hosting

The platform layer is intentionally separated from the game runtime.

`EngineNext.Platform` defines:

- `IGameHost`
- `HostOptions`

`EngineNext.Platform.Windows` provides `Win32GameHost`, which:

- creates a native window
- processes Win32 input
- advances the engine frame loop
- paints the render list
- supports resizing
- draws an FPS overlay
- translates mouse and keyboard input into `Engine.Input`

This separation keeps the engine core focused and portable.

---

## The simplest mental model

If you only remember one thing about EngineNext, remember this:

- A **scene** owns the world
- A **scene** spawns **actors**
- **actors** update every frame
- **actors** can move through **physics**
- **actors** render into a **render list**
- **UI** renders after the world
- the **host** runs the frame loop

That is the whole engine at a useful level of abstraction.

---

## Minimal example

This is the smallest meaningful example of how the engine is meant to feel.

```csharp
using EngineNext.Core;

public sealed class GameScene : Scene
{
    public override void OnStart()
    {
        Spawn<Player>();
    }

    public override void OnUpdate(float dt)
    {
        if (Engine.Actions.Pressed("Quit"))
            Engine.RequestExit();
    }
}

public sealed class Player : Actor
{
    public override void Start()
    {
        Name = "Player";
        Size = new Vec2(32, 32);
        Tint = EngineColor.FromHex("#64AAFF");
        Transform.Position = new Vec2(100, 100);
    }

    public override void Update(float dt)
    {
        var speed = 220f;
        var move = Vec2.Zero;

        if (Engine.Actions.Down("Left"))  move += new Vec2(-speed * dt, 0);
        if (Engine.Actions.Down("Right")) move += new Vec2(speed * dt, 0);
        if (Engine.Actions.Down("Up"))    move += new Vec2(0, -speed * dt);
        if (Engine.Actions.Down("Down"))  move += new Vec2(0, speed * dt);

        Move(move);

        base.Update(dt);
    }
}
````

Start it with a host:

```csharp
using EngineNext.Core;
using EngineNext.Platform;
using EngineNext.Platform.Windows;

var host = new Win32GameHost();

host.Run(
    new HostOptions
    {
        Title = "EngineNext",
        Width = 1280,
        Height = 720
    },
    startup: () =>
    {
        Engine.Actions.Bind("Left", InputKey.A, InputKey.Left);
        Engine.Actions.Bind("Right", InputKey.D, InputKey.Right);
        Engine.Actions.Bind("Up", InputKey.W, InputKey.Up);
        Engine.Actions.Bind("Down", InputKey.S, InputKey.Down);
        Engine.Actions.Bind("Quit", InputKey.Escape);

        Engine.Start(new GameScene());
    });
```

That is the intended experience: define behavior in code, bind input, start the engine, and go.

---

## A more complete example

The following example shows multiple engine systems working together:

* scene setup
* actor spawning
* gravity
* solid collision
* action mapping
* particles
* custom rendering

```csharp
using EngineNext.Core;

public sealed class DemoScene : Scene
{
    private ParticlePrefab _jumpDust = null!;

    public override void OnStart()
    {
        CameraSmoothing = 8f;

        _jumpDust = new ParticlePrefab()
            .Count(10)
            .Lifetime(0.20f, 0.45f)
            .Speed(40f, 120f)
            .Size(4f, 8f, 1f, 2f)
            .Colors(
                EngineColor.FromHex("#E8E8E8"),
                EngineColor.FromHex("#FFFFFF00"))
            .Direction(90f, 90f)
            .Physics(240f, 3f)
            .AsCircle();

        Spawn<Ground>();
        Spawn<Player>();
    }

    public override void OnRenderBackground(RenderList list, SizeI viewport)
    {
        list.DrawText(
            "EngineNext Demo",
            new RectF(20, 20, 300, 30),
            EngineColor.White,
            20f,
            TextAlign.Left);
    }

    public void EmitJumpDust(Vec2 position)
    {
        Particles.Trigger(_jumpDust, position);
    }
}

public sealed class Ground : Actor
{
    public override void Start()
    {
        Name = "Ground";
        Transform.Position = new Vec2(0, 400);
        Size = new Vec2(1200, 64);
        Tint = EngineColor.FromHex("#2F3642");

        Body.Enabled = true;
        Body.IsStatic = true;
        Body.IsSolid = true;
    }
}

public sealed class Player : Actor
{
    public override void Start()
    {
        Name = "Player";
        Transform.Position = new Vec2(120, 120);
        Size = new Vec2(32, 48);
        Tint = EngineColor.FromHex("#64AAFF");

        Body.Enabled = true;
        Body.IsSolid = true;
        Body.UseGravity = true;
    }

    public override void Update(float dt)
    {
        var moveSpeed = 220f;
        var jumpSpeed = -540f;

        if (Engine.Actions.Down("Left"))
            Velocity = new Vec2(-moveSpeed, Velocity.Y);
        else if (Engine.Actions.Down("Right"))
            Velocity = new Vec2(moveSpeed, Velocity.Y);
        else
            Velocity = new Vec2(0, Velocity.Y);

        if (IsGrounded && Engine.Actions.Pressed("Jump"))
        {
            Velocity = new Vec2(Velocity.X, jumpSpeed);

            if (Scene is DemoScene demo)
                demo.EmitJumpDust(new Vec2(Bounds.CenterX, Bounds.Bottom));
        }

        Move(Velocity * dt);

        Scene.CameraPosition = Vec2.Lerp(Scene.CameraPosition, Transform.Position, dt * Scene.CameraSmoothing);

        base.Update(dt);
    }

    public override void Render(RenderList list, SizeI viewport)
    {
        base.Render(list, viewport);

        var labelRect = Scene.WorldToScreenRect(
            new RectF(Bounds.X - 8, Bounds.Y - 22, 80, 18),
            viewport);

        list.DrawText("Player", labelRect, EngineColor.White, 14f, TextAlign.Left);
    }
}
```

Example bindings:

```csharp
Engine.Actions.Bind("Left", InputKey.A, InputKey.Left);
Engine.Actions.Bind("Right", InputKey.D, InputKey.Right);
Engine.Actions.Bind("Jump", InputKey.Space);
Engine.Actions.Bind("Quit", InputKey.Escape);
```

This is a good representation of the engine’s style: compact, explicit, and easy to trace.

---

## How the frame works

A normal frame flows like this:

### 1. Previous transforms are synced

Before the frame advances, the current scene asks actors to sync previous transform state.

### 2. Time advances

`Engine.Time.Advance(deltaSeconds)` updates engine time state.

### 3. UI is updated

The UI system processes open bindings and updates active windows.

### 4. Scene and actor logic runs

If input mode allows gameplay input, the engine runs:

* `Scene.OnUpdate(dt)`
* physics step
* spatial rebuild
* actor updates
* particle updates

### 5. Rendering happens

The render pass clears the screen, renders the scene, actors, and UI.

That loop is small enough to fully understand, which is one of the engine’s biggest strengths.

---

## Using scenes well

Scenes are best used for world-level ownership.

Good responsibilities for a scene:

* spawning actors
* loading the level
* managing the camera
* switching game state
* orchestrating world systems
* rendering background and overlays tied to the world

Avoid stuffing all gameplay directly into the scene. Let the scene coordinate; let actors behave.

### Example

```csharp
public sealed class CombatScene : Scene
{
    public override void OnStart()
    {
        Spawn<Player>();
        Spawn<Enemy>();
        Spawn<Enemy>();
    }

    public override void OnUpdate(float dt)
    {
        if (Engine.Actions.Pressed("Pause"))
            PauseMenu.Open();
    }
}
```

---

## Using actors well

Actors are where most gameplay should live.

Good responsibilities for an actor:

* movement
* reactions
* state transitions
* animation selection
* custom rendering
* triggering particles or sound

### Example

```csharp
public sealed class Coin : Actor
{
    public override void Start()
    {
        Size = new Vec2(16, 16);
        Tint = EngineColor.FromHex("#FFD54A");
    }

    public override void Update(float dt)
    {
        Transform.Rotation += 180f * dt;
        base.Update(dt);
    }
}
```

An actor can stay very small and still be useful.

---

## Components

Actors can own components through the built-in `Component` type.

A component has:

* `Actor`
* `Start()`
* `Update(float dt)`

This is a good place for reusable behavior that should not force deeper inheritance trees.

### Example

```csharp
public sealed class BobbingComponent : Component
{
    private float _time;
    private Vec2 _origin;

    public override void Start()
    {
        _origin = Actor.Transform.Position;
    }

    public override void Update(float dt)
    {
        _time += dt;
        Actor.Transform.Position = _origin + new Vec2(0, MathF.Sin(_time * 4f) * 6f);
    }
}
```

Attach it:

```csharp
public sealed class Pickup : Actor
{
    public override void Start()
    {
        Add(new BobbingComponent());
    }
}
```

---

## Prefabs

`Prefab<TActor>` is the reusable setup mechanism for actors.

Use prefabs when you want to spawn the same actor shape repeatedly with a shared configuration pattern.

### Example

```csharp
public sealed class CratePrefab : Prefab<Actor>
{
    public override void Build(Actor actor)
    {
        actor.Name = "Crate";
        actor.Size = new Vec2(32, 32);
        actor.Tint = EngineColor.FromHex("#8C5E3C");
        actor.Body.Enabled = true;
        actor.Body.IsSolid = true;
        actor.Body.IsStatic = true;
    }
}
```

Spawn it from a scene:

```csharp
Spawn(new CratePrefab());
```

Or if using the generic overload as implemented:

```csharp
Spawn(new CratePrefabTyped());
```

A prefab is one of the cleanest ways to preserve simplicity while scaling repeated content.

---

## Rendering in practice

By default, an actor renders either:

* its `SpritePath`, if one is assigned
* or a filled rounded rectangle using `Tint`

That means every actor is visible immediately, even before art exists. This is excellent for prototyping.

### Example: custom actor rendering

```csharp
public override void Render(RenderList list, SizeI viewport)
{
    var rect = Scene.WorldToScreenRect(Bounds, viewport);

    list.FillRect(rect, EngineColor.FromHex("#222A38"), 8f);
    list.StrokeRect(rect, EngineColor.White, 2f, 8f);
    list.DrawText("NPC", rect, EngineColor.White, 14f, TextAlign.Center);
}
```

You do not need to learn a separate rendering DSL to get meaningful output.

---

## Physics in practice

EngineNext physics favors clarity over simulation complexity.

Typical flow for a dynamic actor:

1. set velocity
2. apply movement with `Move(Velocity * dt)`
3. read collision state such as `IsGrounded` or `IsOnWall`

### Example

```csharp
public override void Update(float dt)
{
    if (!IsGrounded)
        Velocity = new Vec2(Velocity.X, Velocity.Y + 0);

    var result = Move(Velocity * dt);

    if (result.HitBottom)
        Velocity = new Vec2(Velocity.X, 0);

    base.Update(dt);
}
```

Use `Body.IsStatic` and `Body.IsSolid` for world geometry, and `Body.UseGravity` for actors that should fall.

This gives you a compact platformer-friendly model.

---

## Input in practice

The strongest pattern is to bind actions once at startup and then read actions everywhere.

### Bind once

```csharp
Engine.Actions.Bind("MoveLeft", InputKey.A, InputKey.Left);
Engine.Actions.Bind("MoveRight", InputKey.D, InputKey.Right);
Engine.Actions.Bind("Jump", InputKey.Space);
Engine.Actions.Bind("Pause", InputKey.Escape);
```

### Read in gameplay

```csharp
if (Engine.Actions.Down("MoveLeft"))
{
    // move left
}

if (Engine.Actions.Pressed("Jump"))
{
    // jump once
}
```

That separation keeps controls flexible and gameplay code clean.

---

## UI in practice

The UI framework is built around code-defined windows and attributes.

This works especially well for menus and overlays that should stay close to the rest of the codebase.

A good mental model is:

* the scene owns world logic
* the UI owns menu and interface logic
* the engine decides when both are updated based on input mode

### Typical use cases

* title menu
* pause screen
* settings popup
* debug panel
* game over screen

Because the engine supports multiple input modes, it is easy to let UI take over input temporarily.

---

## Particles in practice

Particles are designed to be authored entirely in code.

### Example: impact sparks

```csharp
var sparks = new ParticlePrefab()
    .Count(14)
    .Lifetime(0.08f, 0.25f)
    .Speed(120f, 240f)
    .Size(2f, 4f, 1f, 1f)
    .Colors(
        EngineColor.FromHex("#FFD36B"),
        EngineColor.FromHex("#FF6B0000"))
    .Direction(0f, 360f)
    .Physics(300f, 6f)
    .AsRect();

Particles.Trigger(sparks, hitPoint);
```

This is one of the fastest ways to add feedback and polish to a prototype.

---

## Animation in practice

The animation system is procedural and bone-based.

You define:

* a skeleton
* named clips
* rules for when clips activate
* pose logic as code

### Why this matters

This approach is ideal when:

* you want animation to respond dynamically to gameplay state
* you want very lightweight character rendering
* you want movement driven by math instead of imported timelines

That makes the animation system powerful for experiments, enemies, creatures, and stylized projects.

---

## Sound in practice

The core sound system is intentionally thin.

```csharp
Engine.Sound.Play("Assets/Sounds/jump.wav");
```

At the core level, this emits a playback event. This is a clean separation point for platform-specific audio backends.

The value here is consistency: gameplay code always triggers sound through the same engine service.

---

## Camera and world/screen conversion

Scenes expose camera helpers that make 2D work straightforward:

* `CameraPosition`
* `CameraSmoothing`
* `GetWorldViewportBounds(SizeI viewport)`
* `WorldToScreen(Vec2 world, SizeI viewport)`
* `WorldToScreenRect(RectF rect, SizeI viewport)`

These methods matter because they keep actor logic in world space while rendering stays in screen space.

### Example

```csharp
Scene.CameraPosition = player.Transform.Position;
```

### Example: world label rendering

```csharp
var screenRect = Scene.WorldToScreenRect(actor.Bounds, viewport);
list.DrawText("Enemy", screenRect, EngineColor.White, 12f, TextAlign.Center);
```

---

## Simplicity as a feature

A big part of EngineNext’s value is what it does **not** force you to do.

You do not need:

* a heavy scene editor
* serialized inspector workflows for every feature
* a large asset pipeline just to test a movement idea
* complex renderer setup before you can see objects
* a big gameplay framework before you can write your first actor

That simplicity is not an absence of capability. It is the engine’s core design choice.

Every major system is still there:

* scenes
* actors
* physics
* rendering
* UI
* particles
* animation
* sound
* host platform

They are just presented in a form you can read and control directly.

---

## Suggested project structure

A practical project layout on top of EngineNext could look like this:

```text
MyGame/
├─ Game/
│  ├─ Scenes/
│  │  ├─ BootScene.cs
│  │  ├─ MenuScene.cs
│  │  └─ GameScene.cs
│  ├─ Actors/
│  │  ├─ Player.cs
│  │  ├─ Enemy.cs
│  │  ├─ Projectile.cs
│  │  └─ World/
│  ├─ Components/
│  ├─ UI/
│  ├─ Effects/
│  └─ Animation/
├─ Assets/
│  ├─ Images/
│  └─ Sounds/
└─ Program.cs
```

This complements the engine’s model well.

---

## Current strengths

EngineNext is already especially strong at:

* code-first 2D gameplay
* explicit frame-loop architecture
* lightweight world composition
* collision-driven movement
* procedural visuals
* in-code UI and effects
* rapid prototyping without losing structure

---

## Current technical profile

From this repository, the engine currently includes:

* a single active scene model
* actor lists and scene-managed updates
* viewport-aware render querying
* layer and sort-order actor rendering
* action-based input
* raw keyboard and mouse support
* basic 2D collision and gravity
* particles with configurable prefabs
* procedural skeletal animation
* code-defined UI windows
* Win32 host integration
* software-style draw command rendering
* optional FPS display support

---

## Good fit / not the point

### Good fit

* solo game development
* custom game architecture
* prototypes that may become products
* code-centric teams
* experiments with movement, effects, and procedural systems

### Not the point

EngineNext is not trying to be a giant all-in-one editor-first ecosystem. Its strength is that it stays small, understandable, and easy to shape.

---

## Getting started quickly

1. Create a scene
2. Create one actor
3. Bind a few actions
4. Start the engine with a host
5. Add movement
6. Add collision
7. Add particles or UI only when needed

That order matches how the engine is designed to scale.

---

## Example startup checklist

```csharp
Engine.Actions.Bind("Left", InputKey.A, InputKey.Left);
Engine.Actions.Bind("Right", InputKey.D, InputKey.Right);
Engine.Actions.Bind("Jump", InputKey.Space);
Engine.Actions.Bind("Quit", InputKey.Escape);

Engine.Start(new GameScene());
```

Run it with:

```csharp
var host = new Win32GameHost();
host.Run(new HostOptions
{
    Title = "My Game",
    Width = 1280,
    Height = 720
}, () =>
{
    // bindings + Engine.Start(...)
});
```