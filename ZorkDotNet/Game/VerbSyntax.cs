namespace ZorkDotNet.Game;

/// <summary>
/// VARG: argument spec for one slot (np.93 defs.63). VWord = where to look + constraints.
/// </summary>
[Flags]
public enum VArgFlags
{
    None = 0,
    /// <summary>Look in AOBJS (inventory).</summary>
    VABIT = 1,
    /// <summary>Look in ROBJS (room).</summary>
    VRBIT = 2,
    /// <summary>Object need not be takeable (e.g. fixed in room).</summary>
    VTBIT = 4,
    /// <summary>Exact match / object must have specific bit (MDL VXBIT).</summary>
    VXBIT = 8,
}

/// <summary>
/// One argument slot spec: optional preposition and VWord flags (AOBJS, ROBJS, NO-TAKE, etc.).
/// </summary>
public readonly record struct VArg(string? Prep, VArgFlags VWord)
{
    /// <summary>No object required (empty slot).</summary>
    public static readonly VArg Empty = new(null, VArgFlags.None);

    public bool WantsInventory => (VWord & VArgFlags.VABIT) != 0;
    public bool WantsRoom => (VWord & VArgFlags.VRBIT) != 0;
    public bool NoTake => (VWord & VArgFlags.VTBIT) != 0;
}

/// <summary>
/// One syntax pattern for a verb (np.93 SYNTAX): slot1, slot2, action, driver, flip.
/// </summary>
public sealed class Syntax
{
    public VArg Syn1 { get; }
    public VArg Syn2 { get; }
    public string Action { get; }
    public bool IsDriver { get; }
    public bool IsFlip { get; }

    public Syntax(VArg syn1, VArg syn2, string action, bool isDriver = false, bool isFlip = false)
    {
        Syn1 = syn1;
        Syn2 = syn2;
        Action = action;
        IsDriver = isDriver;
        IsFlip = isFlip;
    }
}

/// <summary>
/// VSPEC: list of SYNTAX for one verb (np.93 MAKE-ACTION).
/// </summary>
public sealed class VerbSpec
{
    public string ActionName { get; }
    public string VerbString { get; }
    public IReadOnlyList<Syntax> Syntaxes { get; }

    public VerbSpec(string actionName, string verbString, IReadOnlyList<Syntax> syntaxes)
    {
        ActionName = actionName;
        VerbString = verbString;
        Syntaxes = syntaxes;
    }
}

/// <summary>
/// PREPVEC-style: canonical prepositions. WITH/USING/THROU → "WITH"; IN/INSID/INTO → "IN".
/// </summary>
public static class Preposition
{
    public const string With = "WITH";
    public const string In = "IN";
    public const string To = "TO";
    public const string At = "AT";
    public const string On = "ON";
    public const string Off = "OFF";

    public static string? Normalize(string normWord)
    {
        if (normWord == "WITH" || normWord == "USING" || normWord == "THROU") return With;
        if (normWord == "IN" || normWord == "INSID" || normWord == "INTO") return In;
        if (normWord == "TO") return To;
        if (normWord == "AT") return At;
        if (normWord == "ON") return On;
        if (normWord == "OFF") return Off;
        return null;
    }
}

/// <summary>
/// Verb table: normalized (first-five) word → VerbSpec. Built from np.92/np.93 MAKE-ACTION style.
/// </summary>
public static class VerbTable
{
    private static IReadOnlyDictionary<string, VerbSpec>? _table;
    private static Dictionary<string, VerbSpec>? _byAction;

    public static IReadOnlyDictionary<string, VerbSpec> Table =>
        _table ??= BuildTable();

    /// <summary>Look up VerbSpec by action name (e.g. for orphan disambiguation dispatch).</summary>
    public static VerbSpec? GetByActionName(string actionName)
    {
        _byAction ??= Table.Values.Distinct().ToDictionary(s => s.ActionName, StringComparer.OrdinalIgnoreCase);
        return _byAction.TryGetValue(actionName, out var s) ? s : null;
    }

    private static VerbSpec V(string actionName, string verbString, params Syntax[] syntaxes) =>
        new(actionName, verbString, syntaxes);

    private static VArg A(VArgFlags flags, string? prep = null) => new(prep, flags);
    private static readonly VArg ArgInv = A(VArgFlags.VABIT);
    private static readonly VArg ArgRoom = A(VArgFlags.VRBIT);
    private static readonly VArg ArgRoomNoTake = A(VArgFlags.VRBIT | VArgFlags.VTBIT);
    private static readonly VArg ArgInvWith = A(VArgFlags.VABIT, Preposition.With);
    private static readonly VArg ArgRoomTo = A(VArgFlags.VRBIT, Preposition.To);
    private static readonly VArg ArgRoomAt = A(VArgFlags.VRBIT, Preposition.At);
    private static readonly VArg ArgRoomIn = A(VArgFlags.VRBIT, Preposition.In);

    private static Dictionary<string, VerbSpec> BuildTable()
    {
        var dict = new Dictionary<string, VerbSpec>(StringComparer.OrdinalIgnoreCase);

        void AddWords(string[] words, VerbSpec spec)
        {
            foreach (var w in words)
            {
                var n = Parser.NormWord(w);
                if (!string.IsNullOrEmpty(n)) dict[n] = spec;
            }
        }

        // LOOK: no obj (driver)
        // dung.56 LOOK: LOOK, and LOOK AT obj (ROOM-INFO for the object, like EXAMINE)
        AddWords(new[] { "LOOK", "L", "STARE", "GAZE" }, V("LOOK", "look",
            new Syntax(VArg.Empty, VArg.Empty, "LOOK", isDriver: true),
            new Syntax(A(VArgFlags.VABIT | VArgFlags.VRBIT | VArgFlags.VTBIT, Preposition.At), VArg.Empty, "EXAMINE")));
        AddWords(new[] { "EXAMI", "DESCR", "WHAT", "WHATS", "WHAT'" }, V("EXAMINE", "examine",
            new Syntax(ArgRoomNoTake, VArg.Empty, "EXAMINE", isDriver: true)));

        // TAKE: ROBJS (prefer take from room)
        AddWords(new[] { "TAKE", "GET", "PICK", "CARRY", "HOLD" }, V("TAKE", "take",
            new Syntax(ArgRoom, VArg.Empty, "TAKE", isDriver: true)));

        // DROP: AOBJS
        AddWords(new[] { "DROP", "PUT", "RELEA" }, V("DROP", "drop",
            new Syntax(ArgInv, VArg.Empty, "DROP", isDriver: true)));

        // INVENTORY
        AddWords(new[] { "INVEN", "I", "LIST" }, V("INVENTORY", "inventory",
            new Syntax(VArg.Empty, VArg.Empty, "INVENTORY", isDriver: true)));

        // OPEN / CLOSE (NO-TAKE: use in place, don't take first)
        AddWords(new[] { "OPEN" }, V("OPEN", "open",
            new Syntax(ArgRoomNoTake, VArg.Empty, "OPEN", isDriver: true)));
        AddWords(new[] { "CLOSE", "SHUT" }, V("CLOSE", "close",
            new Syntax(ArgRoomNoTake, VArg.Empty, "CLOSE", isDriver: true)));

        // UNLOCK obj WITH tool (UNLOCKER) / LOCK obj (LOCKER): their own verbs in dung.56, not OPEN/CLOSE.
        // UNLOCK is a driver so "unlock grate" reaches UNLOCKER, which picks the tool GWIM-style.
        AddWords(new[] { "UNLOCK" }, V("UNLOCK", "unlock",
            new Syntax(ArgRoomNoTake, ArgInvWith, "UNLOCK", isDriver: true)));
        AddWords(new[] { "LOCK" }, V("LOCK", "lock",
            new Syntax(ArgRoomNoTake, VArg.Empty, "LOCK", isDriver: true)));

        // READ (verb that takes object)
        AddWords(new[] { "READ", "SKIM", "SCAN" }, V("READ", "read",
            new Syntax(ArgRoom, VArg.Empty, "READ", isDriver: true)));
        // Note: LOOK words also have READ; LOOK takes precedence in current code. We register READ separately for "read X".

        // EAT / DRINK
        AddWords(new[] { "EAT", "DEVOU", "CONSU", "GOBBL", "MUNCH", "TASTE" }, V("EAT", "eat",
            new Syntax(ArgRoom, VArg.Empty, "EAT", isDriver: true)));
        AddWords(new[] { "DRINK", "SWALL", "IMBIB" }, V("DRINK", "drink",
            new Syntax(ArgRoom, VArg.Empty, "DRINK", isDriver: true)));

        // THROW: AOBJS [AT ROBJS]
        AddWords(new[] { "THROW", "TOSS", "HURL", "CHUCK" }, V("THROW", "throw",
            new Syntax(ArgInv, VArg.Empty, "THROW", isDriver: true),
            new Syntax(ArgInv, ArgRoomAt, "THROW")));

        // WAVE
        AddWords(new[] { "WAVE", "SHAKE" }, V("WAVE", "wave",
            new Syntax(ArgRoom, VArg.Empty, "WAVE", isDriver: true)));

        // RUB (mirror puzzle)
        AddWords(new[] { "RUB", "RUBB", "CARES", "TOUCH", "FONDL" }, V("RUB", "rub",
            new Syntax(ArgRoom, VArg.Empty, "RUB", isDriver: true)));

        // ATTACK: ROBJS [WITH AOBJS]
        AddWords(new[] { "ATTAC", "KILL", "FIGHT", "HIT", "HACK", "FROB", "HURT", "INJUR", "DAMAG", "MURDE", "SLAY", "DISPA" }, V("ATTACK", "attack",
            new Syntax(ArgRoom, VArg.Empty, "ATTACK", isDriver: true),
            new Syntax(ArgRoom, ArgInvWith, "ATTACK")));

        // MOVE / LIFT
        AddWords(new[] { "MOVE", "PUSH", "SLIDE", "PULL", "TUG", "PRESS" }, V("MOVE", "move",
            new Syntax(ArgRoom, VArg.Empty, "MOVE", isDriver: true)));
        AddWords(new[] { "LIFT", "RAISE" }, V("LIFT", "lift",
            new Syntax(ArgRoom, VArg.Empty, "LIFT", isDriver: true)));

        // PUT: AOBJS IN ROBJS
        AddWords(new[] { "PUT", "STUFF", "PLACE", "INSER" }, V("PUT", "put",
            new Syntax(ArgInv, ArgRoomIn, "PUT"),
            new Syntax(ArgInv, VArg.Empty, "PUT", isDriver: true)));

        // GIVE: AOBJS TO ROBJS
        AddWords(new[] { "GIVE", "FEED", "HAND", "DONAT" }, V("GIVE", "give",
            new Syntax(ArgInv, VArg.Empty, "GIVE", isDriver: true),
            new Syntax(ArgInv, ArgRoomTo, "GIVE")));

        // RAISE / LOWER (object actions)
        AddWords(new[] { "RAISE" }, V("RAISE", "raise",
            new Syntax(ArgRoom, VArg.Empty, "RAISE", isDriver: true)));
        AddWords(new[] { "LOWER" }, V("LOWER", "lower",
            new Syntax(ArgRoom, VArg.Empty, "LOWER", isDriver: true)));

        // BURN
        AddWords(new[] { "BURN", "LIGHT", "INCIN", "IGNIT" }, V("BURN", "burn",
            new Syntax(ArgRoom, VArg.Empty, "BURN", isDriver: true)));

        // FILL / TIE / UNTIE / BREAK / POUR
        AddWords(new[] { "FILL" }, V("FILL", "fill",
            new Syntax(ArgRoom, VArg.Empty, "FILL", isDriver: true)));
        AddWords(new[] { "TIE", "TIED", "KNOT", "FASTE" }, V("TIE", "tie",
            new Syntax(ArgRoom, VArg.Empty, "TIE", isDriver: true),
            new Syntax(ArgRoom, ArgRoomTo, "TIE")));
        AddWords(new[] { "UNTIE", "FREE" }, V("UNTIE", "untie",
            new Syntax(ArgRoom, VArg.Empty, "UNTIE", isDriver: true)));
        AddWords(new[] { "BREAK", "SMASH", "MUNG" }, V("BREAK", "break",
            new Syntax(ArgRoom, VArg.Empty, "BREAK", isDriver: true)));
        AddWords(new[] { "POUR", "SPILL" }, V("POUR", "pour",
            new Syntax(ArgRoom, VArg.Empty, "POUR", isDriver: true)));

        // dung.56 TURN: TURN obj [WITH tool] (driver), TURN ON light, TURN OFF light. The light slots are AOBJS ROBJS
        // without NO-TAKE, so a lamp on the floor is taken first (np.93 TAKE-IT-OR-LEAVE-IT).
        AddWords(new[] { "TURN" }, V("TURN", "turn",
            new Syntax(ArgRoomNoTake, VArg.Empty, "TURN", isDriver: true),
            new Syntax(A(VArgFlags.VABIT | VArgFlags.VRBIT, Preposition.On), VArg.Empty, "TURNON"),
            new Syntax(A(VArgFlags.VABIT | VArgFlags.VRBIT, Preposition.Off), VArg.Empty, "TURNOFF")));

        // System / no object
        AddWords(new[] { "BRIEF" }, V("BRIEF", "brief", new Syntax(VArg.Empty, VArg.Empty, "BRIEF", isDriver: true)));
        AddWords(new[] { "UNBRI" }, V("UNBRIEF", "unbrief", new Syntax(VArg.Empty, VArg.Empty, "UNBRIEF", isDriver: true)));
        AddWords(new[] { "SUPER" }, V("SUPERBRIEF", "superbrief", new Syntax(VArg.Empty, VArg.Empty, "SUPERBRIEF", isDriver: true)));
        AddWords(new[] { "UNSUP" }, V("UNSUPERBRIEF", "unsuperbrief", new Syntax(VArg.Empty, VArg.Empty, "UNSUPERBRIEF", isDriver: true)));
        AddWords(new[] { "QUIT", "Q" }, V("QUIT", "quit", new Syntax(VArg.Empty, VArg.Empty, "QUIT", isDriver: true)));
        AddWords(new[] { "SCORE" }, V("SCORE", "score", new Syntax(VArg.Empty, VArg.Empty, "SCORE", isDriver: true)));
        AddWords(new[] { "INFO" }, V("INFO", "info", new Syntax(VArg.Empty, VArg.Empty, "INFO", isDriver: true)));
        AddWords(new[] { "SAVE" }, V("SAVE", "save", new Syntax(VArg.Empty, VArg.Empty, "SAVE", isDriver: true)));
        AddWords(new[] { "RESTO" }, V("RESTORE", "restore", new Syntax(VArg.Empty, VArg.Empty, "RESTORE", isDriver: true)));
        AddWords(new[] { "SCRIP" }, V("SCRIPT", "script", new Syntax(VArg.Empty, VArg.Empty, "SCRIPT", isDriver: true)));
        AddWords(new[] { "UNSCR" }, V("UNSCRIPT", "unscript", new Syntax(VArg.Empty, VArg.Empty, "UNSCRIPT", isDriver: true)));
        AddWords(new[] { "DIAGN" }, V("DIAGNOSE", "diagnose", new Syntax(VArg.Empty, VArg.Empty, "DIAGNOSE", isDriver: true)));
        AddWords(new[] { "EXORC", "EXORCI", "XORCI" }, V("EXORCISE", "exorcise", new Syntax(VArg.Empty, VArg.Empty, "EXORCISE", isDriver: true)));
        AddWords(new[] { "BOARD" }, V("BOARD", "board", new Syntax(ArgRoom, VArg.Empty, "BOARD", isDriver: true)));
        AddWords(new[] { "DISEM", "GETOU" }, V("DISEMBARK", "disembark", new Syntax(VArg.Empty, VArg.Empty, "DISEMBARK", isDriver: true)));

        // Short verbs of the Verbs class (dung.56 SADD-ACTION and friends)
        void Simple(string action, params string[] words) =>
            AddWords(words, V(action, action.ToLowerInvariant(), new Syntax(VArg.Empty, VArg.Empty, action, isDriver: true)));
        Simple("BACK", "BACK");
        Simple("REPENT", "REPEN");
        Simple("CURSE", "CURSE", "SHIT", "FUCK", "DAMN");
        Simple("JARGON", "JARGO", "FOO", "BLETC");
        Simple("CHOMP", "CHOMP", "LOSE", "BARF");
        Simple("YELL", "YELL", "SCREA", "SHOUT");
        Simple("WIN", "WIN", "WINNA");
        Simple("FROBOZZ", "FROBO");
        Simple("TREASURE", "TREAS");
        Simple("TEMPLE", "TEMPL");
        Simple("PRAY", "PRAY");
        Simple("JUMP", "JUMP", "LEAP", "VAULT");
        Simple("SKIP", "SKIP", "HOP");
        Simple("MUMBLE", "MUMBL", "SIGH");
        Simple("ZORK", "ZORK");
        Simple("DUNGEON", "DUNGE");
        Simple("GRANITE", "GRANI");
        Simple("GERONIMO", "GERON");
        Simple("BLAST", "BLAST");
        Simple("ODYSSEUS", "ODYSS", "ULYSS");
        Simple("TIME", "TIME");
        AddWords(new[] { "HELLO", "HI" }, V("HELLO", "hello",
            new Syntax(VArg.Empty, VArg.Empty, "HELLO", isDriver: true),
            new Syntax(ArgRoomNoTake, VArg.Empty, "HELLO")));

        // dung.56 SADD-ACTION "WELL": the answer to the riddle
        AddWords(new[] { "WELL" }, V("WELL", "well", new Syntax(VArg.Empty, VArg.Empty, "WELL", isDriver: true)));

        // WALK/GO: special (direction); handled before verb table
        AddWords(new[] { "WALK", "GO", "G" }, V("WALK", "walk", new Syntax(VArg.Empty, VArg.Empty, "WALK", isDriver: true)));

        return dict;
    }
}
