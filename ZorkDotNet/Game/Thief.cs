namespace ZorkDotNet.Game;

/// <summary>
/// The thief (act1.38 ROBBER, ROBBER-FUNCTION, SNARF-OBJECT, CHALICE, TREASURE-ROOM; defs.63 ROB-ROOM, ROB-ADV). His
/// demon walks the dungeon one room per move in the order of the original's ROOMS list, skipping the sacred rooms;
/// he robs rooms you have seen, and where you are he may appear, rob you blind and leave. What he takes goes into
/// his bag (<see cref="RobberState.Bag"/>, MDL HOBJS); treasures end up in his hideaway, the Treasure Room.
/// Fighting him uses the port's melee (<see cref="Combat"/>): the original's melee code isn't in the MIT archive.
/// </summary>
public static class Thief
{
    /// <summary>
    /// The original's ROOMS list: MDL adds each room at the front when it is first mentioned, so this is the reverse of
    /// the order rooms first appear in dung.56 (where the order of the original's files can't be told, it is a best
    /// reconstruction).
    /// </summary>
    public static readonly string[] Rooms =
    [
        "ALITR", "ALISM", "TWELL", "CAGED", "CAGER", "MAGNE", "ALICE", "CMACH", "SAFE", "LIBRA", "LEDG4", "VAIR4",
        "VAIR3", "LEDG2", "VAIR2", "VAIR1", "VLBOT", "CLMID", "CLBOT", "POG", "BARRE", "FALLS", "FANTE", "FCHMP",
        "BEACH", "RIVR5", "WCLF2", "RCAVE", "WCLF1", "RIVR4", "RIVR3", "RIVR2", "RIVR1", "RAINB", "TREAS", "MAINT",
        "LOBBY", "DOCK", "TEMP2", "TEMP1", "TOMB", "LLD2", "BWELL", "MPEAR", "RIDDL", "DEAD6", "DEAD5", "TCAVE",
        "CAVE4", "GALLE", "STUDI", "MTORC", "BSHAF", "TIMBE", "DEAD7", "MINE6", "MINE3", "MINE5", "MINE2", "MINE4",
        "MINE7", "BLADD", "TLADD", "BOOM", "MINE1", "SMELL", "TUNNE", "BATS", "TSHAF", "SQUEE", "ENTRA", "SLIDE",
        "MGRAI", "LLD1", "CAVE2", "CRAW3", "PASS4", "MIRR2", "CRAW2", "PASS3", "MIRR1", "CAVE3", "PASS5", "CHAS3",
        "ECHO", "DAM", "CAVE1", "LAVA", "RUBYR", "LEDG3", "ICY", "ATLAN", "CANY1", "RESEN", "STREA", "EGYPT", "DOME",
        "CRAW1", "CHAS1", "RESES", "FORE5", "CLTOP", "FORE4", "DEAD4", "MGRAT", "MAZ12", "MAZ13", "MAZ10", "MAZ11",
        "DEAD3", "MAZ15", "MAZE8", "MAZ14", "MAZE9", "MAZE7", "MAZE6", "DEAD2", "DEAD1", "MAZE5", "MAZE3", "MAZE4",
        "MAZE2", "CYCLO", "MAZE1", "CRAW4", "CHAS2", "CELLA", "BLROO", "MACHI", "ATTIC", "LROOM", "KITCH", "CLEAR",
        "FORE2", "FORE3", "EHOUS", "FORE1", "SHOUS", "NHOUS", "WHOUS", "RAVI1", "MTROL", "CAROU", "PASS1",
    ];

    /// <summary>dung.56 RSACREDBIT: rooms the thief never works in.</summary>
    private static readonly HashSet<string> Sacred =
    [
        "BSHAF", "RIVR1", "DOCK", "FANTE", "FALLS", "BEACH", "RCAVE", "VAIR1", "VAIR2", "VAIR3", "VAIR4", "RIVR2", "RIVR3",
        "RIVR4", "RIVR5", "TIMBE", "WHOUS", "NHOUS", "EHOUS", "SHOUS", "KITCH", "LROOM", "FORE1", "FORE2", "FORE3",
        "FORE4", "FORE5", "CLEAR", "TEMP1", "TEMP2", "CLTOP", "CLMID", "CLBOT", "RAINB",
    ];

    private const string MazeDesc = "You are in a maze of twisty little passages, all alike.";

    private static bool Prob(GameState state, int percent) => state.Random.Next(100) < percent;

    private static void Remove(GameObject o) => o.MoveTo(null);

    private static void Insert(GameObject o, Room room) => o.MoveTo(room);

    /// <summary>act1.38 SNARF-OBJECT: the thief picks up his stiletto (or anything) wherever it lies.</summary>
    private static void Snarf(GameObject who, GameObject what)
    {
        if (what.Container == who || (what.InRoom == null && what.Container == null)) return;
        what.MoveTo(who, first: true);
    }

    private static void Bag(GameState state, GameObject o)
    {
        Remove(o);
        o.Touched = true;
        state.Robber.Bag.Insert(0, o.Id);
    }

    private static bool Valuable(GameObject o) => o.TrophyScore > 0 && (o.Flags & ObjectFlags.Sacred) == 0;

    /// <summary>defs.63 ROB-ROOM (and ROB-ADV for an actor there, the robot).</summary>
    private static void RobRoom(GameState state, Room room, int percent)
    {
        foreach (var o in room.Objects.ToList())
        {
            if (Valuable(o) && o.IsVisible && Prob(state, percent)) Bag(state, o);
            else if (o.Id == Obj.Robot) RobAdventurer(state, state.Robot);
        }
    }

    /// <summary>defs.63 ROB-ADV: every treasure an adventurer carries.</summary>
    private static void RobAdventurer(GameState state, Adventurer who)
    {
        foreach (var o in who.Inventory.Where(Valuable).ToList()) Bag(state, o);
    }

    /// <summary>
    /// The melee's WINNING? (not in the archive): here the thief is winning while he has more than one blow left in
    /// him.
    /// </summary>
    private static bool Winning(GameState state) =>
        !state.VillainStrength.TryGetValue("THIEF", out var strength) || strength > 1;

    /// <summary>act1.38 ROBBER: the thief's demon, run every move.</summary>
    public static void Demon(GameState state)
    {
        if (!state.Robber.Active || state.World == null) return;
        var world = state.World;
        var thief = world.FindObject(Obj.Thief)!;
        var still = world.FindObject(Obj.Still)!;
        var treas = world.FindRoom(RoomId.Treas)!;
        var wroom = state.Here;
        var rm = world.FindRoom(state.Robber.Room)!;
        var seen = rm.Seen;
        var once = false;
        while (true)
        {
            Room? here = thief.InRoom;
            if (here != null) rm = here;
            var before = state.Robber.Bag.ToList();
            bool Robbed() => !before.SequenceEqual(state.Robber.Bag);

            if (rm == treas && rm != wroom)
            {
                if (here != null)
                {
                    if (still.InRoom == treas) Snarf(thief, still);
                    Remove(thief);
                    here = null;
                }
                foreach (var id in state.Robber.Bag.ToList())
                {
                    var o = world.FindObject(id)!;
                    if (o.TrophyScore > 0)
                    {
                        state.Robber.Bag.Remove(id);
                        Insert(o, rm);
                    }
                }
            }
            else if (rm == wroom) // "Adventurer is in room:  CHOMP, CHOMP"
            {
                if (rm == treas) { } // "Don't move, Gertrude"
                else if (!state.Robber.Announced)
                {
                    if (here == null && Prob(state, 30))
                    {
                        if (still.Container == thief)
                        {
                            Insert(thief, rm);
                            state.Output.WriteLine("Someone carrying a large bag is casually leaning against one of the walls here.  He does not speak, but it is clear from his aspect that the bag will be taken only over his dead body.");
                            state.Robber.Announced = true;
                        }
                        return;
                    }
                    if (here != null && thief.Fighting && (!Winning(state) || Prob(state, 90)))
                    {
                        if (!Winning(state))
                        {
                            state.Output.WriteLine("Your opponent, determining discretion to be the better part of valor, decides to terminate this little contretemps.  With a rueful nod of his head, he steps backward into the gloom and disappears.");
                            Remove(thief);
                            thief.Flags &= ~ObjectFlags.Fighting;
                            Snarf(thief, still);
                        }
                        return;
                    }
                    if (here != null && Prob(state, 30))
                    {
                        state.Output.WriteLine("The holder of the large bag just left, looking disgusted. Fortunately, he took nothing.");
                        Remove(thief);
                        Snarf(thief, still);
                        return;
                    }
                    if (Prob(state, 70)) return;
                    if (state.Robber.Bag.Remove(still.Id))
                        still.MoveTo(thief, first: true);
                    RobRoom(state, rm, 100);
                    RobAdventurer(state, state.Winner);
                    state.Robber.Announced = true;
                    if (Robbed() && here == null)
                        state.Output.WriteLine("A seedy-looking individual with a large bag just wandered through the room.  On the way through, he quietly abstracted all valuables from the room and from your possession, mumbling something about \"Doing unto others before..\"");
                    else if (here != null)
                    {
                        Snarf(thief, still);
                        state.Output.WriteLine(Robbed()
                            ? "The other occupant just left, still carrying his large bag.  You may not have noticed that he robbed you blind first."
                            : "The other occupant (he of the large bag), finding nothing of value, left disgusted.");
                        Remove(thief);
                        here = null;
                    }
                    else
                        state.Output.WriteLine("A 'lean and hungry' gentleman just wandered through.  Finding nothing of value, he left disgruntled.");
                }
                else if (here != null) // here, already announced
                {
                    if (!Prob(state, 30)) return;
                    RobRoom(state, rm, 100);
                    RobAdventurer(state, state.Winner);
                    if (state.Robber.Bag.Contains("ROPE")) state.SetFlag(Flag.Dome, false);
                    state.Output.WriteLine(Robbed()
                        ? "The other occupant just left, still carrying his large bag.  You may not have noticed that he robbed you blind first."
                        : "The other occupant (he of the large bag), finding nothing of value, left disgusted.");
                    Remove(thief);
                    here = null;
                    Snarf(thief, still);
                }
            }
            else
            {
                if (thief.InRoom == rm) // leave if the victim left
                {
                    Snarf(thief, still);
                    Remove(thief);
                    here = null;
                }
                if (still.InRoom == rm) Snarf(thief, still);
                if (seen) HackRoom(state, rm, wroom);
            }

            once = !once;
            if (!once) break;
            // Move to the next room of ROOMS where he may work.
            do
            {
                rm = world.FindRoom(Rooms[state.Robber.Next])!;
                state.Robber.Next = (state.Robber.Next + 1) % Rooms.Length;
            }
            while (Sacred.Contains(rm.Id));
            state.Robber.Room = rm.Id;
            state.Robber.Announced = false;
            seen = rm.Seen;
        }

        // Drop worthless cruft, sometimes.
        if (rm == treas) return;
        foreach (var id in state.Robber.Bag.ToList())
        {
            var o = world.FindObject(id)!;
            if (o.TrophyScore == 0 && Prob(state, 30))
            {
                state.Robber.Bag.Remove(id);
                Insert(o, rm);
                if (rm == wroom) state.Output.WriteLine("The robber, rummaging through his bag, dropped a few items he found valueless.");
            }
        }
    }

    /// <summary>ROBBER in a room you have seen but aren't in: he helps himself.</summary>
    private static void HackRoom(GameState state, Room rm, Room wroom)
    {
        RobRoom(state, rm, 75);
        if (rm.DescriptionShort == MazeDesc && wroom.DescriptionShort == MazeDesc)
        {
            foreach (var o in rm.Objects.ToList())
            {
                if (!o.CanTake || !o.IsVisible || !Prob(state, 40)) continue;
                state.Output.WriteLine("You hear, off in the distance, someone saying \"My, I wonder what this fine " + o.DescriptionShort + " is doing here.\"");
                state.Output.WriteLine();
                if (Prob(state, 60)) Bag(state, o);
                break;
            }
            return;
        }
        foreach (var o in rm.Objects.ToList())
        {
            if (o.TrophyScore != 0 || !o.CanTake || !o.IsVisible || !Prob(state, 20)) continue;
            Bag(state, o);
            if (rm == wroom) state.Output.WriteLine("You suddenly notice that the " + o.DescriptionShort + " vanished.");
            break;
        }
        if (state.Robber.Bag.Contains("ROPE")) state.SetFlag(Flag.Dome, false);
    }

    /// <summary>act1.38 ROBBER-FUNCTION: what is done to the thief himself (his melee hooks are in <see cref="Combat"/>).</summary>
    public static void ThiefFunction(GameState state, string verb, GameObject? other)
    {
        var world = state.World!;
        var thief = world.FindObject(Obj.Thief)!;
        if (verb == "THROW" && other?.Id == Obj.Knife && !thief.Fighting)
        {
            if (Prob(state, 10))
            {
                var bag = state.Robber.Bag.ToList();
                if (bag.Count == 0)
                    state.Output.WriteLine("You evidently frightened the robber, though you didn't hit him.  He flees.");
                else
                {
                    foreach (var id in bag) Insert(world.FindObject(id)!, state.Here);
                    state.Robber.Bag.Clear();
                    state.Output.WriteLine("You evidently frightened the robber, though you didn't hit him.  He flees, but the contents of his bag fall on the floor.");
                }
                Remove(thief);
            }
            else
            {
                state.Output.WriteLine("You missed.  The thief makes no attempt to take the knife, though it would be a fine addition to the collection in his bag.  He does seem angered by your attempt.");
                thief.Flags |= ObjectFlags.Fighting;
                state.VillainStrength.TryAdd("THIEF", Combat.ThiefStrength);
            }
        }
        else if (verb is "THROW" or "GIVE" && other != null && other != thief)
        {
            var fuse = world.FindObject(Obj.Fuse)!;
            if (other.Id == Obj.Brick && fuse.Container == other && state.ScheduledEvents.Exists(e => e.Name == Clocks.Fuse))
                state.Output.WriteLine("The thief seems rather offended by your offer.  Do you think he's as stupid as you are?");
            else
            {
                Remove(other);
                state.Robber.Bag.Insert(0, other.Id);
                state.Output.WriteLine("The thief places the " + other.DescriptionShort + " in his bag and thanks you politely.");
            }
        }
        else if (verb == "TAKE")
            state.Output.WriteLine("Once you got him, what would you do with him?");
    }

    /// <summary>
    /// ROBBER-FUNCTION with DEAD!: his booty remains, the chalice can be taken, and in his hideaway the treasures he
    /// hid reappear. His demon stops.
    /// </summary>
    public static void Died(GameState state)
    {
        var world = state.World!;
        var here = state.Here;
        var chalice = world.FindObject(Obj.Chali)!;
        var thief = world.FindObject(Obj.Thief)!;
        var booty = new HashSet<string>(state.Robber.Bag);
        if (booty.Count > 0)
        {
            state.Output.WriteLine("  His booty remains.");
            foreach (var id in state.Robber.Bag) Insert(world.FindObject(id)!, here);
            state.Robber.Bag.Clear();
        }
        chalice.Flags |= ObjectFlags.Take;
        if (here.Id == RoomId.Treas)
        {
            var told = false;
            foreach (var o in here.Objects.Where(o => o != chalice && o != thief && !booty.Contains(o.Id) && !o.IsVisible))
            {
                o.Flags |= ObjectFlags.Visible;
                if (!told)
                {
                    state.Output.WriteLine("As the thief dies, the power of his magic decreases, and his treasures reappear:");
                    told = true;
                }
                state.Output.WriteLine("  A " + o.DescriptionShort);
            }
        }
        state.Robber.Active = false;
    }

    /// <summary>act1.38 CHALICE: while the thief fights for his hideaway, his chalice is out of reach.</summary>
    public static void Chalice(GameState state, string verb, GameObject? _)
    {
        if (verb != "TAKE") return;
        var world = state.World!;
        var chalice = world.FindObject(Obj.Chali)!;
        var thief = world.FindObject(Obj.Thief)!;
        if (chalice.Container == null && chalice.InRoom?.Id == RoomId.Treas && thief.InRoom?.Id == RoomId.Treas && thief.Fighting && state.Robber.Active)
            state.Output.WriteLine("Realizing just in time that you'd be stabbed in the back if you attempted to take the chalice, you return to the fray.");
    }

    /// <summary>
    /// act1.38 TREASURE-ROOM on WALK-IN: the thief rushes to defend his hideaway, the chalice can't be taken while he
    /// does, and his treasures vanish.
    /// </summary>
    public static void TreasureRoom(GameState state, string verb)
    {
        if (verb != "WALK-IN" || !state.Robber.Active) return;
        var world = state.World!;
        var here = state.Here;
        var thief = world.FindObject(Obj.Thief)!;
        if (thief.InRoom != here)
        {
            state.Output.WriteLine("You hear a scream of anguish as you violate the robber's hideaway.  Using passages unknown to you, he rushes to its defense.");
            Insert(thief, here);
            state.Robber.Room = here.Id;
            var at = Array.IndexOf(Rooms, here.Id);
            state.Robber.Next = (at + 1) % Rooms.Length;
        }
        thief.Flags |= ObjectFlags.Fighting;
        state.VillainStrength.TryAdd("THIEF", Combat.ThiefStrength);
        var chalice = world.FindObject(Obj.Chali)!;
        if (chalice.Container == null && chalice.InRoom == here) chalice.Flags &= ~ObjectFlags.Take;
        if (here.Objects.Count > 2)
            state.Output.WriteLine("The thief gestures mysteriously, and the treasures in the room suddenly vanish.");
        foreach (var o in here.Objects.Where(o => o != chalice && o != thief))
            o.Flags &= ~ObjectFlags.Visible;
    }

    public static void TreasureEnter(GameState state) => TreasureRoom(state, "WALK-IN");
}
