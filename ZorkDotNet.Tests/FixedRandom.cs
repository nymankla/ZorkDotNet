namespace ZorkDotNet.Tests;

/// <summary>A Random that always gives the same result, so chance events (GameState.Random) are predictable.</summary>
public sealed class FixedRandom(int percent) : Random
{
    public override int Next(int maxValue) => Math.Min(percent, maxValue - 1);
    public override int Next(int minValue, int maxValue) => Math.Clamp(percent, minValue, maxValue - 1);
    public override int Next() => percent;
    public override double NextDouble() => percent / 100.0;
}
