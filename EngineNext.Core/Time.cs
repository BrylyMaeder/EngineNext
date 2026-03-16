namespace EngineNext.Core;

public sealed class TimeState
{
    public float DeltaSeconds { get; private set; }
    public double TotalSeconds { get; private set; }

    internal void Advance(float deltaSeconds)
    {
        DeltaSeconds = deltaSeconds;
        TotalSeconds += deltaSeconds;
    }
}
