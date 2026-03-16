using System.Globalization;
using System.Reflection;

namespace EngineNext.Core;

internal enum SpatialBucketKind
{
    Collision = 1,
    Render = 2,
}

public sealed class SpatialCellRecord
{
    public long Id { get; set; }
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int Kind { get; set; }
    public string ActorIds { get; set; } = string.Empty;
}

internal sealed class SpatialIndex2D
{
    private readonly Dictionary<(int X, int Y), List<Actor>> _dynamicCollisionBuckets = new();
    private readonly Dictionary<(int X, int Y), List<Actor>> _dynamicRenderBuckets = new();
    private readonly HashSet<Actor> _seen = new();
    private readonly List<Actor> _queryBuffer = new(128);
    private readonly IStaticCellStore _staticStore;
    private IReadOnlyDictionary<long, Actor> _actorById = new Dictionary<long, Actor>();

    public SpatialIndex2D(int cellSize)
    {
        CellSize = Math.Max(16, cellSize);
        _staticStore = StaticCellStoreFactory.Create();
    }

    public int CellSize { get; }

    public void Rebuild(IEnumerable<Actor> actors)
    {
        var actorById = new Dictionary<long, Actor>();
        var staticCollision = new Dictionary<(int X, int Y), List<long>>();
        var staticRender = new Dictionary<(int X, int Y), List<long>>();

        _dynamicCollisionBuckets.Clear();
        _dynamicRenderBuckets.Clear();

        foreach (var actor in actors)
        {
            if (actor is null || !actor.Enabled)
                continue;

            actorById[actor.SpatialId] = actor;
            if (!actor.ParticipatesInSpatialQueries)
                continue;

            var isStatic = actor.IsSpatiallyStatic || actor.Body.IsStatic;
            var cells = GetCoveredCells(actor.Bounds);
            if (isStatic)
            {
                if (actor.Body.Enabled && actor.Body.IsSolid)
                    AddStaticCells(staticCollision, cells, actor.SpatialId);
                AddStaticCells(staticRender, cells, actor.SpatialId);
            }
            else
            {
                if (actor.Body.Enabled && actor.Body.IsSolid)
                    AddDynamicCells(_dynamicCollisionBuckets, cells, actor);
                AddDynamicCells(_dynamicRenderBuckets, cells, actor);
            }
        }

        _actorById = actorById;
        _staticStore.Replace(staticCollision, staticRender);
    }

    public IEnumerable<Actor> QueryCollision(RectF bounds, Actor? exclude = null)
        => Query(bounds, SpatialBucketKind.Collision, exclude);

    public IEnumerable<Actor> QueryRender(RectF bounds)
        => Query(bounds, SpatialBucketKind.Render, exclude: null);

    private IEnumerable<Actor> Query(RectF bounds, SpatialBucketKind kind, Actor? exclude)
    {
        _queryBuffer.Clear();
        _seen.Clear();
        foreach (var cell in GetCoveredCells(bounds.Inflate(2f)))
        {
            foreach (var actorId in _staticStore.GetActorIds(cell.X, cell.Y, kind))
            {
                if (!_actorById.TryGetValue(actorId, out var actor))
                    continue;
                if (ReferenceEquals(actor, exclude))
                    continue;
                if (!_seen.Add(actor))
                    continue;
                _queryBuffer.Add(actor);
            }

            var dynamicBuckets = kind == SpatialBucketKind.Collision ? _dynamicCollisionBuckets : _dynamicRenderBuckets;
            if (!dynamicBuckets.TryGetValue(cell, out var list))
                continue;
            for (var i = 0; i < list.Count; i++)
            {
                var actor = list[i];
                if (ReferenceEquals(actor, exclude))
                    continue;
                if (!_seen.Add(actor))
                    continue;
                _queryBuffer.Add(actor);
            }
        }

        return _queryBuffer;
    }

    private void AddStaticCells(Dictionary<(int X, int Y), List<long>> target, List<(int X, int Y)> cells, long actorId)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (!target.TryGetValue(cell, out var list))
            {
                list = new List<long>(4);
                target[cell] = list;
            }
            list.Add(actorId);
        }
    }

    private static void AddDynamicCells(Dictionary<(int X, int Y), List<Actor>> target, List<(int X, int Y)> cells, Actor actor)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (!target.TryGetValue(cell, out var list))
            {
                list = new List<Actor>(4);
                target[cell] = list;
            }
            list.Add(actor);
        }
    }

    private List<(int X, int Y)> GetCoveredCells(RectF rect)
    {
        var minX = (int)MathF.Floor(rect.Left / CellSize);
        var maxX = (int)MathF.Floor((rect.Right - 0.001f) / CellSize);
        var minY = (int)MathF.Floor(rect.Top / CellSize);
        var maxY = (int)MathF.Floor((rect.Bottom - 0.001f) / CellSize);
        var cells = new List<(int X, int Y)>((maxX - minX + 1) * (maxY - minY + 1));
        for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
                cells.Add((x, y));
        return cells;
    }
}

internal interface IStaticCellStore
{
    void Replace(Dictionary<(int X, int Y), List<long>> collisionBuckets, Dictionary<(int X, int Y), List<long>> renderBuckets);
    IReadOnlyList<long> GetActorIds(int cellX, int cellY, SpatialBucketKind kind);
}

internal static class StaticCellStoreFactory
{
    public static IStaticCellStore Create()
    {
        try
        {
            return new Memory2StaticCellStore();
        }
        catch
        {
            return new InMemoryStaticCellStore();
        }
    }
}

internal sealed class InMemoryStaticCellStore : IStaticCellStore
{
    private static readonly IReadOnlyList<long> Empty = Array.Empty<long>();
    private readonly Dictionary<long, long[]> _cells = new();

    public void Replace(Dictionary<(int X, int Y), List<long>> collisionBuckets, Dictionary<(int X, int Y), List<long>> renderBuckets)
    {
        _cells.Clear();
        CopyBuckets(collisionBuckets, SpatialBucketKind.Collision);
        CopyBuckets(renderBuckets, SpatialBucketKind.Render);
    }

    public IReadOnlyList<long> GetActorIds(int cellX, int cellY, SpatialBucketKind kind)
        => _cells.TryGetValue(PackCellId(cellX, cellY, kind), out var ids) ? ids : Empty;

    private void CopyBuckets(Dictionary<(int X, int Y), List<long>> source, SpatialBucketKind kind)
    {
        foreach (var pair in source)
            _cells[PackCellId(pair.Key.X, pair.Key.Y, kind)] = pair.Value.Distinct().ToArray();
    }

    internal static long PackCellId(int cellX, int cellY, SpatialBucketKind kind)
    {
        unchecked
        {
            const long offset = 1L << 20;
            var x = ((long)cellX + offset) & 0x1FFFFF;
            var y = ((long)cellY + offset) & 0x1FFFFF;
            return ((long)kind << 42) | (x << 21) | y;
        }
    }
}

internal sealed class Memory2StaticCellStore : IStaticCellStore
{
    private static readonly IReadOnlyList<long> Empty = Array.Empty<long>();
    private readonly object _collection;
    private readonly MethodInfo _upsertMethod;
    private readonly MethodInfo _getMethod;
    private readonly HashSet<long> _knownCellIds = new();

    public Memory2StaticCellStore()
    {
        var memoryDbType = Type.GetType("Memory2.MemoryDb, Memory2", throwOnError: false)
            ?? throw new InvalidOperationException("Memory2.MemoryDb not found.");
        var createMethod = memoryDbType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Memory2.MemoryDb.Create not found.");
        var db = createMethod.Invoke(null, new object[] { 10_000_000L, 4096, 8192 })
            ?? throw new InvalidOperationException("Memory2.MemoryDb.Create returned null.");
        var forMethod = memoryDbType.GetMethod("For", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Memory2.MemoryDb.For not found.");
        _collection = forMethod.MakeGenericMethod(typeof(SpatialCellRecord)).Invoke(db, new object?[] { null })
            ?? throw new InvalidOperationException("Memory2 collection creation failed.");
        var collectionType = _collection.GetType();
        _upsertMethod = collectionType.GetMethod("Upsert", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Memory2 collection Upsert not found.");
        _getMethod = collectionType.GetMethod("Get", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Memory2 collection Get not found.");
    }

    public void Replace(Dictionary<(int X, int Y), List<long>> collisionBuckets, Dictionary<(int X, int Y), List<long>> renderBuckets)
    {
        var nextIds = new HashSet<long>();
        WriteBuckets(collisionBuckets, SpatialBucketKind.Collision, nextIds);
        WriteBuckets(renderBuckets, SpatialBucketKind.Render, nextIds);

        foreach (var staleId in _knownCellIds.Except(nextIds).ToArray())
        {
            var record = new SpatialCellRecord { Id = staleId, ActorIds = string.Empty };
            _upsertMethod.Invoke(_collection, new object[] { record });
        }

        _knownCellIds.Clear();
        foreach (var id in nextIds)
            _knownCellIds.Add(id);
    }

    public IReadOnlyList<long> GetActorIds(int cellX, int cellY, SpatialBucketKind kind)
    {
        var id = InMemoryStaticCellStore.PackCellId(cellX, cellY, kind);
        if (!_knownCellIds.Contains(id))
            return Empty;

        var record = _getMethod.Invoke(_collection, new object[] { id }) as SpatialCellRecord;
        if (record is null || string.IsNullOrWhiteSpace(record.ActorIds))
            return Empty;

        var parts = record.ActorIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return Empty;

        var ids = new List<long>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            if (long.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                ids.Add(value);
        }
        return ids;
    }

    private void WriteBuckets(Dictionary<(int X, int Y), List<long>> buckets, SpatialBucketKind kind, HashSet<long> nextIds)
    {
        foreach (var pair in buckets)
        {
            var ids = pair.Value.Distinct().OrderBy(static x => x).ToArray();
            var cellId = InMemoryStaticCellStore.PackCellId(pair.Key.X, pair.Key.Y, kind);
            nextIds.Add(cellId);
            var record = new SpatialCellRecord
            {
                Id = cellId,
                CellX = pair.Key.X,
                CellY = pair.Key.Y,
                Kind = (int)kind,
                ActorIds = string.Join(',', ids),
            };
            _upsertMethod.Invoke(_collection, new object[] { record });
        }
    }
}