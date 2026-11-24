namespace ZorkDotNet.Game;

/// <summary>
/// A clock event (MDL CEVENT) that runs when Moves >= TriggerMove. It is data, not a delegate, so SAVE can
/// store it: <see cref="Name"/> picks the routine in <see cref="Clocks"/>, <see cref="Argument"/> is its input.
/// </summary>
public record ScheduledEvent(int TriggerMove, string Name, string? Argument = null);

/// <summary>
/// Global game state (MDL: WINNER, HERE, PRSVEC, MOVES, flags, etc.).
/// </summary>
public class GameState
{
    public Adventurer Winner { get; } = new();
    public Room Here => Winner.CurrentRoom;
    public int Moves => Winner.Moves;
    public Dictionary<string, bool> Flags { get; } = new();
    public List<Room> Rooms { get; } = new();
    public List<GameObject> AllObjects { get; } = new();
    public bool Running { get; set; } = true;
    /// <summary>MDL HS: how many times the player has said HELLO (act1.38 HELLO).</summary>
    public int HelloCount { get; set; }
    /// <summary>Real-time clock for TIME (rooms.99 PLAY-TIME); tests can replace it.</summary>
    public TimeProvider Clock { get; set; } = TimeProvider.System;
    /// <summary>When this game started, for TIME.</summary>
    public DateTimeOffset StartedAt { get; set; } = TimeProvider.System.GetUtcNow();

    /// <summary>Candle clock (CNDIN): moves left until the next LIGHT-INT step; 0 until the candles are first touched.</summary>
    public int CandleTicks { get; set; }
    /// <summary>Candle clock: LIGHT-INT steps taken so far (the count in the candles' ORAND).</summary>
    public int CandleStage { get; set; }
    /// <summary>The matchbook's ORAND: matches left (dung.56 starts it at 5; the last one never lights).</summary>
    public int MatchesLeft { get; set; } = 5;

    /// <summary>The thief's demon (MDL HACK): the room he works in (HROOM).</summary>
    public string ThiefRoom { get; set; } = Thief.Rooms[0];
    /// <summary>The thief's demon: the index in <see cref="Thief.Rooms"/> of the next room he goes to (HROOMS).</summary>
    public int ThiefNext { get; set; }
    /// <summary>The thief's bag (HOBJS), most recent first.</summary>
    public List<string> ThiefBag { get; } = new();
    /// <summary>The thief's demon still runs (HACTION); false once he is dead.</summary>
    public bool ThiefActive { get; set; } = true;
    /// <summary>The thief has shown himself in this room (HFLAG).</summary>
    public bool ThiefAnnounced { get; set; }
    /// <summary>The robot as an adventurer (its ADV in dung.56): its room and what it carries.</summary>
    public Adventurer Robot { get; } = new();
    /// <summary>The robot is carrying out a TELL ROBOT command (MDL WINNER is the robot).</summary>
    public bool RobotActing { get; set; }
    /// <summary>MDL BLOC: the room the balloon is in, or heading for (act2.27).</summary>
    public string BalloonLocation { get; set; } = "VLBOT";
    /// <summary>act2.27 LIGHT-SHAFT: the points for lighting the bottom of the shaft have been given.</summary>
    public bool LightShaftScored { get; set; }
    /// <summary>
    /// act1.38 ECHO-ROOM: the Loud Room is drowning out everything; each line is echoed until you walk out or say
    /// ECHO. Never true across a SAVE (SAVE would be echoed too).
    /// </summary>
    public bool EchoListening { get; set; }
    /// <summary>The objects the Loud Room hid while listening (MDL ECHO-ROOM-BIT).</summary>
    public List<string> EchoHidden { get; } = new();

    /// <summary>MDL DEATHS: how many times the player has died.</summary>
    public int Deaths { get; set; }
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
    /// <summary>Lamp clock (LNTIN): moves while lit until the next LIGHT-INT step. Starts at 350 (dung.56).</summary>
    public int LampTicks { get; set; } = LampFirstInterval;
    /// <summary>Lamp clock: LIGHT-INT steps taken so far (MDL ORAND count), indexes LAMP-TICKS / LAMP-TELLS.</summary>
    public int LampStage { get; set; }

    /// <summary>dung.56: &lt;CLOCK-INT ,LNTIN 350&gt;.</summary>
    public const int LampFirstInterval = 350;
    /// <summary>act1.38 LAMP-TICKS: interval after each step; 0 = burned out.</summary>
    private static readonly int[] LampTickTable = [50, 30, 20, 10, 4, 0];
    private const string Dimmer = "The lamp appears to be getting dimmer.";
    /// <summary>act1.38 LAMP-TELLS.</summary>
    private static readonly string[] LampTells = [Dimmer, Dimmer, Dimmer, Dimmer, "The lamp is dying."];
    /// <summary>Scheduled events (MATCH burnout, FUSE explode, etc.) fired when Moves >= TriggerMove.</summary>
    public List<ScheduledEvent> ScheduledEvents { get; } = new();
    /// <summary>Sword glow state (0=none, 1=faint, 2=bright). MDL OTVAL on sword.</summary>
    public Dictionary<string, int> ObjectState { get; } = new();
    /// <summary>Villain health in melee (e.g. TROLL -> 2 hits to kill).</summary>
    public Dictionary<string, int> VillainStrength { get; } = new();
    public TextWriter Output { get; set; } = Console.Out;
    /// <summary>SCORE-MAX, computed by World from room and object values (see Scoring.ComputeMaxScore).</summary>
    public int MaxScore { get; set; }
    /// <summary>QUIT asked "Do you wish to leave the game?"; the next input line is the answer (rooms.99 FINISH).</summary>
    public bool AwaitingQuitConfirmation { get; set; }

    /// <summary>Path used by SAVE and RESTORE.</summary>
    public string SavePath { get; set; } = GameSave.DefaultSavePath;

    /// <summary>Schedule clock event <paramref name="name"/> (a <see cref="Clocks"/> constant) to run in <paramref name="delay"/> moves.</summary>
    public void Schedule(int delay, string name, string? argument = null) =>
        ScheduledEvents.Add(new ScheduledEvent(Moves + delay, name, argument));

    /// <summary>Run each move after parser (CEVENTs: lantern burn, match/fuse/candles, MAINT leak, etc.).</summary>
    public void ProcessClocks()
    {
        if (World == null) return;
        // Lamp clock: enabled while the lamp is on (LANTERN CLOCK-ENABLE / CLOCK-DISABLE), wherever the lamp is.
        var lamp = World.FindObject(Obj.Lamp);
        if (lamp != null && lamp.LightAmount > 0 && --LampTicks <= 0)
            LampLightInt(lamp);
        // Candle clock: runs while the candles burn, once they have been touched (act1.38 CANDLES).
        var candles = World.FindObject(Obj.Candl);
        if (candles != null && candles.LightAmount > 0 && CandleTicks > 0 && --CandleTicks <= 0)
            CandleLightInt(candles);
        var toRun = ScheduledEvents.Where(e => Moves >= e.TriggerMove).ToList();
        foreach (var e in toRun) ScheduledEvents.Remove(e);
        foreach (var e in toRun) Clocks.Run(this, e);
        // Demons (SWORD-DEMON, FIGHT-DEMON, ROBBER-DEMON)
        Combat.SwordDemon(this);
        Combat.FightDemon(this);
        Thief.Demon(this);
        // CEVENT CURIN (dung.56)
        CureClock(this);
    }

    // act1.38 CANDLE-TICKS and CANDLE-TELLS
    private static readonly int[] CandleTickTable = [20, 10, 5, 0];
    private static readonly string[] CandleTells = ["The candles grow shorter.", "The candles grow shorter.", "The candles are very short."];

    /// <summary>act1.38 LIGHT-INT for the candles: the next step of CANDLE-TICKS, told if they are with you or here.</summary>
    private void CandleLightInt(GameObject candles)
    {
        CandleStage++;
        CandleTicks = CandleTickTable[Math.Min(CandleStage, CandleTickTable.Length) - 1];
        var seen = candles.InRoom == null || candles.InRoom == Here;
        if (CandleTicks == 0)
        {
            if (seen) Output.WriteLine("I hope you have more light than from a pair of candles.");
            candles.LightAmount = 0;
        }
        else if (seen)
            Output.WriteLine(CandleTells[CandleStage - 1]);
    }

    /// <summary>
    /// rooms.99 CLOCK-DEMON run once more within a turn, as WAIT does: the lamp burns a move and every pending clock
    /// event comes one move closer. Returns true when something happened (WAIT then stops waiting).
    /// </summary>
    public bool ClockTick()
    {
        if (World == null) return false;
        var fired = false;
        var lamp = World.FindObject(Obj.Lamp);
        if (lamp != null && lamp.LightAmount > 0 && --LampTicks <= 0)
        {
            LampLightInt(lamp);
            fired = true;
        }
        for (var i = 0; i < ScheduledEvents.Count; i++)
            ScheduledEvents[i] = ScheduledEvents[i] with { TriggerMove = ScheduledEvents[i].TriggerMove - 1 };
        var toRun = ScheduledEvents.Where(e => Moves >= e.TriggerMove).ToList();
        foreach (var e in toRun) ScheduledEvents.Remove(e);
        foreach (var e in toRun) Clocks.Run(this, e);
        return fired || toRun.Count > 0;
    }

    /// <summary>
    /// act1.38 LIGHT-INT for the lamp: take the next step of LAMP-TICKS. Warnings and the burn-out message are
    /// only told when the lamp is carried or in this room.
    /// </summary>
    private void LampLightInt(GameObject lamp)
    {
        LampStage++;
        LampTicks = LampTickTable[Math.Min(LampStage, LampTickTable.Length) - 1];
        var nearby = lamp.InRoom == null || lamp.InRoom == Here;
        if (LampTicks == 0)
        {
            if (nearby) Output.WriteLine("I hope you have more light than from a " + lamp.DescriptionShort + ".");
            lamp.LightAmount = 0;
        }
        else if (nearby)
        {
            Output.WriteLine(LampTells[LampStage - 1]);
        }
    }

    /// <summary>Moves between healed wounds (CURE-CLOCK).</summary>
    public const int CureInterval = 30;

    /// <summary>Strength one wound costs in the port's melee (<see cref="Combat.FightDemon"/>).</summary>
    public const int WoundStrength = 20;

    /// <summary>
    /// CURE-CLOCK (CURIN, a CEVENT in dung.56). Its code is part of the melee, which is not in the MIT archive. In
    /// the port, as in later Dungeon, wounds heal slowly: one wound every <see cref="CureInterval"/> moves.
    /// </summary>
    public static void CureClock(GameState state)
    {
        var hero = state.Winner;
        if (hero.Strength < Adventurer.FullStrength && state.Moves % CureInterval == 0)
            hero.Strength = Math.Min(Adventurer.FullStrength, hero.Strength + WoundStrength);
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
