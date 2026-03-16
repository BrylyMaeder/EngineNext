namespace EngineNext.Core;

public enum EngineAuthorityMode
{
    Standalone = 0,
    Server = 1,
    Client = 2
}

public enum ActorNetworkMode
{
    None = 0,
    Authority = 1,
    Predicted = 2,
    RemoteInterpolated = 3
}

public readonly struct MotionCommit
{
    public int Tick { get; init; }
    public int NetworkActorId { get; init; }
    public BlockVector2 Position { get; init; }
    public BlockVector2 Rotation { get; init; }
    public BlockVector2 Scale { get; init; }
}

public readonly struct ActorIntent
{
    public readonly int Tick;
    public readonly int NetworkActorId;
    public readonly sbyte MoveX;
    public readonly sbyte MoveY;
    public readonly byte Buttons;

    public ActorIntent(int tick, int networkActorId, sbyte moveX, sbyte moveY, byte buttons)
    {
        Tick = tick;
        NetworkActorId = networkActorId;
        MoveX = moveX;
        MoveY = moveY;
        Buttons = buttons;
    }
}

public sealed class BufferedIntent
{
    public int Tick { get; set; }
    public MotionIntent Intent { get; set; }
    public MotionResult Predicted { get; set; }
}

public sealed class PredictionBuffer
{
    private readonly List<BufferedIntent> _items = new(64);
    public IReadOnlyList<BufferedIntent> Items => _items;
    public void Add(BufferedIntent item) => _items.Add(item);
    public void DiscardUpTo(int tick) => _items.RemoveAll(x => x.Tick <= tick);

    public BufferedIntent? Find(int tick)
    {
        for (int i = 0; i < _items.Count; i++) if (_items[i].Tick == tick) return _items[i];
        return null;
    }

    public List<BufferedIntent> GetAfter(int tick)
    {
        var result = new List<BufferedIntent>();
        for (int i = 0; i < _items.Count; i++) if (_items[i].Tick > tick) result.Add(_items[i]);
        return result;
    }
}

public static class Reconciler
{
    public static void Reconcile(Actor actor, MotionCommit authoritative)
    {
        if (actor.Scene == null) return;
        var predicted = actor.Prediction.Find(authoritative.Tick);
        bool exactMatch = predicted is not null && predicted.Predicted.ResolvedEnd == authoritative.Position;

        actor.Transform.Position = authoritative.Position;
        actor.Transform.Rotation = authoritative.Rotation;
        actor.Transform.Scale = authoritative.Scale;

        if (exactMatch)
        {
            actor.Prediction.DiscardUpTo(authoritative.Tick);
            return;
        }

        var later = actor.Prediction.GetAfter(authoritative.Tick);
        actor.Prediction.DiscardUpTo(authoritative.Tick);

        for (int i = 0; i < later.Count; i++)
        {
            var replay = later[i];
            MotionResult result = actor.Scene.Physics.StepMotion(actor, replay.Intent, false);
            actor.Prediction.Add(new BufferedIntent
            {
                Tick = replay.Tick,
                Intent = replay.Intent,
                Predicted = result
            });
        }
    }
}

public sealed class SceneNetworkState
{
    private WorldCommit? _currentCommit;
    private readonly Queue<WorldCommit> _outgoing = new();
    public IReadOnlyCollection<WorldCommit> Outgoing => _outgoing;

    public void Reset()
    {
        _currentCommit = null;
        _outgoing.Clear();
    }

    public void BeginTick(int tick)
    {
        _currentCommit = new WorldCommit(tick);
    }

    public void EndTick()
    {
        if (_currentCommit == null) return;
        if (_currentCommit.HasAny) _outgoing.Enqueue(_currentCommit);
        _currentCommit = null;
    }

    public bool TryDequeue(out WorldCommit? commit)
    {
        if (_outgoing.Count > 0)
        {
            commit = _outgoing.Dequeue();
            return true;
        }
        commit = null;
        return false;
    }

    public void RecordSpawn(Actor actor)
    {
        if (_currentCommit == null) return;
        _currentCommit.SceneName = actor.Scene?.SceneName ?? string.Empty;
        _currentCommit.Spawns.Add(new WorldSpawn
        {
            NetworkActorId = actor.NetworkActorId,
            Blueprint = actor.SourceBlueprint,
            Name = actor.Name,
            Position = actor.Transform.Position,
            Rotation = actor.Transform.Rotation,
            Scale = actor.Transform.Scale
        });
    }

    public void RecordDestroy(Actor actor)
    {
        if (_currentCommit == null) return;
        _currentCommit.SceneName = actor.Scene?.SceneName ?? string.Empty;
        _currentCommit.Destroys.Add(actor.NetworkActorId);
    }

    public void RecordEnable(Actor actor, bool enabled)
    {
        if (_currentCommit == null) return;
        _currentCommit.SceneName = actor.Scene?.SceneName ?? string.Empty;
        if (enabled) _currentCommit.Enables.Add(actor.NetworkActorId);
        else _currentCommit.Disables.Add(actor.NetworkActorId);
    }

    public void RecordTransform(Actor actor)
    {
        if (_currentCommit == null) return;
        _currentCommit.SceneName = actor.Scene?.SceneName ?? string.Empty;
        _currentCommit.Transforms.Add(new WorldTransform
        {
            Tick = _currentCommit.Tick,
            NetworkActorId = actor.NetworkActorId,
            Position = actor.Transform.Position,
            Rotation = actor.Transform.Rotation,
            Scale = actor.Transform.Scale
        });
    }

    public void RecordState(Actor actor, string key, string value)
    {
        if (_currentCommit == null) return;
        _currentCommit.SceneName = actor.Scene?.SceneName ?? string.Empty;
        _currentCommit.States.Add(new WorldStatePatch
        {
            NetworkActorId = actor.NetworkActorId,
            Key = key,
            Value = value
        });
    }
}

public sealed class WorldCommit
{
    public WorldCommit(int tick)
    {
        Tick = tick;
    }

    public int Tick { get; }
    public string SceneName { get; set; } = string.Empty;
    public List<WorldSpawn> Spawns { get; } = new(4);
    public List<int> Destroys { get; } = new(4);
    public List<int> Enables { get; } = new(4);
    public List<int> Disables { get; } = new(4);
    public List<WorldTransform> Transforms { get; } = new(8);
    public List<WorldStatePatch> States { get; } = new(8);
    public bool HasAny => Spawns.Count > 0 || Destroys.Count > 0 || Enables.Count > 0 || Disables.Count > 0 || Transforms.Count > 0 || States.Count > 0;
}

public sealed class WorldSpawn
{
    public int NetworkActorId { get; set; }
    public Blueprint? Blueprint { get; set; }
    public string Name { get; set; } = string.Empty;
    public BlockVector2 Position { get; set; }
    public BlockVector2 Rotation { get; set; }
    public BlockVector2 Scale { get; set; }
}

public sealed class WorldTransform
{
    public int Tick { get; set; }
    public int NetworkActorId { get; set; }
    public BlockVector2 Position { get; set; }
    public BlockVector2 Rotation { get; set; }
    public BlockVector2 Scale { get; set; }
}

public sealed class WorldStatePatch
{
    public int NetworkActorId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
