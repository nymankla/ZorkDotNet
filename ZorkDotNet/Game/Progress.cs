namespace ZorkDotNet.Game;

/// <summary>
/// What the game keeps track of besides the world itself: counters and the state of the clocks, demons and puzzles.
/// A save holds it whole (<see cref="GameSave"/>), so a property added here, or in one of its parts, is saved and
/// restored with no further work. Room and object ids kept here must be checked in <c>GameSave.Validate</c>.
/// </summary>
public sealed class Progress
{
    /// <summary>The thief's demon (act1.38 ROBBER).</summary>
    public RobberState Robber { get; set; } = new();

    /// <summary>The lamp, the candles and the matches.</summary>
    public LightState Lights { get; set; } = new();

    /// <summary>MDL HS: how many times the player has said HELLO (act1.38 HELLO).</summary>
    public int HelloCount { get; set; }

    /// <summary>MDL DEATHS: how many times the player has died.</summary>
    public int Deaths { get; set; }

    /// <summary>MDL BLOC: the room the balloon is in, or heading for (act2.27).</summary>
    public string BalloonLocation { get; set; } = RoomId.Vlbot;

    /// <summary>act2.27 LIGHT-SHAFT: the points for lighting the bottom of the shaft have been given.</summary>
    public bool LightShaftScored { get; set; }

    /// <summary>Per-object counters kept in MDL slots such as OTVAL and ORAND (the sword's glow, the leaves).</summary>
    public Dictionary<string, int> ObjectState { get; set; } = new();

    /// <summary>Villain health in the port's melee (the troll takes 2 hits).</summary>
    public Dictionary<string, int> VillainStrength { get; set; } = new();
}

/// <summary>The thief's demon (act1.38 ROBBER and its HACK).</summary>
public sealed class RobberState
{
    /// <summary>The room he works in (HROOM).</summary>
    public string Room { get; set; } = Thief.Rooms[0];

    /// <summary>The index in <see cref="Thief.Rooms"/> of the next room he goes to (HROOMS).</summary>
    public int Next { get; set; }

    /// <summary>His bag (HOBJS), most recent first.</summary>
    public List<string> Bag { get; set; } = new();

    /// <summary>The demon still runs (HACTION); false once he is dead.</summary>
    public bool Active { get; set; } = true;

    /// <summary>He has shown himself in this room (HFLAG).</summary>
    public bool Announced { get; set; }
}

/// <summary>The lamp, the candles and the matchbook.</summary>
public sealed class LightState
{
    /// <summary>Lamp clock (LNTIN): moves while lit until the next LIGHT-INT step. Starts at 350 (dung.56).</summary>
    public int LampTicks { get; set; } = GameState.LampFirstInterval;

    /// <summary>Lamp clock: LIGHT-INT steps taken so far (MDL ORAND count), indexes LAMP-TICKS / LAMP-TELLS.</summary>
    public int LampStage { get; set; }

    /// <summary>Candle clock (CNDIN): moves left until the next LIGHT-INT step; 0 until the candles are first touched.</summary>
    public int CandleTicks { get; set; }

    /// <summary>Candle clock: LIGHT-INT steps taken so far (the count in the candles' ORAND).</summary>
    public int CandleStage { get; set; }

    /// <summary>The matchbook's ORAND: matches left (dung.56 starts it at 5; the last one never lights).</summary>
    public int MatchesLeft { get; set; } = 5;
}
