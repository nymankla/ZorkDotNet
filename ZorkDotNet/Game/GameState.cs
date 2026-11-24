namespace ZorkDotNet.Game;

/// <summary>
/// A clock event (MDL CEVENT): <see cref="Ticks"/> (CTICK) counts down one each move while <see cref="Enabled"/>
/// (CFLAG); at 0 its routine runs and the event is gone until it is set again (CLOCK-INT). It is data, not a
/// delegate, so SAVE can store it: <see cref="Name"/> picks the routine in <see cref="Clocks"/>, and
/// <see cref="Argument"/> is its input.
/// </summary>
public sealed class ClockEvent
{
    public required string Name { get; init; }
    public int Ticks { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Argument { get; set; }
}

/// <summary>
/// Global game state (MDL: WINNER, HERE, PRSVEC, MOVES, flags, etc.).
/// </summary>
public class GameState
{
    public Adventurer Winner { get; } = new();

    /// <summary>Counters and the state of clocks, demons and puzzles; saved whole (see <see cref="Game.Progress"/>).</summary>
    public Progress Progress { get; set; } = new();
    /// <summary>The thief's demon (MDL ROBBER).</summary>
    public RobberState Robber => Progress.Robber;
    /// <summary>The lamp, the candles and the matches.</summary>
    public LightState Lights => Progress.Lights;
    /// <summary>Per-object counters (MDL OTVAL / ORAND slots).</summary>
    public Dictionary<string, int> ObjectState => Progress.ObjectState;
    /// <summary>Villain health in the port's melee.</summary>
    public Dictionary<string, int> VillainStrength => Progress.VillainStrength;
    public Room Here => Winner.CurrentRoom;
    public int Moves => Winner.Moves;
    public Dictionary<string, bool> Flags { get; } = new();
    public List<Room> Rooms { get; } = new();
    public List<GameObject> AllObjects { get; } = new();
    public bool Running { get; set; } = true;
    /// <summary>Real-time clock for TIME (rooms.99 PLAY-TIME); tests can replace it.</summary>
    public TimeProvider Clock { get; set; } = TimeProvider.System;
    /// <summary>When this game started, for TIME.</summary>
    public DateTimeOffset StartedAt { get; set; } = TimeProvider.System.GetUtcNow();


    /// <summary>The robot as an adventurer (its ADV in dung.56): its room and what it carries.</summary>
    public Adventurer Robot { get; } = new();
    /// <summary>The robot is carrying out a TELL ROBOT command (MDL WINNER is the robot).</summary>
    public bool RobotActing { get; set; }
    /// <summary>
    /// act1.38 ECHO-ROOM: the Loud Room is drowning out everything; each line is echoed until you walk out or say
    /// ECHO. Never true across a SAVE (SAVE would be echoed too).
    /// </summary>
    public bool EchoListening { get; set; }
    /// <summary>The objects the Loud Room hid while listening (MDL ECHO-ROOM-BIT).</summary>
    public List<string> EchoHidden { get; } = new();

    /// <summary>JIGS-UP asked "Do you want me to try to patch you?"; the next input line is the answer.</summary>
    public bool AwaitingPatchAnswer { get; set; }
    /// <summary>The player died during a command, so that turn's clocks run after being patched up.</summary>
    public bool ClocksAfterPatch { get; set; }
    /// <summary>The next input line answers a question rather than being a command (no prompt is shown).</summary>
    public bool AwaitingAnswer => AwaitingQuitConfirmation || AwaitingPatchAnswer || EchoListening;
    /// <summary>Random numbers for chance events such as the grue (MDL PROB); tests can replace it.</summary>
    public Random Random { get; set; } = Random.Shared;

    /// <summary>Set by <see cref="ObjectActions.FallThrough"/> while an object action runs.</summary>
    internal bool ActionFellThrough { get; set; }

    /// <summary>MDL JIGS-UP: the player dies (see <see cref="Death"/>). The message may already have been told.</summary>
    public void JigsUp(string? message) => Death.JigsUp(this, message);
    public string? LastVerb { get; set; }
    public GameObject? LastDirectObject { get; set; }
    public GameObject? LastIndirectObject { get; set; }
    /// <summary>Orphan handling: after "Which X?" we store pending verb and slot so next input can disambiguate.</summary>
    public string? PendingVerb { get; set; }
    public bool PendingNeedsDirect { get; set; }
    public bool PendingNeedsIndirect { get; set; }
    public string? PendingAmbiguousWord { get; set; }
    /// <summary>When ResolveObject finds multiple matches (GWIM-style), list of candidates for "Which X - the A, the B, or the C?".</summary>
    public List<GameObject>? PendingAmbiguousCandidates { get; set; }
    public string[]? PendingWords { get; set; }

    /// <summary>dung.56: &lt;CLOCK-INT ,LNTIN 350&gt;.</summary>
    public const int LampFirstInterval = 350;
    /// <summary>act1.38 LAMP-TICKS: interval after each step; 0 = burned out.</summary>
    private static readonly int[] LampTickTable = [50, 30, 20, 10, 4, 0];
    private const string Dimmer = "The lamp appears to be getting dimmer.";
    /// <summary>act1.38 LAMP-TELLS.</summary>
    private static readonly string[] LampTells = [Dimmer, Dimmer, Dimmer, Dimmer, "The lamp is dying."];
    /// <summary>The clock events (MDL CEVENTs on the CLOCKER's list): the lamp, the candles, the fuse, the balloon, ...</summary>
    public List<ClockEvent> ClockEvents { get; } = new();
    public TextWriter Output { get; set; } = Console.Out;
    /// <summary>SCORE-MAX, computed by World from room and object values (see Scoring.ComputeMaxScore).</summary>
    public int MaxScore { get; set; }
    /// <summary>QUIT asked "Do you wish to leave the game?"; the next input line is the answer (rooms.99 FINISH).</summary>
    public bool AwaitingQuitConfirmation { get; set; }

    /// <summary>Path used by SAVE and RESTORE.</summary>
    public string SavePath { get; set; } = GameSave.DefaultSavePath;

    /// <summary>
    /// rooms.99 CLOCK-INT: clock event <paramref name="name"/> (a <see cref="Clocks"/> constant) runs in
    /// <paramref name="ticks"/> moves (0: at the end of this one). An event already set is reset, keeping whether it is
    /// enabled; a new one is enabled.
    /// </summary>
    public void Schedule(int ticks, string name, string? argument = null)
    {
        if (FindClock(name) is { } clock)
        {
            clock.Ticks = ticks;
            clock.Argument = argument;
        }
        else
            ClockEvents.Add(new ClockEvent { Name = name, Ticks = ticks, Argument = argument });
    }

    /// <summary>rooms.99 CLOCK-ENABLE / CLOCK-DISABLE: a disabled event keeps its ticks but doesn't count down.</summary>
    public void EnableClock(string name, bool enabled)
    {
        if (FindClock(name) is { } clock) clock.Enabled = enabled;
    }

    /// <summary>Takes clock event <paramref name="name"/> off the clock (it won't run).</summary>
    public void CancelClock(string name) => ClockEvents.RemoveAll(e => e.Name == name);

    /// <summary>The clock event <paramref name="name"/> if it is set, else null.</summary>
    public ClockEvent? FindClock(string name) => ClockEvents.Find(e => e.Name == name);

    /// <summary>
    /// rooms.99 CLOCK-DEMON: every enabled clock event counts down a move, and those that reach 0 run. Returns true
    /// when one ran.
    /// </summary>
    private bool RunClocks()
    {
        var fired = false;
        foreach (var clock in ClockEvents.ToList())
        {
            if (!clock.Enabled || !ClockEvents.Contains(clock) || --clock.Ticks > 0) continue;
            ClockEvents.Remove(clock);
            Clocks.Run(this, clock);
            fired = true;
        }
        return fired;
    }

    /// <summary>The demons and clocks of one move, after the command (rooms.99 RDCOM).</summary>
    public void ProcessClocks()
    {
        if (World == null) return;
        RunClocks();
        // Demons (SWORD-DEMON, FIGHT-DEMON, ROBBER-DEMON)
        Combat.SwordDemon(this);
        Combat.FightDemon(this);
        Thief.Demon(this);
    }

    // act1.38 CANDLE-TICKS and CANDLE-TELLS
    private static readonly int[] CandleTickTable = [20, 10, 5, 0];
    private static readonly string[] CandleTells = ["The candles grow shorter.", "The candles grow shorter.", "The candles are very short."];

    /// <summary>act1.38 LIGHT-INT for the candles: the next step of CANDLE-TICKS, told if they are with you or here.</summary>
    internal void CandleLightInt()
    {
        var candles = World!.FindObject(Obj.Candl)!;
        Lights.CandleStage++;
        var ticks = CandleTickTable[Math.Min(Lights.CandleStage, CandleTickTable.Length) - 1];
        if (ticks > 0) Schedule(ticks, Clocks.Candles);
        var seen = candles.InRoom == null || candles.InRoom == Here;
        if (ticks == 0)
        {
            if (seen) Output.WriteLine("I hope you have more light than from a pair of candles.");
            candles.LightAmount = 0;
        }
        else if (seen)
            Output.WriteLine(CandleTells[Lights.CandleStage - 1]);
    }

    /// <summary>
    /// rooms.99 CLOCK-DEMON run once more within a turn, as WAIT does. Returns true when a clock event ran (WAIT then
    /// stops waiting).
    /// </summary>
    public bool ClockTick() => World != null && RunClocks();

    /// <summary>
    /// act1.38 LIGHT-INT for the lamp: take the next step of LAMP-TICKS. Warnings and the burn-out message are
    /// only told when the lamp is carried or in this room.
    /// </summary>
    internal void LampLightInt()
    {
        var lamp = World!.FindObject(Obj.Lamp)!;
        Lights.LampStage++;
        var ticks = LampTickTable[Math.Min(Lights.LampStage, LampTickTable.Length) - 1];
        if (ticks > 0) Schedule(ticks, Clocks.Lantern);
        var nearby = lamp.InRoom == null || lamp.InRoom == Here;
        if (ticks == 0)
        {
            if (nearby) Output.WriteLine("I hope you have more light than from a " + lamp.DescriptionShort + ".");
            lamp.LightAmount = 0;
        }
        else if (nearby)
        {
            Output.WriteLine(LampTells[Lights.LampStage - 1]);
        }
    }

    /// <summary>Moves between healed wounds (CURE-CLOCK).</summary>
    public const int CureInterval = 30;

    /// <summary>Strength one wound costs in the port's melee (<see cref="Combat.FightDemon"/>).</summary>
    public const int WoundStrength = 20;

    /// <summary>
    /// CURE-CLOCK (CURIN, a CEVENT in dung.56). Its code is part of the melee, which is not in the MIT archive. In
    /// the port, as in later Dungeon, wounds heal slowly: one wound every <see cref="CureInterval"/> moves after
    /// you are hurt (<see cref="Combat.FightDemon"/> sets the clock), until you are whole.
    /// </summary>
    internal static void CureClock(GameState state)
    {
        var hero = state.Winner;
        hero.Strength = Math.Min(Adventurer.FullStrength, hero.Strength + WoundStrength);
        if (hero.Strength < Adventurer.FullStrength) state.Schedule(CureInterval, Clocks.Cure);
    }

    public World? World { get; set; }
    internal StreamWriter? ScriptFile { get; set; }

    public bool GetFlag(string name) => Flags.TryGetValue(name, out var v) && v;
    public void SetFlag(string name, bool value) => Flags[name] = value;

    /// <summary>Block a room (explosion debris / collapsed ledge).</summary>
    public void MungRoom(string? roomId, string message)
    {
        var r = World?.FindRoom(roomId ?? "");
        if (r != null) { r.Munged = true; r.MungedMessage = message; }
    }

    /// <summary>
    /// rooms.99 LIT?: the room has its own light, or something in the room or carried is burning (OLIGHT? &gt; 0),
    /// also inside a visible open or transparent container (LFCN). An unlit lamp (-1) gives no light.
    /// (LFCN also looks at what other actors carry; there are no actors in the port yet.)
    /// </summary>
    public static bool IsLit(GameState state, Room? room = null)
    {
        var r = room ?? state.Here;
        if (r == null) return false;
        return r.HasLight || GivesLight(r.Objects) || GivesLight(state.Winner.Inventory);
    }

    /// <summary>rooms.99 LFCN.</summary>
    private static bool GivesLight(IEnumerable<GameObject> objects) =>
        objects.Any(o => o.LightAmount > 0
            || (o.IsVisible && o.IsOpenOrTransparent && o.Contents.Any(c => c.LightAmount > 0)));
}
