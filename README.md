````markdown
# EngineNext

EngineNext is a lightweight, code-first game engine designed for **clarity, speed, and control**.

Instead of hiding systems behind complex editors or frameworks, EngineNext exposes a **minimal but powerful API** that lets you build games quickly while maintaining full control.

---

## Core Design Principles

- **Code first**
- **Minimal boilerplate**
- **Fast iteration**
- **Composable systems**
- **Clear architecture**

---

## Example

A complete game loop can be written in only a few lines.

```csharp
class GameScene : Scene
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
````

Start the engine:

```csharp
Engine.Start(new GameScene());
```

That's it.

---

## Features

EngineNext includes:

* Scene system
* Actor system
* Physics
* Rendering
* Input actions
* UI framework
* Particle system
* Spatial indexing
* Sound system
* Animation
* Time system

All systems are **fully accessible through code**.

---

## Wiki Contents

| Page                | Description               |
| ------------------- | ------------------------- |
| Getting Started     | Setup and first scene     |
| Engine Architecture | How the engine works      |
| Scenes              | Scene lifecycle           |
| Actors              | Entity system             |
| Rendering           | Rendering pipeline        |
| Physics             | Physics system            |
| Particles           | Particle system           |
| Input               | Input mapping             |
| UI System           | Immediate UI framework    |
| Sound               | Audio system              |
| Animation           | Animation system          |
| Time                | Time management           |
| Spatial Index       | Efficient spatial queries |
| Advanced Patterns   | Best practices            |
| Full Example Game   | Complete working example  |

---

# Getting Started

This guide walks through creating your first EngineNext project.

---

## 1. Install

Add EngineNext to your project.

```bash
dotnet add package EngineNext
```

Or reference the project directly.

---

## 2. Create a Scene

Scenes control the game world.

```csharp
class MyScene : Scene
{
    public override void OnStart()
    {
        Spawn<Player>();
    }

    public override void OnUpdate(float dt)
    {
        // game logic
    }
}
```

---

## 3. Start the Engine

```csharp
Engine.Start(new MyScene());
```

The engine will now run your scene.

---

## 4. Game Loop

The engine internally performs:

```
Tick()
 ├ Time update
 ├ UI update
 ├ Scene update
 ├ Actor updates
 └ Rendering
```

You only implement what you need.

---

## 5. Spawning Actors

Actors represent objects in the world.

```csharp
var player = Spawn<Player>();
```

Actors automatically:

* Update
* Render
* Interact with physics

---

# Engine Architecture

EngineNext is structured around a **small set of core systems**.

```
Engine
├ Scene
│   ├ Actors
│   ├ Physics
│   ├ Particles
│   └ Spatial Index
│
├ Rendering
├ Input
├ UI
├ Sound
└ Time
```

---

## Engine Core

The engine provides global services.

```csharp
Engine.Scene
Engine.UI
Engine.Time
Engine.Actions
Engine.Input
Engine.Sound
```

These services are accessible from anywhere.

---

## Scene Lifecycle

```
Start Scene
   ↓
OnStart()
   ↓
OnUpdate(dt)
   ↓
OnRender()
   ↓
OnEnd()
```

---

## Why This Design?

EngineNext avoids hidden systems and magic.

Everything is:

* predictable
* debuggable
* explicit

---

# Scenes

Scenes define the game world.

They manage:

* actors
* physics
* particles
* camera

---

## Basic Scene

```csharp
class GameScene : Scene
{
    public override void OnStart()
    {
        Spawn<Player>();
    }

    public override void OnUpdate(float dt)
    {
    }
}
```

---

## Camera

Scenes control the camera.

```csharp
CameraPosition = player.Position;
```

Camera smoothing:

```csharp
CameraSmoothing = 10f;
```

---

## Rendering Hooks

```csharp
public override void OnRenderBackground(RenderList list, SizeI viewport)
{
}

public override void OnRender(RenderList list, SizeI viewport)
{
}
```

These allow full control over rendering order.

---

# Actors

Actors are the core gameplay objects.

Examples:

* player
* enemies
* projectiles
* world objects

---

## Creating an Actor

```csharp
class Player : Actor
{
    public override void OnStart()
    {
    }

    public override void OnUpdate(float dt)
    {
    }
}
```

---

## Transform

Actors include transform data.

```
Position
Rotation
Scale
Velocity
```

---

## Spawning Actors

Actors are spawned through scenes.

```csharp
Spawn<Player>();
```

---

## Prefabs

Prefabs allow reusable configurations.

```csharp
Spawn(playerPrefab);
```

---

## Actor Update Order

Actors automatically update each frame.

```
Scene.OnUpdate()
Actor.Update()
Physics
Rendering
```

---

# Rendering

Rendering is performed through the `RenderList`.

This allows batching and efficient drawing.

---

## Drawing Primitives

```csharp
list.DrawRect(position, size, color);
```

---

## Custom Rendering

```csharp
public override void OnRender(RenderList list)
{
    list.DrawSprite(texture, Position);
}
```

---

## Render Settings

```csharp
Engine.RenderSettings.VSync = true;
```

---

# Input

EngineNext uses an **action mapping system**.

This decouples input from gameplay.

---

## Defining Actions

```csharp
Engine.Actions.Bind("Jump", Key.Space);
Engine.Actions.Bind("Shoot", Mouse.Left);
```

---

## Reading Input

```csharp
if (Engine.Actions.Pressed("Jump"))
{
    Jump();
}
```

---

## Input Modes

```
GameOnly
UIOnly
GameAndUI
```

Example:

```csharp
Engine.InputMode = InputMode.GameOnly;
```

---

# Physics

Each scene includes a physics world.

```
Scene.Physics
```

---

## Physics Bodies

Actors can attach physics bodies.

```csharp
var body = Physics.CreateBody();
```

---

## Collision

```csharp
Physics.Raycast(origin, direction);
```

---

# Particles

Scenes contain a particle system.

```
Scene.Particles
```

---

## Spawning Particles

```csharp
Particles.Emit(
    position,
    velocity,
    lifetime
);
```

Particles are ideal for:

* explosions
* smoke
* sparks
* magic

---

# UI System

EngineNext includes a lightweight UI framework.

```
Engine.UI
```

---

## Creating UI

```csharp
UI.Button("Play", () =>
{
    StartGame();
});
```

---

## Binding

```csharp
UI.Slider("Volume", ref volume, 0, 1);
```

---

## Advantages

The UI system is:

* immediate
* reactive
* minimal

---

# Sound

EngineNext includes a sound system.

```
Engine.Sound
```

---

## Playing Sounds

```csharp
Engine.Sound.Play("laser");
```

---

## Looping

```csharp
Engine.Sound.Loop("music");
```

---

# Time

EngineNext provides a global time system.

```
Engine.Time
```

---

## Delta Time

Every update receives delta time.

```csharp
public override void OnUpdate(float dt)
```

---

## Time Scaling

```csharp
Engine.Time.Scale = 0.5f;
```

Useful for:

* slow motion
* pause systems

---

# Spatial Index

Scenes include a spatial index for efficient queries.

```
Scene.SpatialIndex
```

This allows fast lookups of nearby actors.

---

## Example

```csharp
var nearby = SpatialIndex.Query(radius);
```

Used for:

* AI
* collision
* gameplay queries

---

# Complete Example

A minimal playable game.

---

## Player

```csharp
class Player : Actor
{
    public override void OnUpdate(float dt)
    {
        if (Engine.Actions.Down("Left"))
            Position.X -= 5 * dt;

        if (Engine.Actions.Down("Right"))
            Position.X += 5 * dt;
    }
}
```

---

## Scene

```csharp
class GameScene : Scene
{
    public override void OnStart()
    {
        Spawn<Player>();
    }
}
```

---

## Start

```csharp
Engine.Start(new GameScene());
```

You now have a working game.

```
```
