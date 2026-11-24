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
        var lamp = World.FindObject("LAMP");
        if (lamp != null && lamp.LightAmount > 0 && --LampTicks <= 0)
            LampLightInt(lamp);
        // Candle clock: runs while the candles burn, once they have been touched (act1.38 CANDLES).
        var candles = World.FindObject("CANDL");
        if (candles != null && candles.LightAmount > 0 && CandleTicks > 0 && --CandleTicks <= 0)
            CandleLightInt(candles);
        var toRun = ScheduledEvents.Where(e => Moves >= e.TriggerMove).ToList();
        foreach (var e in toRun) ScheduledEvents.Remove(e);
        foreach (var e in toRun) Clocks.Run(this, e);
        // Demons (SWORD-DEMON, FIGHT-DEMON, ROBBER-DEMON)
        Combat.SwordDemon(this);
        Combat.FightDemon(this);
        RunRobberDemon();
        // CEVENTs with no effect in original MDL (dung.56 CURIN, SPHIN)
        CureClock(this);
        SphereFunction(this);
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
        var lamp = World.FindObject("LAMP");
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

    /// <summary>CURE-CLOCK (CURIN). CEVENT in dung.56; no-op in original MDL — implemented as explicit no-op.</summary>
    public static void CureClock(GameState _) { }

    /// <summary>SPHERE-FUNCTION (SPHIN). CEVENT in dung.56; no-op in original MDL — implemented as explicit no-op.</summary>
    public static void SphereFunction(GameState _) { }

    /// <summary>ROBBER-DEMON (act1.37 ROBBER): thief in room can rob (ROB-ROOM + ROB-ADV); thief not in room can appear.</summary>
    private void RunRobberDemon()
    {
        var thief = World?.FindObject("THIEF");
        if (thief == null || Here == null) return;
        var treas = World?.FindRoom("TREAS");

        if (thief.InRoom == Here)
        {
            if (Random.Shared.Next(100) < 30)
            {
                var fromRoom = Combat.RobRoom(this, Here, 100);
                foreach (var o in fromRoom)
                {
                    Here.Objects.Remove(o);
                    o.InRoom = null;
                    o.Carrier = null;
                    o.Container = null;
                    if (treas != null) { o.InRoom = treas; treas.Objects.Add(o); }
                }
                var fromAdv = ObjectActions.RobAdventurer(this, printMessage: false);
                if (fromRoom.Count > 0 || fromAdv)
                {
                    Output.WriteLine("The other occupant just left, still carrying his large bag.  You may not have noticed that he robbed you blind first.");
                    Here.Objects.Remove(thief);
                    thief.InRoom = null;
                }
            }
        }
        else if (thief.InRoom == null && Here.Id != "TREAS" && Random.Shared.Next(100) < 30)
        {
            thief.InRoom = Here;
            Here.Objects.Add(thief);
            if (!GetFlag("THIEF-APPEARED!-FLAG"))
            {
                SetFlag("THIEF-APPEARED!-FLAG", true);
                Output.WriteLine("Someone carrying a large bag is casually leaning against one of the walls here.  He does not speak, but it is clear from his aspect that the bag will be taken only over his dead body.");
            }
        }
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
