using System.Globalization;

namespace ZorkDotNet.Tests.Original;

/// <summary>
/// An exit in dung.56. RoomId: plain exit, or the target of a #CEXIT. Message: #NEXIT text, or the #CEXIT
/// "else" text (may be null). Flag: #CEXIT condition flag without the "!-FLAG" suffix. Raw: unsupported form.
/// </summary>
internal sealed record OriginalExit(string? RoomId, string? Message, string? Flag, string? Raw = null);

internal sealed record OriginalRoom(
    string Id,
    string LongDescription,
    string ShortDescription,
    bool Light,
    IReadOnlyDictionary<string, OriginalExit> Exits,
    IReadOnlyList<string> Objects,
    string? Action,
    int Value);

/// <summary>An object from #OBJECT {...} (Kind "OBJECT"), &lt;AOBJECT ...&gt; or &lt;SOBJECT ...&gt;.</summary>
internal sealed record OriginalObject(
    string Id,
    string Kind,
    string? DescriptionHere,
    string ShortDescription,
    string? DescriptionUntouched,
    string? Action,
    IReadOnlyList<string> Contents,
    string? Container,
    IReadOnlyList<string> Flags,
    int Light,
    int Find,
    int Trophy,
    int Size,
    int Capacity,
    IReadOnlyList<string> Synonyms)
{
    public bool IsFullObject => Kind == "OBJECT";
}

/// <summary>A verb from ADD-ACTION / 1ADD-ACTION with its VSYNONYM words (Words includes Name).</summary>
internal sealed record OriginalVerb(string Name, IReadOnlyList<string> Words);

/// <summary>dung.56 parsed into rooms, objects, verbs and directions.</summary>
internal sealed class OriginalDungeon
{
    /// <summary>MDL BIGFIX ("too big to carry"); used for size/capacity.</summary>
    public const int BigFix = int.MaxValue;

    private readonly Dictionary<string, MdlNode> _globals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OriginalRoom> _rooms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OriginalObject> _objects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _verbs = new(StringComparer.Ordinal);
    private readonly List<string> _directions = new();

    public IReadOnlyCollection<OriginalRoom> Rooms => _rooms.Values;
    public IReadOnlyCollection<OriginalObject> Objects => _objects.Values;
    public IEnumerable<OriginalVerb> Verbs => _verbs.Select(v => new OriginalVerb(v.Key, v.Value));
    public IReadOnlyList<string> Directions => _directions;

    public static OriginalDungeon Load(string path) => Parse(File.ReadAllText(path, System.Text.Encoding.Latin1));

    public static OriginalDungeon Parse(string text)
    {
        var nodes = MdlReader.ReadAll(text);
        var d = new OriginalDungeon();
        foreach (var n in nodes) d.CollectGlobals(n);
        foreach (var n in nodes) d.Walk(n);
        return d;
    }

    // ---------------- Tree walking ----------------

    private void CollectGlobals(MdlNode node)
    {
        if (node is MdlSeq { Kind: MdlSeqKind.Form, Head: "SETG" or "PSETG" } s && s.Items.Count >= 3 && s.Items[1] is MdlAtom name)
            _globals.TryAdd(name.Name, s.Items[2]);
        foreach (var child in Children(node)) CollectGlobals(child);
    }

    private void Walk(MdlNode node)
    {
        switch (node)
        {
            case MdlHash { Type: "ROOM", Value: MdlSeq room }:
                AddRoom(room.Items);
                return;
            case MdlHash { Type: "OBJECT", Value: MdlSeq obj }:
                AddFullObject(obj.Items, Array.Empty<string>());
                return;
            case MdlSeq { Kind: MdlSeqKind.Form, Head: "ADD-OBJECT" } add when add.Items.Count >= 2:
                var synonyms = add.Items.Count > 2 && add.Items[2] is MdlSeq { Kind: MdlSeqKind.Vector } v
                    ? v.Items.OfType<MdlString>().Select(x => x.Value).ToList()
                    : new List<string>();
                if (add.Items[1] is MdlHash { Type: "OBJECT", Value: MdlSeq full })
                    AddFullObject(full.Items, synonyms);
                else if (add.Items[1] is MdlSeq { Kind: MdlSeqKind.Form, Head: "SOBJECT" or "AOBJECT" } simple)
                    AddSimpleObject(simple, synonyms);
                else
                    Walk(add.Items[1]);
                return;
            case MdlSeq { Kind: MdlSeqKind.Form, Head: "SOBJECT" or "AOBJECT" } simpleObj:
                AddSimpleObject(simpleObj, Array.Empty<string>());
                return;
            case MdlSeq { Kind: MdlSeqKind.Form, Head: "ADD-ACTION" or "1ADD-ACTION" } act when act.Items.Count > 1 && act.Items[1] is MdlString verb:
                if (!_verbs.ContainsKey(verb.Value)) _verbs[verb.Value] = new List<string> { verb.Value };
                return;
            case MdlSeq { Kind: MdlSeqKind.Form, Head: "VSYNONYM" } vs when vs.Items.Count > 1 && vs.Items[1] is MdlString vname:
                if (!_verbs.TryGetValue(vname.Value, out var words)) _verbs[vname.Value] = words = new List<string> { vname.Value };
                words.AddRange(vs.Items.Skip(2).OfType<MdlString>().Select(x => x.Value));
                return;
            case MdlSeq { Kind: MdlSeqKind.Form, Head: "ADD-DIRECTIONS" or "DSYNONYM" } dirs:
                _directions.AddRange(dirs.Items.Skip(1).OfType<MdlString>().Select(x => x.Value).Where(x => x != "#!#!#"));
                return;
        }
        foreach (var child in Children(node)) Walk(child);
    }

    private static IEnumerable<MdlNode> Children(MdlNode node) => node switch
    {
        MdlSeq s => s.Items,
        MdlHash h => new[] { h.Value },
        MdlPrefix p => new[] { p.Value },
        _ => Array.Empty<MdlNode>(),
    };

    // ---------------- Definitions ----------------

    private void AddRoom(IReadOnlyList<MdlNode> f)
    {
        MdlNode? At(int i) => i < f.Count ? f[i] : null;
        var id = ((MdlString)f[0]).Value;
        var exits = At(4) is MdlHash { Type: "EXIT", Value: MdlSeq ex } ? ParseExits(ex.Items) : new Dictionary<string, OriginalExit>();
        var objects = At(5) is MdlSeq { Kind: MdlSeqKind.List } objs
            ? objs.Items.Select(ObjectRef).OfType<string>().ToList()
            : new List<string>();
        _rooms.TryAdd(id, new OriginalRoom(
            id,
            Text(At(1)) ?? "",
            Text(At(2)) ?? "",
            At(3) is MdlAtom { Name: "T" },
            exits,
            objects,
            Action(At(6)),
            Number(At(7))));
    }

    private Dictionary<string, OriginalExit> ParseExits(IReadOnlyList<MdlNode> items)
    {
        var exits = new Dictionary<string, OriginalExit>(StringComparer.Ordinal);
        for (var i = 0; i + 1 < items.Count; i += 2)
        {
            if (items[i] is MdlString dir)
                exits[dir.Value] = ParseExit(items[i + 1]);
        }
        return exits;
    }

    private OriginalExit ParseExit(MdlNode target)
    {
        var resolved = Resolve(target);
        switch (resolved)
        {
            case MdlString room:
                return new OriginalExit(room.Value, null, null);
            case MdlHash { Type: "NEXIT" } nexit:
                return new OriginalExit(null, Text(nexit.Value) ?? "", null);
            case MdlHash { Type: "CEXIT", Value: MdlSeq c } when c.Items.Count >= 2:
                return new OriginalExit(Text(c.Items[1]), c.Items.Count > 2 ? Text(c.Items[2]) : null, Text(c.Items[0]));
            default:
                return new OriginalExit(null, null, null, Raw: Describe(target));
        }
    }

    private void AddFullObject(IReadOnlyList<MdlNode> f, IReadOnlyList<string> synonyms)
    {
        MdlNode? At(int i) => i < f.Count ? f[i] : null;
        var id = ((MdlString)f[0]).Value;
        var contents = At(5) is MdlSeq { Kind: MdlSeqKind.List } c ? c.Items.Select(ObjectRef).OfType<string>().ToList() : new List<string>();
        AddObject(new OriginalObject(
            id, "OBJECT",
            Text(At(1)), Text(At(2)) ?? "", Text(At(3)), Action(At(4)),
            contents, ObjectRef(At(6)), Flags(At(7)),
            Number(At(8)), Number(At(9)), Number(At(10)), Number(At(11)), Number(At(12)),
            synonyms));
    }

    private void AddSimpleObject(MdlSeq form, IReadOnlyList<string> synonyms)
    {
        // <SOBJECT "ID" desc flags...> / <AOBJECT "ID" desc ACTION flags...>
        var items = form.Items;
        var id = ((MdlString)items[1]).Value;
        var hasAction = form.Head == "AOBJECT";
        var flags = items.Skip(hasAction ? 4 : 3).SelectMany(Flags).ToList();
        AddObject(new OriginalObject(
            id, form.Head!,
            null, Text(items[2]) ?? "", null, hasAction ? Action(items[3]) : null,
            Array.Empty<string>(), null, flags,
            0, 0, 0, BigFix, 0, synonyms));
    }

    private void AddObject(OriginalObject obj)
    {
        if (!_objects.TryGetValue(obj.Id, out var existing))
            _objects[obj.Id] = obj;
        else if (existing.Synonyms.Count == 0 && obj.Synonyms.Count > 0)
            _objects[obj.Id] = existing with { Synonyms = obj.Synonyms };
    }

    // ---------------- Value helpers ----------------

    /// <summary>Follow ,NAME and %,NAME references to the global's value.</summary>
    private MdlNode? Resolve(MdlNode? node)
    {
        for (var depth = 0; depth < 10 && node != null; depth++)
        {
            if (node is MdlPrefix { Prefix: '%', Value: MdlPrefix inner }) node = inner;
            if (node is MdlPrefix { Prefix: ',', Value: MdlAtom g } && _globals.TryGetValue(g.Name, out var value))
                node = value;
            else
                return node;
        }
        return node;
    }

    private static bool IsFalse(MdlNode? node) =>
        node is null or MdlPrefix { Prefix: '%', Value: MdlSeq { Kind: MdlSeqKind.Form, Items.Count: 0 } } or MdlSeq { Kind: MdlSeqKind.Form, Items.Count: 0 };

    private string? Text(MdlNode? node)
    {
        var r = Resolve(node);
        return IsFalse(r) ? null : r switch
        {
            MdlString s => s.Value,
            MdlAtom a => a.Name,
            _ => Describe(r!),
        };
    }

    private int Number(MdlNode? node)
    {
        if (IsReference(node, "BIGFIX")) return BigFix;
        return Resolve(node) is MdlAtom a && int.TryParse(a.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static bool IsReference(MdlNode? node, string global) =>
        (node is MdlPrefix { Prefix: '%', Value: MdlPrefix inner } ? inner : node) is MdlPrefix { Prefix: ',', Value: MdlAtom { Name: var name } }
        && name == global;

    private static string? Action(MdlNode? node) =>
        node is MdlAtom a && !int.TryParse(a.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? a.Name : null;

    /// <summary>Flag bits: ,BIT / %,BIT / %&lt;+ ,A ,B&gt; / &lt;+ ,A ,B&gt;.</summary>
    private static List<string> Flags(MdlNode? node)
    {
        if (node is MdlPrefix { Prefix: '%', Value: var inner }) node = inner;
        return node switch
        {
            MdlPrefix { Prefix: ',', Value: MdlAtom a } => new List<string> { a.Name },
            MdlSeq { Kind: MdlSeqKind.Form, Head: "+" } plus => plus.Items.Skip(1).SelectMany(Flags).ToList(),
            _ => new List<string>(),
        };
    }

    /// <summary>#FIND-OBJ {"ID"} or %&lt;FIND-OBJ "ID"&gt;.</summary>
    private static string? ObjectRef(MdlNode? node)
    {
        if (node is MdlPrefix { Prefix: '%', Value: var inner }) node = inner;
        return node switch
        {
            MdlHash { Type: "FIND-OBJ", Value: MdlSeq { Items: [MdlString id, ..] } } => id.Value,
            MdlSeq { Kind: MdlSeqKind.Form, Head: "FIND-OBJ", Items: [_, MdlString id, ..] } => id.Value,
            _ => null,
        };
    }

    private static string Describe(MdlNode node) => node switch
    {
        MdlString s => "\"" + s.Value + "\"",
        MdlAtom a => a.Name,
        MdlPrefix p => p.Prefix + Describe(p.Value),
        MdlHash h => "#" + h.Type + " " + Describe(h.Value),
        MdlSeq s => s.Kind switch
        {
            MdlSeqKind.List => "(",
            MdlSeqKind.Vector => "[",
            MdlSeqKind.Brace => "{",
            _ => "<",
        } + string.Join(" ", s.Items.Select(Describe)) + s.Kind switch
        {
            MdlSeqKind.List => ")",
            MdlSeqKind.Vector => "]",
            MdlSeqKind.Brace => "}",
            _ => ">",
        },
        _ => node.ToString(),
    };
}
