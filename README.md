
# EngineNext

EngineNext is a **network‑first game engine** built for **rapid gameplay prototyping and clean gameplay code**.

The engine focuses on a simple goal:

> Make it extremely easy to turn gameplay ideas into working systems.

EngineNext keeps its architecture intentionally small and understandable so developers can focus on building games instead of fighting complex engine systems.

---

# Why EngineNext Exists

Many game engines become difficult to work with over time due to:

- heavy editor pipelines
- complex asset graphs
- networking added later as an afterthought
- hidden engine behavior

EngineNext takes a different approach.

It focuses on:

- rapid gameplay iteration
- clear architecture
- built‑in multiplayer awareness
- minimal boilerplate
- flexible networking that is not tied to one transport

---

# Core Architecture

EngineNext revolves around a few simple concepts.

## Scene

A Scene owns the world and manages:

- actors
- physics
- particles
- rendering
- camera
- network state

Example:

```csharp
public sealed class DemoScene : Scene
{
    public override void OnBegin()
    {
        PixelsPerUnit = 32;

        Spawn(new PlayerBlueprint
        {
            SpawnPosition = BlockVector2.FromDouble(2,2)
        });

        Spawn(new CoinBlueprint
        {
            Position = BlockVector2.FromDouble(10,5)
        });
    }
}
```

---

## Actor

Actors represent objects in the world.

Actors contain:

- gameplay logic
- physics bodies
- networking behavior
- transforms
- optional custom rendering

Example:

```csharp
public sealed class Coin : Actor
{
    public override void OnTriggerEnter(TriggerInfo info)
    {
        if (info.Other is Player)
            Scene!.Destroy(this);
    }
}
```

Notice the actor contains **no rendering code**.

---

## Blueprint (Prefab)

Blueprints define how actors are created.

They control:

- visual assets
- physics setup
- spawn configuration
- networking mode

Example:

```csharp
public sealed class CoinBlueprint : Blueprint<Coin>
{
    public BlockVector2 Position { get; set; }

    protected override Coin Create(Scene scene)
    {
        return new Coin();
    }

    protected override void Initialize(Scene scene, Coin actor)
    {
        actor.Transform.Position = Position;

        actor.Visual.Asset = Assets.Sprite("assets/coin.png");
        actor.Visual.Size = new Vec2(24,24);
        actor.Visual.Anchor = VisualAnchor.Center;

        actor.Body.AddCircle(0,0,0.35,true);
    }
}
```

Blueprints keep gameplay logic separate from spawn configuration.

---

# Automatic Rendering

EngineNext supports **automatic rendering**.

If a blueprint assigns a visual asset:

```csharp
actor.Visual.Asset = Assets.Sprite("coin.png");
```

the engine renders the actor automatically.

Most actors therefore do **not need to override Render()**.

---

# Custom Rendering

Actors can still override rendering when needed.

Example:

```csharp
public sealed class Boss : Actor
{
    public override void Render(RenderList list, SizeI viewport)
    {
        DefaultActorRenderer.Instance.Render(this, list, viewport);

        var bounds = GetScreenVisualBounds(viewport, Visual.Size, Visual.Anchor);

        list.StrokeRect(bounds, new EngineColor(255,0,0,120), 3f);
    }
}
```

This allows special visual effects while still using the default sprite rendering.

---

# Reusable Renderers

For more advanced visuals, reusable renderers can be attached.

Example animated sprite:

```csharp
actor.Renderer = new AnimatedSpriteRenderer(
    texture,
    frameWidth: 32,
    frameHeight: 32,
    frameCount: 4,
    fps: 8
);
```

This keeps actor classes focused on gameplay instead of graphics code.

---

# Physics

Physics bodies are defined directly on actors.

Example:

```csharp
actor.Body.AddBox(-0.5,-0.5,1,1,false);
actor.Body.AddCircle(0,0,0.35,true);
```

Movement is handled with:

```csharp
Scene.Physics.StepMotion(actor, intent);
```

The physics system is designed for gameplay use rather than simulation complexity.

---

# Particle Systems

Particles provide fast gameplay feedback.

Define a preset:

```csharp
var burst = new ParticlePrefab()
    .Count(14)
    .Lifetime(0.2f,0.5f)
    .Speed(40f,120f)
    .Size(8f,12f,1f,2f)
    .Colors(
        new EngineColor(255,220,90,255),
        new EngineColor(255,180,50,0))
    .AsCircle();
```

Trigger the effect:

```csharp
Scene.Particles.Trigger(burst, position);
```

---

# Network‑First Design

EngineNext was built with multiplayer in mind.

Actors support multiple network modes:

- Authority
- Predicted
- RemoteInterpolated

Example:

```csharp
if (Engine.IsAuthority)
    Scene.Physics.StepMotion(this, move, false);
else if (NetworkMode == ActorNetworkMode.Predicted)
    Scene.Physics.StepMotion(this, move, true);
```

---

# Transport‑Agnostic Networking

EngineNext does not require a specific networking library.

Scenes produce world commits that can be sent over any transport.

Example server:

```csharp
while(scene.Network.TryDequeue(out var commit))
{
    byte[] packet = Serialize(commit);
    transport.Broadcast(packet);
}
```

Example client:

```csharp
scene.ApplyWorldCommit(commit);
```

This allows developers to use:

- UDP
- ENet
- Steam Networking
- custom protocols
- relay servers

---

# Rapid Prototyping

Most gameplay features require only:

- an actor
- a blueprint
- optional particles
- optional UI

This makes EngineNext excellent for experimentation and gameplay iteration.

---

# Example Project Structure

```
Game/
 ├─ Actors/
 │   ├─ Player.cs
 │   ├─ Coin.cs
 │   └─ Enemy.cs
 │
 ├─ Blueprints/
 │   ├─ PlayerBlueprint.cs
 │   ├─ CoinBlueprint.cs
 │   └─ EnemyBlueprint.cs
 │
 ├─ Scenes/
 │   └─ DemoScene.cs
 │
 └─ Assets/
     ├─ sprites/
     └─ ui/
```

---

# Vision

EngineNext aims to be one of the **most developer‑friendly game engines ever built**.

The engine prioritizes:

- readable gameplay code
- multiplayer‑ready architecture
- rapid iteration
- minimal boilerplate
- extensibility without complexity

The goal is simple:

**ideas should become playable systems quickly.**
