using System.Text.RegularExpressions;
using ZorkDotNet.Game;

namespace ZorkDotNet.Tests.Original;

/// <summary>
/// Compares the port's freshly built World (and parser vocabulary) with dung.56.
/// Each difference is a single stable line "Category: KEY: detail", so the list can be kept in KnownDifferences.txt.
/// </summary>
internal static partial class WorldComparer
{
    /// <summary>MDL object bit → port ObjectFlags. Bits not listed here have no port equivalent yet.</summary>
    private static readonly Dictionary<string, ObjectFlags> FlagMap = new(StringComparer.Ordinal)
    {
        ["OVISON"] = ObjectFlags.Visible,
        ["READBIT"] = ObjectFlags.Readable,
        ["TAKEBIT"] = ObjectFlags.Take,
        ["DOORBIT"] = ObjectFlags.Door,
        ["TRANSBIT"] = ObjectFlags.Transparent,
        ["FOODBIT"] = ObjectFlags.Food,
        ["NDESCBIT"] = ObjectFlags.NoDescribe,
        ["DRINKBIT"] = ObjectFlags.Drinkable,
        ["CONTBIT"] = ObjectFlags.Container,
        ["LIGHTBIT"] = ObjectFlags.Light,
        ["BURNBIT"] = ObjectFlags.Burnable,
        ["FLAMEBIT"] = ObjectFlags.OnFire,
        ["TOOLBIT"] = ObjectFlags.Tool,
        ["TURNBIT"] = ObjectFlags.Turnable,
        ["VEHBIT"] = ObjectFlags.Vehicle,
        ["WEAPONBIT"] = ObjectFlags.Weapon,
        ["SACREDBIT"] = ObjectFlags.Sacred,
        ["VILLAIN"] = ObjectFlags.Villain,
    };

    /// <summary>Port sizes at or above this count as MDL BIGFIX (can't be carried).</summary>
    private const int PortBigSize = 100;

    public static List<string> Compare(OriginalDungeon original)
    {
        var state = new GameState { Output = TextWriter.Null };
        var world = new World(state);
        var diffs = new List<string>();
        CompareRooms(original, world, diffs);
        CompareObjects(original, world, diffs);
        CompareVocabulary(original, diffs);
        return diffs.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    // ---------------- Rooms ----------------

    private static void CompareRooms(OriginalDungeon original, World world, List<string> diffs)
    {
        var portRooms = world.Rooms.ToDictionary(r => Id5(r.Id), StringComparer.Ordinal);
        var origIds = original.Rooms.Select(r => Id5(r.Id)).ToHashSet(StringComparer.Ordinal);

        foreach (var extra in portRooms.Keys.Where(k => !origIds.Contains(k)))
            diffs.Add($"Room not in original: {extra}");

        foreach (var o in original.Rooms)
        {
            var id = Id5(o.Id);
            if (!portRooms.TryGetValue(id, out var p))
            {
                diffs.Add($"Room missing: {id} ({Norm(o.ShortDescription)})");
                continue;
            }

            if (Norm(o.ShortDescription) != Norm(p.DescriptionShort))
                diffs.Add($"Room short description: {id}: original \"{Norm(o.ShortDescription)}\", port \"{Norm(p.DescriptionShort)}\"");

            // Empty original text means the room action prints the description; that can't be checked statically.
            // Same when the port leaves the text empty and has a room action.
            var origLong = Norm(o.LongDescription);
            if (origLong.Length > 0 && origLong != Norm(p.DescriptionLong) && !(p.DescriptionLong.Length == 0 && p.RoomAction != null))
                diffs.Add($"Room long description: {id}: original \"{origLong}\", port \"{Norm(p.DescriptionLong)}\"");

            if (o.Light != p.HasLight)
                diffs.Add($"Room light: {id}: original {(o.Light ? "lit" : "dark")}, port {(p.HasLight ? "lit" : "dark")}");
            if (o.Value != p.VisitScore)
                diffs.Add($"Room visit score: {id}: original {o.Value}, port {p.VisitScore}");
            if (o.Action != null && p.RoomAction == null && p.OnEnter == null)
                diffs.Add($"Room action missing: {id}: {o.Action}");

            CompareExits(id, o, p, portRooms, diffs);
        }
    }

    private static void CompareExits(string id, OriginalRoom o, Room p, Dictionary<string, Room> portRooms, List<string> diffs)
    {
        foreach (var dir in o.Exits.Keys.Union(p.Exits.Keys).Order(StringComparer.Ordinal))
        {
            var key = $"{id} {dir}";
            var hasOrig = o.Exits.TryGetValue(dir, out var oe);
            var hasPort = p.Exits.TryGetValue(dir, out var pe);
            if (!hasOrig)
            {
                diffs.Add($"Exit not in original: {key} -> {DescribePort(pe)}");
                continue;
            }
            if (!hasPort)
            {
                diffs.Add($"Exit missing: {key} -> {DescribeOrig(oe!)}");
                continue;
            }
            if (oe!.Raw != null)
            {
                diffs.Add($"Exit uses a form this check can't read: {key}: {oe.Raw}");
                continue;
            }
            if (!pe.IsBlocked && pe.RoomId != null && !portRooms.ContainsKey(Id5(pe.RoomId)))
            {
                diffs.Add($"Exit target is not a room: {key}: \"{pe.RoomId}\"");
                continue;
            }

            var portFlag = pe.ConditionFlag?.Replace("!-FLAG", "", StringComparison.Ordinal);
            if (oe.RoomId == null)
            {
                // #NEXIT: never passable, prints the message.
                if (!pe.IsBlocked)
                    diffs.Add($"Exit should be blocked: {key}: original \"{Norm(oe.Message)}\", port {DescribePort(pe)}");
                else if (Norm(oe.Message) != Norm(pe.BlockedMessage))
                    diffs.Add($"Exit message: {key}: original \"{Norm(oe.Message)}\", port \"{Norm(pe.BlockedMessage)}\"");
            }
            else if (pe.IsBlocked || Id5(pe.RoomId) != Id5(oe.RoomId))
            {
                diffs.Add($"Exit target: {key}: original {DescribeOrig(oe)}, port {DescribePort(pe)}");
            }
            else if (oe.Flag != portFlag)
            {
                diffs.Add($"Exit condition: {key}: original {oe.Flag ?? "none"}, port {portFlag ?? "none"}");
            }
            else if (!string.IsNullOrEmpty(oe.Message) && Norm(oe.Message) != Norm(pe.BlockedMessage))
            {
                diffs.Add($"Exit message: {key}: original \"{Norm(oe.Message)}\", port \"{Norm(pe.BlockedMessage)}\"");
            }
        }
    }

    private static string DescribeOrig(OriginalExit e) =>
        e.Raw ?? (e.RoomId == null
            ? $"blocked \"{Norm(e.Message)}\""
            : e.RoomId + (e.Flag != null ? $" if {e.Flag}" : ""));

    private static string DescribePort(ExitTarget e) =>
        e.IsBlocked
            ? $"blocked \"{Norm(e.BlockedMessage)}\""
            : e.RoomId + (e.ConditionFlag != null ? $" if {e.ConditionFlag.Replace("!-FLAG", "", StringComparison.Ordinal)}" : "");

    // ---------------- Objects ----------------

    private static void CompareObjects(OriginalDungeon original, World world, List<string> diffs)
    {
        var portObjects = world.Objects.ToDictionary(o => Id5(o.Id), StringComparer.Ordinal);
        var origIds = original.Objects.Select(o => Id5(o.Id)).ToHashSet(StringComparer.Ordinal);
        var origLocations = OriginalLocations(original);

        foreach (var extra in portObjects.Keys.Where(k => !origIds.Contains(k)))
            diffs.Add($"Object not in original: {extra} ({portObjects[extra].DescriptionShort})");

        foreach (var o in original.Objects)
        {
            var id = Id5(o.Id);
            if (!portObjects.TryGetValue(id, out var p))
            {
                diffs.Add($"Object missing: {id} ({Norm(o.ShortDescription)})");
                continue;
            }

            if (Norm(o.ShortDescription) != Norm(p.DescriptionShort))
                diffs.Add($"Object name: {id}: original \"{Norm(o.ShortDescription)}\", port \"{Norm(p.DescriptionShort)}\"");
            if (o.Action != null && p.ObjectAction == null)
                diffs.Add($"Object action missing: {id}: {o.Action}");

            CompareFlags(id, o, p, diffs);

            var missingSyn = o.Synonyms.Select(Id5).Where(s => !p.Synonyms.Select(Id5).Contains(s)).ToList();
            if (missingSyn.Count > 0)
                diffs.Add($"Object synonyms missing: {id}: {string.Join(" ", missingSyn)}");

            var portLoc = PortLocation(p);
            var origLoc = origLocations.TryGetValue(id, out var l) ? l : new SortedSet<string>(StringComparer.Ordinal);
            if (!origLoc.SetEquals(portLoc))
                diffs.Add($"Object start location: {id}: original {Where(origLoc)}, port {Where(portLoc)}");

            if (o.IsFullObject)
                CompareFullObject(id, o, p, diffs);
        }
    }

    private static void CompareFullObject(string id, OriginalObject o, GameObject p, List<string> diffs)
    {
        if (Norm(o.DescriptionHere) != Norm(p.DescriptionHere))
            diffs.Add($"Object description: {id}: original \"{Norm(o.DescriptionHere)}\", port \"{Norm(p.DescriptionHere)}\"");
        if (Norm(o.DescriptionUntouched) != Norm(p.DescriptionUntouched))
            diffs.Add($"Object untouched description: {id}: original \"{Norm(o.DescriptionUntouched)}\", port \"{Norm(p.DescriptionUntouched)}\"");
        if (o.Light != p.LightAmount)
            diffs.Add($"Object light: {id}: original {o.Light}, port {p.LightAmount}");
        if (o.Find != p.FindScore)
            diffs.Add($"Object find score: {id}: original {o.Find}, port {p.FindScore}");
        if (o.Trophy != p.TrophyScore)
            diffs.Add($"Object trophy score: {id}: original {o.Trophy}, port {p.TrophyScore}");
        if (!SizeMatches(o.Size, p.Size))
            diffs.Add($"Object size: {id}: original {Size(o.Size)}, port {p.Size}");
        if (!SizeMatches(o.Capacity, p.Capacity))
            diffs.Add($"Object capacity: {id}: original {Size(o.Capacity)}, port {p.Capacity}");
    }

    private static void CompareFlags(string id, OriginalObject o, GameObject p, List<string> diffs)
    {
        var want = ObjectFlags.None;
        foreach (var bit in o.Flags)
        {
            if (FlagMap.TryGetValue(bit, out var f))
                want |= f;
            else
                diffs.Add($"Object bit not modelled: {id}: {bit}");
        }
        var have = p.Flags & ~ObjectFlags.Fighting;
        if (want != have)
        {
            var missing = FlagNames(want & ~have);
            var extra = FlagNames(have & ~want);
            diffs.Add($"Object flags: {id}:" + (missing.Length > 0 ? " missing " + missing : "") + (extra.Length > 0 ? " extra " + extra : ""));
        }
    }

    private static string FlagNames(ObjectFlags flags) =>
        string.Join(" ", Enum.GetValues<ObjectFlags>().Where(f => f != ObjectFlags.None && flags.HasFlag(f)));

    private static bool SizeMatches(int original, int port) =>
        original == OriginalDungeon.BigFix ? port >= PortBigSize : original == port;

    private static string Size(int value) => value == OriginalDungeon.BigFix ? "BIGFIX" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Start locations from room object lists, container fields and container contents.</summary>
    private static Dictionary<string, SortedSet<string>> OriginalLocations(OriginalDungeon original)
    {
        var loc = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        void Add(string obj, string where)
        {
            if (!loc.TryGetValue(Id5(obj), out var set)) loc[Id5(obj)] = set = new SortedSet<string>(StringComparer.Ordinal);
            set.Add(where);
        }
        foreach (var r in original.Rooms)
            foreach (var o in r.Objects) Add(o, "room " + Id5(r.Id));
        foreach (var o in original.Objects)
        {
            if (o.Container != null) Add(o.Id, "in " + Id5(o.Container));
            foreach (var c in o.Contents) Add(c, "in " + Id5(o.Id));
        }
        return loc;
    }

    private static SortedSet<string> PortLocation(GameObject p)
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        if (p.InRoom != null) set.Add("room " + Id5(p.InRoom.Id));
        if (p.Container != null) set.Add("in " + Id5(p.Container.Id));
        return set;
    }

    private static string Where(SortedSet<string> set) => set.Count == 0 ? "nowhere" : string.Join(" + ", set);

    // ---------------- Vocabulary ----------------

    private static void CompareVocabulary(OriginalDungeon original, List<string> diffs)
    {
        foreach (var verb in original.Verbs)
        {
            var unknown = verb.Words.Where(w => !ParserKnows(w)).Select(Id5).Distinct(StringComparer.Ordinal).ToList();
            if (unknown.Count > 0)
                diffs.Add($"Verb not understood: {Id5(verb.Name)}: {string.Join(" ", unknown)}");
        }
        foreach (var dir in original.Directions.Where(d => !ParserKnows(d)))
            diffs.Add($"Direction not understood: {Id5(dir)}");
    }

    /// <summary>True when the parser recognises the word, i.e. doesn't answer "I don't know the word".</summary>
    private static bool ParserKnows(string word)
    {
        var output = new StringWriter();
        var state = new GameState { Output = output };
        _ = new World(state);
        Parser.Execute(state, word);
        return !output.ToString().Contains("I don't know the word", StringComparison.Ordinal);
    }

    // ---------------- Helpers ----------------

    private static string Id5(string? id) => (id ?? "").ToUpperInvariant() is var u && u.Length > 5 ? u[..5] : (id ?? "").ToUpperInvariant();

    /// <summary>Collapse whitespace; drop MDL's leading "." (no-article marker, e.g. "\.lunch").</summary>
    private static string Norm(string? text) => Whitespace().Replace(text ?? "", " ").Trim().TrimStart('.');

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
