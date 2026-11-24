namespace ZorkDotNet.Game;

/// <summary>
/// Represents an object (MDL OBJECT: OID, ONAMES, ODESC1, ODESC2, ODESCO, OACTION, OCONTENTS, OCAN, OFLAGS, ...).
/// </summary>
public class GameObject
{
    public string Id { get; set; } = "";
    public List<string> Synonyms { get; set; } = new();
    /// <summary>MDL OADJS: adjectives that tell this object apart from others with the same name ("YELLOW BUTTON").</summary>
    public List<string> Adjectives { get; set; } = new();
    /// <summary>MDL ADD-STAR: a global object, within reach in every room (the walls, your hands, the sailor).</summary>
    public bool IsGlobal { get; set; }
    /// <summary>Description when in room / not carried.</summary>
    public string DescriptionHere { get; set; } = "";
    /// <summary>Short description (e.g. "lamp", "sword").</summary>
    public string DescriptionShort { get; set; } = "";
    public string? DescriptionUntouched { get; set; }
    /// <summary>MDL OREAD: the text READ prints (from <see cref="ReadTexts"/>).</summary>
    public string? ReadText { get; set; }
    public Action<GameState, string, GameObject?>? ObjectAction { get; set; }
    /// <summary>What is inside this object (MDL OCONTENTS). Changed only by <see cref="MoveTo"/>.</summary>
    public IReadOnlyList<GameObject> Contents => Items;
    internal List<GameObject> Items { get; } = new();
    /// <summary>Where this object is (MDL OROOM / OCAN / an adventurer's AOBJS); null when it is nowhere.</summary>
    public Place? Location { get; private set; }
    /// <summary>The object this is inside (MDL OCAN), or null.</summary>
    public GameObject? Container => Location is GameObject c ? c : null;
    public ObjectFlags Flags { get; set; }
    public bool Touched { get; set; }
    public int LightAmount { get; set; }
    public int FindScore { get; set; }
    public int TrophyScore { get; set; }
    public bool Open { get; set; }
    public int Size { get; set; } = 5;
    public int Capacity { get; set; }
    /// <summary>The room whose floor this is on (MDL OROOM), or null.</summary>
    public Room? InRoom => Location is Room r ? r : null;
    /// <summary>Adventurer carrying this directly (null if in a room or a container).</summary>
    public Adventurer? Carrier => Location is Adventurer a ? a : null;

    /// <summary>
    /// The only way an object changes place: it leaves its old place's list and joins the new one (at the front when
    /// <paramref name="first"/>, as MDL's PUT-ing at the head of a list). Null removes it from the game.
    /// </summary>
    public void MoveTo(Place? to, bool first = false)
    {
        if (Location is { } from) ListOf(from).Remove(this);
        Location = to;
        if (to is not { } place) return;
        var list = ListOf(place);
        if (first) list.Insert(0, this);
        else list.Add(this);
    }

    /// <summary>
    /// dung.56 quirk: the balloon label's OCAN is the balloon, but it is not in the balloon's OCONTENTS until the
    /// balloon is first inflated (then <see cref="MoveTo"/> adds it). Used only for that.
    /// </summary>
    internal void SetUnlistedContainer(GameObject container) => Location = container;

    /// <summary>
    /// Puts this on the floor of <paramref name="room"/>, as a ROBJS listing does when the world is built or a game
    /// restored. An object already in a room stays there and is listed in this room too (dung.56 scenery shared by
    /// several rooms).
    /// </summary>
    internal void PlaceAlsoIn(Room room)
    {
        if (InRoom is { } home && home != room) room.AddScenery(this);
        else if (InRoom != room) MoveTo(room);
    }

    /// <summary>The adventurer carrying this, directly or inside something they carry (MDL AOBJS reach).</summary>
    public Adventurer? Holder
    {
        get
        {
            for (var o = this; o != null; o = o.Container)
                if (o.Carrier is { } a) return a;
            return null;
        }
    }

    /// <summary>The room this is in, directly or inside something on the floor; null if carried or nowhere.</summary>
    public Room? OutermostRoom
    {
        get
        {
            for (var o = this; o != null; o = o.Container)
                if (o.InRoom is { } r) return r;
            return null;
        }
    }

    /// <summary>
    /// rooms.99 WEIGHT: the sizes of the objects and of everything inside them. A BIGFIX size counts nothing (the port
    /// stores BIGFIX as <see cref="Adventurer.MaxLoad"/>).
    /// </summary>
    public static int Weight(IEnumerable<GameObject> objects) =>
        objects.Sum(o => (o.Size >= Adventurer.MaxLoad ? 0 : o.Size) + Weight(o.Contents));

    private static List<GameObject> ListOf(Place place) => place switch
    {
        Room r => r.Items,
        Adventurer a => a.Items,
        GameObject c => c.Items,
    };

    public bool CanTake => (Flags & ObjectFlags.Take) != 0;
    public bool IsVisible => (Flags & ObjectFlags.Visible) != 0;
    public bool IsContainer => (Flags & ObjectFlags.Container) != 0;
    public bool IsTransparent => (Flags & ObjectFlags.Transparent) != 0;
    public bool IsOpenOrTransparent => Open || IsTransparent;
    public bool ProvidesLight => LightAmount > 0;
    /// <summary>MDL FIGHTBIT: in melee (takes damage each round).</summary>
    public bool Fighting => (Flags & ObjectFlags.Fighting) != 0;
    /// <summary>MDL VILLAIN: troll, thief, cyclops.</summary>
    public bool IsVillain => (Flags & ObjectFlags.Villain) != 0;
    /// <summary>MDL ACTORBIT: an actor (the robot), which can be told to do things and carries things.</summary>
    public bool IsActor => (Flags & ObjectFlags.Actor) != 0;
}

[Flags]
public enum ObjectFlags
{
    None = 0,
    Visible = 1 << 0,
    Readable = 1 << 1,
    Take = 1 << 2,
    Door = 1 << 3,
    Transparent = 1 << 4,
    Food = 1 << 5,
    NoDescribe = 1 << 6,
    Drinkable = 1 << 7,
    Container = 1 << 8,
    Light = 1 << 9,
    Burnable = 1 << 10,
    OnFire = 1 << 11,
    Tool = 1 << 12,
    Turnable = 1 << 13,
    Vehicle = 1 << 14,
    Weapon = 1 << 15,
    Sacred = 1 << 16,
    /// <summary>MDL FIGHTBIT: in melee.</summary>
    Fighting = 1 << 17,
    /// <summary>MDL VILLAIN: villain (troll, thief, cyclops).</summary>
    Villain = 1 << 18,
    /// <summary>MDL VICBIT: something that can be the victim of a verb (attacked, given to, woken).</summary>
    Victim = 1 << 19,
    /// <summary>MDL TRYTAKEBIT: its own action decides about TAKE (even TAKE ALL tries it).</summary>
    TryTake = 1 << 20,
    /// <summary>MDL SEARCHBIT: something to search (the brick, the balloon's receptacle).</summary>
    Search = 1 << 21,
    /// <summary>MDL FINDMEBIT: holds something to be found (the buoy and its emerald).</summary>
    FindMe = 1 << 22,
    /// <summary>MDL TIEBIT: can be tied (the rope, the braided wire).</summary>
    Tie = 1 << 23,
    /// <summary>MDL ACTORBIT: an actor (the robot).</summary>
    Actor = 1 << 24,
    /// <summary>MDL NO-CHECK-BIT: TAKE and PUT hand the whole command to the object's action ("all", "valuables").</summary>
    NoCheck = 1 << 25,
}
