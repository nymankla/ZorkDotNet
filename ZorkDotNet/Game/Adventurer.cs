namespace ZorkDotNet.Game;

/// <summary>
/// The player (MDL ADV: AROOM, AOBJS, ASCORE, AVEHICLE, AOBJ, AACTION, ASTRENGTH, AFLAGS).
/// </summary>
public class Adventurer
{
    public Room CurrentRoom { get; set; } = null!;
    /// <summary>What the adventurer carries (MDL AOBJS). Changed only by <see cref="GameObject.MoveTo"/>.</summary>
    public IReadOnlyList<GameObject> Inventory => Items;
    internal List<GameObject> Items { get; } = new();
    public int Score { get; set; }
    public GameObject? Vehicle { get; set; }
    /// <summary>Strength of an unhurt adventurer.</summary>
    public const int FullStrength = 100;

    public int Strength { get; set; } = FullStrength;
    public int Moves { get; set; }
    /// <summary>MDL ASTAGGERED: can't fight this turn.</summary>
    public bool Staggered { get; set; }
    public bool BriefMode { get; set; }
    public bool SuperBriefMode { get; set; }

    public const int MaxLoad = 100; // LOAD-MAX 100 in defs.63
}
