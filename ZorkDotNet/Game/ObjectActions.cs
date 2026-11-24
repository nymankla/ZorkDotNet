namespace ZorkDotNet.Game;

/// <summary>
/// Object-action handlers ported from act1.37, act2.27, act3.13 (e.g. WINDOW-FUNCTION, RUG, LANTERN, BOTTLE-FUNCTION).
/// </summary>
public static class ObjectActions
{
    /// <summary>act1.38 DUMMY: replies for doing something that is already done.</summary>
    private static readonly string[] Dummy = ["Look around.", "You think it isn't?", "I think you've already done that."];

    /// <summary>
    /// act1.38 OPEN-CLOSE: OPEN sets the flag (or a DUMMY reply if already set), CLOSE clears it (or DUMMY).
    /// Other verbs do nothing and return false.
    /// </summary>
    private static bool OpenClose(GameState state, string verb, string flag, string openText, string closeText)
    {
        if (verb == "OPEN")
        {
            if (state.GetFlag(flag)) state.Output.WriteLine(Dummy[Random.Shared.Next(Dummy.Length)]);
            else { state.Output.WriteLine(openText); state.SetFlag(flag, true); }
            return true;
        }
        if (verb == "CLOSE")
        {
            if (state.GetFlag(flag)) { state.Output.WriteLine(closeText); state.SetFlag(flag, false); }
            else state.Output.WriteLine(Dummy[Random.Shared.Next(Dummy.Length)]);
            return true;
        }
        return false;
    }

    /// <summary>act1.38 GRAT1-FUNCTION: the grating seen from the clearing.</summary>
    public static void Grating1Function(GameState state, string verb, GameObject? _)
    {
        if (state.GetFlag("GRUNLOCK!-FLAG"))
            OpenClose(state, verb, "KEY-FLAG!-FLAG", "The grating opens.", "The grating is closed.");
        else
            state.Output.WriteLine("The grating is locked.");
    }

    /// <summary>act1.38 GRAT2-FUNCTION: the grating seen from below (MGRAT); working it makes GRAT1 visible above.</summary>
    public static void Grating2Function(GameState state, string verb, GameObject? _)
    {
        if (state.GetFlag("GRUNLOCK!-FLAG"))
        {
            OpenClose(state, verb, "KEY-FLAG!-FLAG", "The grating opens to reveal trees above you.", "The grating is closed.");
            var grat1 = state.World?.FindObject("GRAT1");
            if (grat1 != null) grat1.Flags |= ObjectFlags.Visible;
        }
        else
        {
            state.Output.WriteLine("The grating is locked.");
        }
    }

    /// <summary>
    /// act1.38 UNLOCKER: only the grating from below (GRAT2) can be unlocked, and only with the skeleton keys.
    /// A missing tool is chosen like np.93 GWIM (the single tool carried, else the single tool in the room).
    /// </summary>
    public static void Unlocker(GameState state, GameObject obj, GameObject? tool)
    {
        tool ??= FindOnlyTool(state);
        if (tool == null)
        {
            state.Output.WriteLine("With what?");
            return;
        }
        if (obj.Id != "GRAT2")
        {
            state.Output.WriteLine("It doesn't seem to work.");
            return;
        }
        if (tool.Id != "KEYS")
        {
            state.Output.WriteLine("Can you unlock a grating with a " + tool.DescriptionShort + "?");
            return;
        }
        state.SetFlag("GRUNLOCK!-FLAG", true);
        state.Output.WriteLine("The grate is unlocked.");
        SetGratingExitMessage(state.World?.FindRoom("MGRAT"), "The grate is closed.");
    }

    /// <summary>act1.38 LOCKER: locks the grating from below (GRAT2).</summary>
    public static void Locker(GameState state, GameObject obj)
    {
        if (obj.Id != "GRAT2")
        {
            state.Output.WriteLine("It doesn't seem to work.");
            return;
        }
        state.SetFlag("GRUNLOCK!-FLAG", false);
        state.Output.WriteLine("The grate is locked.");
        SetGratingExitMessage(state.Here, "The grate is locked.");
    }

    /// <summary>Change the "else" text of the room's KEY-FLAG exit (MDL PUT ... ,CXSTR).</summary>
    private static void SetGratingExitMessage(Room? room, string message)
    {
        if (room == null) return;
        foreach (var (dir, exit) in room.Exits.ToList())
        {
            if (exit.ConditionFlag == "KEY-FLAG!-FLAG")
            {
                room.Exits[dir] = new ExitTarget(exit.ConditionFlag, exit.RoomId!, message);
                return;
            }
        }
    }

    /// <summary>np.93 GWIM for a tool: the only visible tool carried, else the only visible takeable tool in the room.</summary>
    /// <summary>
    /// MDL OBJECT-ACTION for one object: run its action and report whether it dealt with the verb, which here means
    /// it said something.
    /// </summary>
    internal static bool Try(GameState state, GameObject obj, string verb, GameObject? indirect)
    {
        if (obj.ObjectAction == null) return false;
        var output = state.Output;
        var tell = new TellWriter(output);
        state.Output = tell;
        try
        {
            obj.ObjectAction(state, verb, indirect);
        }
        finally
        {
            state.Output = output;
        }
        return tell.Told;
    }

    internal static GameObject? FindOnlyTool(GameState state)
    {
        static bool IsTool(GameObject o) => o.IsVisible && (o.Flags & ObjectFlags.Tool) != 0;
        var carried = state.Winner.Inventory.Where(IsTool).ToList();
        if (carried.Count > 0) return carried.Count == 1 ? carried[0] : null;
        var inRoom = state.Here.Objects.Where(o => IsTool(o) && o.CanTake).ToList();
        return inRoom.Count == 1 ? inRoom[0] : null;
    }

    public static void WindowFunction(GameState state, string verb, GameObject? _)
    {
        if (verb == "OPEN")
        {
            if (state.GetFlag("KITCHEN-WINDOW!-FLAG"))
                state.Output.WriteLine("It is already open.");
            else
            {
                state.Output.WriteLine("With great effort, you open the window far enough to allow entry.");
                state.SetFlag("KITCHEN-WINDOW!-FLAG", true);
            }
        }
        else if (verb == "CLOSE")
        {
            if (state.GetFlag("KITCHEN-WINDOW!-FLAG"))
            {
                state.Output.WriteLine("The window closes (more easily than it opened).");
                state.SetFlag("KITCHEN-WINDOW!-FLAG", false);
            }
            else
                state.Output.WriteLine("It is already closed.");
        }
    }

    /// <summary>act1.38 RUG. (The third argument is the indirect object, which the rug never uses.)</summary>
    /// <summary>Put <paramref name="replacement"/> where <paramref name="old"/> is (room or inventory) and take <paramref name="old"/> away.</summary>
    private static void Swap(GameObject old, GameObject replacement)
    {
        if (old.InRoom == null && old.Carrier == null) return;
        var place = old.Location;
        old.MoveTo(null);
        replacement.MoveTo(place);
    }

    /// <summary>act2.27 IBOAT-FUNCTION: INFLATE the folded boat on the ground, with the pump.</summary>
    public static void IboatFunction(GameState state, string verb, GameObject? _)
    {
        if (verb != "INFLATE") return;
        var iboat = state.World!.FindObject("IBOAT")!;
        if (iboat.InRoom != state.Here)
            state.Output.WriteLine("The boat must be on the ground to be inflated.");
        else if (state.Winner.Inventory.Any(o => o.Id == "PUMP"))
        {
            state.Output.WriteLine("The boat inflates and appears seaworthy.");
            state.SetFlag("DEFLATE!-FLAG", false);
            Swap(iboat, state.World.FindObject("RBOAT")!);
        }
        else
            state.Output.WriteLine("I don't think you have enough lung-power to inflate this boat.");
    }

    /// <summary>act2.27 DBOAT-FUNCTION: the punctured boat can be repaired with putty.</summary>
    public static void DboatFunction(GameState state, string verb, GameObject? with)
    {
        if (verb == "INFLATE")
            state.Output.WriteLine("This boat will not inflate since some moron put a hole in it.");
        else if (verb == "PLUG")
        {
            if (with?.Id == "PUTTY")
            {
                state.Output.WriteLine("Well done.  The boat is repaired.");
                Swap(state.World!.FindObject("DBOAT")!, state.World.FindObject("IBOAT")!);
            }
            else if (with != null)
                state.Output.WriteLine("With a " + with.DescriptionShort + "?");
        }
    }

    /// <summary>
    /// act2.27 RBOAT-FUNCTION: boarding with the sharp stick punctures the boat, disembarking on the river drowns you,
    /// and the boat deflates only on the ground with you out of it.
    /// </summary>
    public static void RboatFunction(GameState state, string verb, GameObject? _)
    {
        var rboat = state.World!.FindObject("RBOAT")!;
        switch (verb)
        {
            case "BOARD":
                if (state.Winner.Inventory.Any(o => o.Id == "STICK"))
                {
                    state.Output.WriteLine("There is a hissing sound and the boat deflates.");
                    Swap(rboat, state.World.FindObject("DBOAT")!);
                }
                break;
            case "DISEMBARK":
                if (state.Here.Id.StartsWith("RIVR", StringComparison.Ordinal))
                    state.JigsUp("Unfortunately, that leaves you in the water, where you drown.");
                break;
            case "DEFLATE":
                if (state.Winner.Vehicle == rboat)
                    state.Output.WriteLine("You can't deflate the boat while you're in it.");
                else if (rboat.InRoom != state.Here)
                    state.Output.WriteLine("The boat must be on the ground to be deflated.");
                else
                {
                    state.Output.WriteLine("The boat deflates.");
                    state.SetFlag("DEFLATE!-FLAG", true);
                    Swap(rboat, state.World.FindObject("IBOAT")!);
                }
                break;
        }
    }

    // act1.38 DOORMUNGS
    private static readonly string[] DoorMungs = ["The door is invulnerable.", "You cannot damage this door.", "The door is still under warranty."];

    /// <summary>act1.38 DDOOR-FUNCTION: the doors that never open (front door, wooden door, stone door).</summary>
    public static void DdoorFunction(GameState state, string verb, GameObject? _)
    {
        if (verb == "OPEN") state.Output.WriteLine("The door cannot be opened.");
        else if (verb == "BURN") state.Output.WriteLine("You cannot burn this door.");
        else if (verb == "MUNG") state.Output.WriteLine(DoorMungs[state.Random.Next(DoorMungs.Length)]);
    }

    /// <summary>act3.13 GRUE-FUNCTION: the grue you never see.</summary>
    public static void GrueFunction(GameState state, string verb, GameObject? _)
    {
        if (verb == "EXAMINE")
            state.Output.WriteLine("The grue is a sinister, lurking presence in the dark places of the earth.  Its favorite diet is adventurers, but its insatiable appetite is tempered by its fear of light.  No grue has ever been seen by the light of day, and few have survived its fearsome jaws to tell the tale.");
        else if (verb == "FIND")
            state.Output.WriteLine("There is no grue here, but I'm sure there is at least one lurking in the darkness nearby.  I wouldn't let my light go out if I were you!");
    }

    /// <summary>
    /// act1.38 RUSTY-KNIFE: picking it up with the sword makes the sword pulse; using it as a weapon makes it cut
    /// your own throat. (Parser calls it for TAKE and when the knife is the weapon.)
    /// </summary>
    public static void RustyKnife(GameState state, string verb, GameObject? _)
    {
        if (verb == "TAKE")
        {
            if (state.Winner.Inventory.Any(o => o.Id == "SWORD"))
                state.Output.WriteLine("As you pick up the rusty knife, your sword gives a single pulse of blinding blue light.");
        }
        else if (verb is "ATTACK" or "SWING" or "THROW" or "KILL")
        {
            var knife = state.World?.FindObject("RKNIF");
            if (knife != null) RemoveFromWhere(knife); // KILL-OBJ
            state.JigsUp("As the knife approaches its victim, your mind is submerged by an overmastering will.  Slowly, your hand turns, until the rusty blade is an inch from your neck.  The knife seems to sing as it savagely slits your throat.");
        }
    }

    /// <summary>
    /// act1.38 SWORD: taking it starts the sword demon (SWORD-GLOW). Here the demon simply runs whenever you
    /// carry the sword (Combat.SwordDemon), so the action itself has nothing to do.
    /// </summary>
    public static void Sword(GameState _1, string _2, GameObject? _3) { }

    /// <summary>act2.27: the bat's action is FLY-ME; whatever you do to it, it carries you off.</summary>
    public static void BatFunction(GameState state, string _1, GameObject? _2) => RoomActions.FlyMe(state);

    // act3.13 CRUSHED, ICEBLAST, VAPORS
    private const string Crushed = "The room seems to have become too small to hold you.  It seems that the  walls are not as compressible as your body, which is somewhat demolished.";
    private const string IceBlast = "You have been blasted to smithereens (wherever they are).";
    private const string Vapors = "Just before you pass out, you notice that the vapors from the flask's contents are fatal.";

    /// <summary>
    /// act3.13 (EATME-FUNCTION, CAKE-FUNCTION): everything in the room goes with you to the other size of room,
    /// growing or shrinking by a factor of 64, and you GOTO it (without a description).
    /// </summary>
    private static void Resize(GameState state, string toRoomId, bool smaller)
    {
        var from = state.Here;
        var to = state.World!.FindRoom(toRoomId)!;
        foreach (var o in from.Objects.ToList())
        {
            o.MoveTo(to);
            o.Size = smaller ? o.Size / 64 : o.Size * 64;
        }
        Parser.Goto(state, to);
    }

    /// <summary>act3.13 ICEBOOM: the orange cake explodes and seals the room.</summary>
    private static void IceBoom(GameState state)
    {
        state.Here.Munged = true;
        state.Here.MungedMessage = "The door to the room seems to be blocked by sticky orange rubble from an explosion.  Probably some careless adventurer was playing with blasting cakes.";
        state.JigsUp(IceBlast);
    }

    private static bool InAliceRooms(GameState state) => state.Here.Id.Contains("ALI", StringComparison.Ordinal);

    /// <summary>act3.13 EATME-FUNCTION: eaten in the tea room, the 'Eat Me' cake makes the room seem very large.</summary>
    public static void EatMeFunction(GameState state, string verb, GameObject? _)
    {
        var cake = state.World!.FindObject("ECAKE")!;
        if (verb != "EAT" || state.Here.Id != "ALICE") return;
        state.Output.WriteLine("Suddenly, the room appears to have become very large.");
        RemoveFromWhere(cake);
        Resize(state, "ALISM", smaller: false);
    }

    /// <summary>
    /// act3.13 CAKE-FUNCTION: the icing is readable through the flask; the blue cake shrinks the room back, the
    /// orange one explodes, and the red one evaporates the pool.
    /// </summary>
    public static void CakeFunction(GameState state, string verb, GameObject? other)
    {
        var cake = state.LastDirectObject;
        if (cake == null) return;
        switch (verb)
        {
            case "READ":
                if (other == null)
                    state.Output.WriteLine("The only writing legible is a capital E.  The rest is too small to be clearly visible.");
                else if (other.Id == "BOTTL")
                    state.Output.WriteLine("The letters appear larger, but still are too small to be read.");
                else if (other.Id == "FLASK")
                    state.Output.WriteLine("The icing, now visible, says '" + (cake.Id == "RDICE" ? "Evaporate" : cake.Id == "ORICE" ? "Explode" : "Enlarge") + "'.");
                else
                    state.Output.WriteLine("You can't see through that!");
                break;
            case "EAT" when InAliceRooms(state):
                if (cake.Id == "ORICE")
                {
                    RemoveFromWhere(cake);
                    IceBoom(state);
                }
                else if (cake.Id == "BLICE")
                {
                    RemoveFromWhere(cake);
                    state.Output.WriteLine("The room around you seems to be getting smaller.");
                    if (state.Here.Id == "ALISM") Resize(state, "ALICE", smaller: true);
                    else state.JigsUp(Crushed);
                }
                break;
            case "THROW" when cake.Id == "ORICE" && InAliceRooms(state):
                RemoveFromWhere(cake);
                IceBoom(state);
                break;
            case "THROW" when cake.Id == "RDICE" && other?.Id == "POOL":
                RemoveFromWhere(other);
                state.Output.WriteLine("The pool of water evaporates, revealing a tin of rare spices.");
                var spices = state.World?.FindObject("SAFFR");
                if (spices != null) spices.Flags |= ObjectFlags.Visible;
                break;
        }
    }

    /// <summary>act3.13 FLASK-FUNCTION: the poison flask kills whoever opens or breaks it.</summary>
    public static void FlaskFunction(GameState state, string verb, GameObject? _)
    {
        var flask = state.World!.FindObject("FLASK")!;
        if (verb == "OPEN")
        {
            state.Here.Munged = true;
            state.Here.MungedMessage = "Noxious vapors prevent your entry.";
            state.JigsUp(Vapors);
        }
        else if (verb is "MUNG" or "THROW")
        {
            state.Output.WriteLine("The flask breaks into pieces.");
            flask.Flags &= ~ObjectFlags.Visible;
            state.JigsUp(Vapors);
        }
    }

    /// <summary>act1.38 PAINTING: breaking it ruins the masterpiece.</summary>
    public static void Painting(GameState state, string verb, GameObject? _)
    {
        if (verb != "MUNG") return;
        var art = state.World!.FindObject("PAINT")!;
        art.TrophyScore = 0;
        art.DescriptionShort = "worthless piece of canvas";
        art.DescriptionHere = "There is a worthless piece of canvas here.";
        state.Output.WriteLine("Congratulations!  Unlike the other vandals, who merely stole the artist's masterpieces, you have destroyed one.");
    }

    /// <summary>
    /// act1.38 TROLL: gifts and things thrown at him he catches and eats (a knife he throws back, angrily); he won't be
    /// taken or moved. His melee hooks (FIGHT, DEAD!, OUT!, IN!) belong to the melee, which isn't in the MIT archive;
    /// <see cref="Combat"/> stands in for it.
    /// </summary>
    public static void TrollFunction(GameState state, string verb, GameObject? other)
    {
        var troll = state.World!.FindObject("TROLL")!;
        if (verb is "THROW" or "GIVE" && other != null)
        {
            state.Output.WriteLine(verb == "THROW"
                ? "The troll, who is remarkably coordinated, catches the " + other.DescriptionShort
                : "The troll, who is not overly proud, graciously accepts the gift");
            if (other.Id == "KNIFE")
            {
                state.Output.WriteLine("and being for the moment sated, throws it back.  Fortunately, the troll has poor control, and the knife falls to the floor.  He does not look pleased.");
                other.MoveTo(state.Here);
                troll.Flags |= ObjectFlags.Fighting;
                state.VillainStrength.TryAdd("TROLL", Combat.TrollStrength);
            }
            else
            {
                state.Output.WriteLine("and not having the most discriminating tastes, gleefully eats it.");
                RemoveFromWhere(other);
            }
        }
        else if (verb is "TAKE" or "MOVE")
            state.Output.WriteLine("The troll spits in your face, saying \"Better luck next time.\"");
        else if (verb == "MUNG")
            state.Output.WriteLine("The troll laughs at your puny gesture.");
    }

    /// <summary>act1.38 BLACK-BOOK: burning the prayer book is a mistake.</summary>
    public static void BlackBook(GameState state, string verb, GameObject? _)
    {
        if (verb != "BURN") return;
        var book = state.World?.FindObject("BOOK");
        if (book != null) RemoveFromWhere(book);
        state.JigsUp("A booming voice says 'Wrong, cretin!' and you notice that you have turned into a pile of dust.");
    }

    /// <summary>act1.38 BODY-FUNCTION: the bodies in the Land of the Living Dead are not to be disturbed.</summary>
    public static void BodyFunction(GameState state, string verb, GameObject? _)
    {
        if (verb == "TAKE")
            state.Output.WriteLine("A force keeps you from taking the bodies.");
        else if (verb is "MUNG" or "BURN")
        {
            if (!state.GetFlag("ON-POLE!-FLAG"))
            {
                state.SetFlag("ON-POLE!-FLAG", true);
                var pole = state.World?.FindObject("HPOLE");
                var lld2 = state.World?.FindRoom("LLD2");
                if (lld2 != null) pole?.MoveTo(lld2);
            }
            state.JigsUp("The voice of the guardian of the dungeon booms out from the darkness 'Your disrespect costs you your life!' and places your head on a pole.");
        }
    }

    /// <summary>
    /// act3.13 HEAD-FUNCTION: anything but reading the tomb or the heads costs you your valuables (into the large case
    /// in the living room) and your life.
    /// </summary>
    public static void HeadFunction(GameState state, string verb, GameObject? _)
    {
        if (verb == "READ") return;
        state.Output.WriteLine("Although the implementers are dead, they foresaw that some cretin would tamper with their remains.  Therefore, they took steps to prevent this.");
        static bool Valuable(GameObject o) => o.TrophyScore > 0 && (o.Flags & ObjectFlags.Sacred) == 0;
        var taken = state.Winner.Inventory.Where(Valuable).Concat(state.Here.Objects.Where(Valuable)).ToList();
        var world = state.World!;
        var lcase = world.FindObject("LCASE")!;
        if (taken.Count > 0 && lcase.InRoom == null)
            lcase.MoveTo(world.FindRoom("LROOM")!);
        foreach (var o in taken) o.MoveTo(lcase);
        state.JigsUp("Unfortunately, we've run out of poles.  Therefore, in punishment for your most grievous sin, we shall deprive you of all your valuables, and of your life.");
    }

    /// <summary>act3.13 COKE-BOTTLES: throw or break them and they vanish.</summary>
    public static void CokeBottles(GameState state, string verb, GameObject? _)
    {
        if (verb is not ("THROW" or "MUNG")) return;
        var cokes = state.World?.FindObject("COKES");
        if (cokes == null) return;
        state.Output.WriteLine("Congratulations!  You've managed to break all those bottles. Fortunately for your feet, they were made of magic glass and disappear immediately.");
        cokes.Flags &= ~ObjectFlags.Visible;
        cokes.Size = 0;
    }

    /// <summary>act1.38 BOLT-FUNCTION: TURN BOLT WITH WRENCH opens or closes the sluice gates, once the yellow button is pushed.</summary>
    public static void BoltFunction(GameState state, string verb, GameObject? tool)
    {
        if (verb != "TURN" || tool == null) return;
        if (tool.Id != "WRENC")
        {
            state.Output.WriteLine("The bolt won't turn using the " + tool.DescriptionShort + ".");
            return;
        }
        if (!state.GetFlag("GATE-FLAG!-FLAG"))
        {
            state.Output.WriteLine("The bolt won't turn with your best effort.");
            return;
        }
        var trunk = state.World?.FindObject("TRUNK");
        if (state.GetFlag("LOW-TIDE!-FLAG"))
        {
            state.SetFlag("LOW-TIDE!-FLAG", false);
            state.Output.WriteLine("The sluice gates close and water starts to collect behind the dam.");
            if (trunk != null && trunk.InRoom?.Id == "RESES") trunk.Flags &= ~ObjectFlags.Visible;
        }
        else
        {
            state.SetFlag("LOW-TIDE!-FLAG", true);
            state.Output.WriteLine("The sluice gates open and water pours through the dam.");
            if (trunk != null) trunk.Flags |= ObjectFlags.Visible;
        }
    }

    /// <summary>act1.38 LEAK-FUNCTION: PLUG LEAK WITH PUTTY stops the flood in the maintenance room.</summary>
    public static void LeakFunction(GameState state, string verb, GameObject? with)
    {
        if (verb != "PLUG" || state.Here.RVars <= 0) return;
        if (with?.Id == "PUTTY")
        {
            state.Here.RVars = -1;
            state.ScheduledEvents.RemoveAll(e => e.Name == Clocks.MaintRoom);
            state.Output.WriteLine("By some miracle of elven technology, you have managed to stop the leak in the dam.");
        }
        else if (with != null)
            state.Output.WriteLine("With a " + with.DescriptionShort + "?"); // act1.38 WITH-TELL
    }

    /// <summary>act1.38 TUBE-FUNCTION: nothing can be put into the tube.</summary>
    public static void TubeFunction(GameState state, string verb, GameObject? _)
    {
        if (verb == "PUT" && state.LastIndirectObject?.Id == "TUBE")
            state.Output.WriteLine("The tube refuses to accept anything.");
    }

    public static void Rug(GameState state, string verb, GameObject? _)
    {
        if (verb == "TAKE")
            state.Output.WriteLine("The rug is extremely heavy and cannot be carried.");
        else if (verb == "LIFT")
            state.Output.WriteLine("The rug is too heavy to lift, but in trying to take it you have noticed an irregularity beneath it.");
        else if (verb == "MOVE")
        {
            if (state.GetFlag("RUG-MOVED!-FLAG"))
                state.Output.WriteLine("Having moved the carpet previously, you find it impossible to move it again.");
            else
            {
                state.Output.WriteLine("With a great effort, the rug is moved to one side of the room. With the rug moved, the dusty cover of a closed trap-door appears.");
                state.SetFlag("RUG-MOVED!-FLAG", true);
                var tdoor = state.World?.FindObject("TDOOR");
                if (tdoor != null) tdoor.Flags |= ObjectFlags.Visible;
                var door = state.World?.FindObject("DOOR");
                if (door != null) door.Flags |= ObjectFlags.Visible;
                // Ensure trap door in current room is visible (same ref as World, but belt-and-braces for OPEN DOOR resolution)
                if (state.Here != null)
                    foreach (var o in state.Here.Objects)
                        if (o.Id == "DOOR") o.Flags |= ObjectFlags.Visible;
            }
        }
    }

    public static void TrophyCase(GameState state, string verb, GameObject? _)
    {
        if (verb == "TAKE")
            state.Output.WriteLine("The trophy case is securely fastened to the wall (perhaps to foil any attempt by robbers to remove it).");
    }

    public static void Lantern(GameState state, string verb, GameObject? _)
    {
        var lamp = state.World?.FindObject("LAMP");
        var blamp = state.World?.FindObject("BLAMP");
        if (verb == "THROW" && lamp != null)
        {
            state.Output.WriteLine("The lamp has smashed into the floor and the light has gone out.");
            RemoveFromWhere(lamp!);
            blamp?.MoveTo(state.Here);
        }
        // TURN ON / OFF: the original LANTERN only enables or disables the lamp's clock and lets LAMP-ON /
        // LAMP-OFF do the rest. Here the lamp clock runs whenever the lamp is on (GameState.ProcessClocks).
        else if ((verb == "TURNON" || verb == "TURN ON") && lamp != null)
        {
            Parser.LampOn(state, lamp);
        }
        else if ((verb == "TURNOFF" || verb == "TURN OFF") && lamp != null)
        {
            Parser.LampOff(state, lamp);
        }
    }

    /// <summary>
    /// act1.38 MATCH-FUNCTION: LIGHT (or STRIKE) uses up one match, which burns for two moves; the matchbook holds
    /// five, and the last one never lights.
    /// </summary>
    public static void MatchFunction(GameState state, string verb, GameObject? _)
    {
        var match = state.World?.FindObject("MATCH");
        if (match == null) return;
        if (verb == "TURNON")
        {
            if (--state.MatchesLeft <= 0)
            {
                state.Output.WriteLine("I'm afraid that you have run out of matches.");
                return;
            }
            match.Flags |= ObjectFlags.OnFire;
            match.LightAmount = 1;
            state.ScheduledEvents.RemoveAll(e => e.Name == Clocks.MatchOut);
            state.Schedule(2, Clocks.MatchOut, match.Id);
            state.Output.WriteLine("One of the matches starts to burn.");
        }
        else if (verb == "TURNOFF" && match.LightAmount > 0)
        {
            state.Output.WriteLine("The match is out.");
            match.Flags &= ~ObjectFlags.OnFire;
            match.LightAmount = 0;
            state.ScheduledEvents.RemoveAll(e => e.Name == Clocks.MatchOut);
        }
    }

    /// <summary>act1.38 MATCH-FUNCTION clock (C-INT): the match goes out.</summary>
    public static void MatchBurnout(GameState state, string? matchId)
    {
        var match = state.World?.FindObject(matchId ?? "MATCH");
        if (match == null) return;
        state.Output.WriteLine("The match has gone out.");
        match.Flags &= ~ObjectFlags.OnFire;
        match.LightAmount = 0;
    }

    /// <summary>
    /// act1.38 CANDLES: the first touch starts their 50-move clock; LIGHT them WITH a burning match (the torch
    /// vaporises them), put them out with TURN OFF.
    /// </summary>
    public static void CandlesFunction(GameState state, string verb, GameObject? with)
    {
        var candles = state.World?.FindObject("CANDL");
        if (candles == null) return;
        if (state.CandleTicks == 0 && state.CandleStage == 0) state.CandleTicks = 50; // ORAND set on first use
        if (verb == "TURNON")
        {
            if (candles.LightAmount == 0)
                state.Output.WriteLine("Alas, there's not much left of the candles.  Certainly not enough to burn.");
            else if (with == null)
                state.Output.WriteLine("With what?");
            else if (with.Id == "MATCH" && with.LightAmount > 0)
            {
                if (candles.LightAmount > 0) state.Output.WriteLine("The candles are already lighted.");
                else
                {
                    candles.LightAmount = 1;
                    state.Output.WriteLine("The candles are lighted.");
                }
            }
            else if (with.Id == "TORCH")
            {
                if (candles.LightAmount > 0)
                    state.Output.WriteLine("You realize, just in time, that the candles are already lighted.");
                else
                {
                    state.Output.WriteLine("The heat from the torch is so intense that the candles are vaporised.");
                    RemoveFromWhere(candles);
                }
            }
            else
                state.Output.WriteLine("You have to light them with something that's burning, you know.");
        }
        else if (verb == "TURNOFF")
        {
            if (candles.LightAmount > 0)
            {
                state.Output.WriteLine("The flame is extinguished.");
                candles.LightAmount = -1;
            }
            else
                state.Output.WriteLine("The candles are not lighted.");
        }
    }

    // act2.27 BRICK-BOOM
    private const string BrickBoom = "Now you've done it.  It seems that the brick has other properties than weight, namely the ability to blow you to smithereens.";

    /// <summary>act2.27 FUSE-FUNCTION: BURN the wire and it burns for two moves.</summary>
    public static void FuseFunction(GameState state, string verb, GameObject? _)
    {
        if (verb != "BURN") return;
        state.Output.WriteLine("The wire starts to burn.");
        state.ScheduledEvents.RemoveAll(e => e.Name == Clocks.Fuse);
        state.Schedule(2, Clocks.Fuse);
    }

    /// <summary>
    /// act2.27 FUSE-FUNCTION clock: the wire is gone. In the brick it sets the brick off: where you are, that kills
    /// you; in the slot of the safe it blows the box open; anywhere else it destroys what lies around. The room
    /// collapses five moves later (SAFE-MUNG).
    /// </summary>
    public static void FuseBurnedDown(GameState state)
    {
        var world = state.World!;
        var fuse = world.FindObject("FUSE")!;
        var brick = world.FindObject("BRICK")!;
        fuse.Flags &= ~ObjectFlags.Visible;
        if (fuse.Container != brick)
        {
            if (fuse.InRoom == null || fuse.InRoom == state.Here)
                state.Output.WriteLine("The wire rapidly burns into nothingness.");
            return;
        }
        brick.Flags &= ~ObjectFlags.Visible;
        // PUTTER leaves a contained object's OROOM at the room it was put in; the carried brick is wherever you are.
        var brickRoom = brick.Container?.InRoom ?? brick.InRoom ?? state.Here;
        if (brickRoom == state.Here)
        {
            state.MungRoom(brickRoom.Id, "The way is blocked by debris from an explosion.");
            state.JigsUp(BrickBoom);
            return;
        }
        state.Output.WriteLine("There is an explosion nearby.");
        state.Schedule(5, Clocks.SafeMung, brickRoom.Id);
        if (brickRoom.Id == "SAFE")
        {
            var slot = world.FindObject("SSLOT")!;
            if (slot.Contents.Contains(brick))
            {
                slot.Flags &= ~ObjectFlags.Visible;
                world.FindObject("SAFE")!.Open = true;
                state.SetFlag("SAFE-FLAG!-FLAG", true);
            }
            return;
        }
        foreach (var o in brickRoom.Objects.Where(o => o.CanTake))
            o.Flags &= ~ObjectFlags.Visible;
        if (brickRoom.Id == "LROOM")
        {
            var tcase = world.FindObject("TCASE")!;
            foreach (var o in tcase.Contents.ToList()) o.MoveTo(null);
        }
    }

    /// <summary>
    /// act2.27 SAFE-MUNG: five moves after the explosion its room collapses (on you, if you're in it). After the safe,
    /// the ledge outside follows eight moves later (LEDGE-MUNG).
    /// </summary>
    public static void SafeMung(GameState state, string? mungedRoomId)
    {
        var world = state.World!;
        if (state.Here.Id == mungedRoomId)
        {
            state.JigsUp(mungedRoomId is "LROOM" or "KITCH" or "ATTIC" // RHOUSEBIT
                ? "The house shakes, and the ceiling of the room you're in collapses, turning you into a pancake."
                : "The room trembles and 50,000 pounds of rock fall on you, turning you into a pancake.");
        }
        else
        {
            state.Output.WriteLine("You may recall your recent explosion.  Well, probably as a result of that, you hear an ominous rumbling, as if one of the rooms in the dungeon had collapsed.");
            if (mungedRoomId == "SAFE") state.Schedule(8, Clocks.LedgeMung);
        }
        var brick = world.FindObject("BRICK")!;
        var room = brick.Container?.InRoom ?? brick.InRoom ?? state.Here;
        state.MungRoom(room.Id, "The way is blocked by debris from an explosion.");
    }

    /// <summary>act2.27 LEDGE-MUNG: eight moves after the safe room collapsed, so does the wide ledge outside it.</summary>
    public static void LedgeMung(GameState state)
    {
        var world = state.World!;
        if (state.Here.Id == "LEDG4")
        {
            if (state.Winner.Vehicle != null)
            {
                if (state.GetFlag("BTIE!-FLAG"))
                {
                    var bottom = world.FindRoom("VLBOT")!;
                    state.BalloonLocation = "VLBOT";
                    var ball = world.FindObject("BALLO")!;
                    ball.MoveTo(null);
                    world.FindObject("DBALL")!.MoveTo(bottom);
                    state.SetFlag("BTIE!-FLAG", false);
                    state.SetFlag("BINF!-FLAG", false);
                    Balloon.StopClock(state);
                    state.ScheduledEvents.RemoveAll(e => e.Name == Clocks.Burnup);
                    state.JigsUp("The ledge collapses, probably as a result of the explosion.  A large chunk of it, which is attached to the hook, drags you down to the ground.  Fatally.");
                }
                else
                    state.Output.WriteLine("The ledge collapses, leaving you with no place to land.");
            }
            else
                state.JigsUp("The force of the explosion has caused the ledge to collapse belatedly.");
        }
        else
            state.Output.WriteLine("The ledge collapses, giving you a narrow escape.");
        state.MungRoom("LEDG4", "The ledge has collapsed and cannot be landed on.");
    }

    /// <summary>act2.27 BRICK-FUNCTION: burning the brick itself sets it off in your hands.</summary>
    public static void BrickFunction(GameState state, string verb, GameObject? _)
    {
        if (verb == "BURN") state.JigsUp(BrickBoom);
    }

    /// <summary>act2.27 SAFE-FUNCTION: the rusty box in the wall.</summary>
    public static void SafeFunction(GameState state, string verb, GameObject? _)
    {
        var blown = state.GetFlag("SAFE-FLAG!-FLAG");
        switch (verb)
        {
            case "TAKE": state.Output.WriteLine("The box is imbedded in the wall."); break;
            case "OPEN": state.Output.WriteLine(blown ? "The box has no door!" : "The box is rusted and will not open."); break;
            case "CLOSE": state.Output.WriteLine(blown ? "The box has no door!" : "The box is not open, chomper!"); break;
            case "BLAST": state.Output.WriteLine("What do you expect, BOOM?"); break;
        }
    }

    /// <summary>act3.13 BUCKET on its clock (BCKIN, 100 moves after it rose): the water in it evaporates.</summary>
    public static void BucketTick(GameState state)
    {
        var water = state.World!.FindObject("WATER")!;
        if (water.Container?.Id == "BUCKE") RemoveFromWhere(water);
    }

    /// <summary>
    /// act3.13 BUCKET with READ-OUT, after every command you spend in the bucket: with water in it, it rises to the
    /// top of the well; at the top without water, it sinks back (PASS-THE-BUCKET).
    /// </summary>
    public static void BucketReadOut(GameState state)
    {
        var world = state.World!;
        var bucket = world.FindObject("BUCKE")!;
        if (state.Winner.Vehicle != bucket) return;
        var full = world.FindObject("WATER")!.Container == bucket;
        var top = state.GetFlag("BUCKET-TOP!-FLAG");
        if (full && !top)
        {
            state.Output.WriteLine("The bucket rises and comes to a stop.");
            state.SetFlag("BUCKET-TOP!-FLAG", true);
            PassTheBucket(state, world.FindRoom("TWELL")!, bucket);
            state.ScheduledEvents.RemoveAll(e => e.Name == Clocks.Bucket);
            state.Schedule(100, Clocks.Bucket);
        }
        else if (top && !full)
        {
            state.Output.WriteLine("The bucket descends and comes to a stop.");
            state.SetFlag("BUCKET-TOP!-FLAG", false);
            PassTheBucket(state, world.FindRoom("BWELL")!, bucket);
        }
    }

    /// <summary>act3.13 PASS-THE-BUCKET: the bucket moves, and you in it see where you are.</summary>
    private static void PassTheBucket(GameState state, Room to, GameObject bucket)
    {
        bucket.MoveTo(to);
        if (state.Winner.Vehicle != bucket) return;
        Parser.Goto(state, to);
        Parser.Describe(state);
    }

    /// <summary>act3.13 BUCKET as the bucket's own action: nothing to say for ordinary verbs.</summary>
    public static void BucketFunction(GameState _1, string _2, GameObject? _3) { }

    /// <summary>
    /// rooms.99 EVERYTHING and VALUABLES (the global "all" and "valuables" objects). The parser hands TAKE, DROP and PUT
    /// of them to <c>Parser.DoLots</c>; as an object action there is nothing more to do.
    /// </summary>
    public static void Lots(GameState _1, string _2, GameObject? _3) { }

    public static void BottleFunction(GameState state, string verb, GameObject? _)
    {
        // The third argument is the indirect object; this action is about the BOTTL itself.
        var obj = state.World?.FindObject("BOTTL");
        if (verb == "THROW" && obj != null)
        {
            state.Output.WriteLine("The bottle hits the far wall and is decimated.");
            RemoveFromWhere(obj);
        }
        else if (verb == "MUNG" || verb == "BREAK")
        {
            if (obj?.Carrier == state.Winner)
            {
                obj.MoveTo(null);
                state.Output.WriteLine("You have destroyed the bottle.  Well done.");
            }
            else if (obj?.InRoom == state.Here)
            {
                obj.MoveTo(null);
                state.Output.WriteLine("A brilliant maneuver destroys the bottle.");
            }
        }
    }

    public static void WaterFunction(GameState state, string verb, GameObject? _)
    {
        // The third argument is the indirect object; this action is about the WATER itself.
        var obj = state.World?.FindObject("WATER");
        if (obj == null) return;
        var bottle = state.World?.FindObject("BOTTL");
        if (verb == "THROW")
        {
            state.Output.WriteLine("The water splashes on the walls, and evaporates immediately.");
            RemoveFromWhere(obj);
        }
        else if (verb == "PUT")
        {
            var cont = state.LastIndirectObject;
            if (cont == bottle && bottle != null && state.Winner.Inventory.Contains(bottle))
            {
                if (bottle.Contents.Count > 0)
                    state.Output.WriteLine("The bottle is already full.");
                else if (!bottle.Open)
                    state.Output.WriteLine("The bottle is closed.");
                else
                {
                    obj.MoveTo(bottle);
                    state.Output.WriteLine("The bottle is now full of water.");
                }
            }
            else if (cont != null && cont == state.Winner.Vehicle)
            {
                // act1.38 WATER-FUNCTION: water put into the vehicle you're in stays there.
                state.Output.WriteLine("There is now a puddle in the bottom of the " + cont.DescriptionShort + ".");
                if (obj.Container != cont) obj.MoveTo(cont);
            }
            else if (cont != null && cont != bottle)
            {
                state.Output.WriteLine("The water leaks out of the " + cont.DescriptionShort + " and evaporates immediately.");
                RemoveFromWhere(obj);
            }
            else
                state.Output.WriteLine("The water slips through your fingers.");
        }
        else if (verb == "DROP" || verb == "POUR")
        {
            obj.MoveTo(state.Here);
            state.Output.WriteLine("The water spills to the floor and evaporates immediately.");
        }
    }

    public static void RopeFunction(GameState state, string verb, GameObject? _)
    {
        // The third argument is the indirect object; this action is about the ROPE itself.
        var obj = state.World?.FindObject("ROPE");
        var dome = state.World?.FindRoom("DOME");
        var raili = state.World?.FindObject("RAILI");
        if (state.Here != dome)
        {
            state.SetFlag("DOME-FLAG!-FLAG", false);
            if (verb == "TIE") state.Output.WriteLine("There is nothing it can be tied to.");
            else state.Output.WriteLine("It is not tied to anything.");
            return;
        }
        if (verb == "TIE" && state.LastIndirectObject == raili)
        {
            if (state.GetFlag("DOME-FLAG!-FLAG"))
                state.Output.WriteLine("The rope is already attached.");
            else
            {
                state.Output.WriteLine("The rope drops over the side and comes within ten feet of the floor.");
                state.SetFlag("DOME-FLAG!-FLAG", true);
                if (obj != null && obj.Carrier == state.Winner)
                {
                    obj.MoveTo(dome);
                }
            }
        }
        else if (verb == "UNTIE")
        {
            if (state.GetFlag("DOME-FLAG!-FLAG"))
            {
                state.SetFlag("DOME-FLAG!-FLAG", false);
                state.Output.WriteLine("Although you tied it incorrectly, the rope becomes free.");
            }
            else
                state.Output.WriteLine("It is not tied to anything.");
        }
        else if (verb == "TIE")
            state.Output.WriteLine("There is nothing it can be tied to.");
    }

    public static void Skeleton(GameState state, string verb, GameObject? _)
    {
        if (verb == "TAKE")
            state.Output.WriteLine("The skeleton is too heavy to carry.");
    }

    public static void LeafPile(GameState state, string verb, GameObject? _)
    {
        // The third argument is the indirect object; this action is about the LEAVE itself.
        var obj = state.World?.FindObject("LEAVE");
        if (verb == "BURN" && obj != null)
        {
            if (obj.InRoom != null)
            {
                state.Output.WriteLine("The leaves burn and the neighbors start to complain.");
                obj.MoveTo(null);
            }
            else
            {
                state.Output.WriteLine("The sight of someone carrying a pile of burning leaves so offends the neighbors that they come over and put you out.");
                state.JigsUp(null);
            }
        }
        else if (verb == "MOVE")
            state.Output.WriteLine("Done.");
    }

    public static void GunkFunction(GameState state, string _, GameObject? obj)
    {
        if (obj?.Container != null)
        {
            obj.MoveTo(null);
            state.Output.WriteLine("The slag turns out to be rather insubstantial, and crumbles into dust at your touch.  It must not have been very valuable.");
        }
    }

    /// <summary>act2.27 MACHINE-FUNCTION: the machine's lid, in the machine room.</summary>
    public static void MachineFunction(GameState state, string verb, GameObject? _)
    {
        if (state.Here.Id != "MACHI") return;
        var mach = state.World!.FindObject("MACHI")!;
        if (verb == "OPEN")
        {
            if (mach.Open) state.Output.WriteLine(Dummy[state.Random.Next(Dummy.Length)]);
            else
            {
                state.Output.WriteLine("The lid opens.");
                mach.Open = true;
            }
        }
        else if (verb == "CLOSE")
        {
            if (mach.Open)
            {
                state.Output.WriteLine("The lid closes.");
                mach.Open = false;
            }
            else state.Output.WriteLine(Dummy[state.Random.Next(Dummy.Length)]);
        }
    }

    public static void Glacier(GameState _, string _1, GameObject? _2)
    {
        // Handled in Parser DoThrow: throw torch at ice in ICY.
    }

    /// <summary>
    /// act2.27 MSWITCH-FUNCTION: TURN SWITCH WITH SCREWDRIVER runs the closed machine; coal inside becomes a diamond,
    /// anything else becomes slag.
    /// </summary>
    public static void MSwitchFunction(GameState state, string verb, GameObject? tool)
    {
        if (verb != "TURN" || tool == null) return;
        var world = state.World!;
        var mach = world.FindObject("MACHI")!;
        if (tool.Id != "SCREW")
        {
            state.Output.WriteLine("It seems that a " + tool.DescriptionShort + " won't do.");
            return;
        }
        if (mach.Open)
        {
            state.Output.WriteLine("The machine doesn't seem to want to do anything.");
            return;
        }
        state.Output.WriteLine("The machine comes to life (figuratively) with a dazzling display of colored lights and bizarre noises.  After a few moments, the excitement abates.");
        var coal = world.FindObject("COAL")!;
        if (coal.Container == mach)
        {
            coal.MoveTo(null);
            world.FindObject("DIAMO")!.MoveTo(mach, first: true);
        }
        else if (mach.Contents.Count > 0)
        {
            foreach (var o in mach.Contents.ToList()) o.MoveTo(null);
            world.FindObject("GUNK")!.MoveTo(mach);
        }
    }

    public static void Dumbwaiter(GameState state, string verb, GameObject? _)
    {
        var top = state.World?.FindRoom("TSHAF");
        var bot = state.World?.FindRoom("BSHAF");
        var tbask = state.World?.FindObject("TBASK");
        var fbask = state.World?.FindObject("FBASK");
        if (top == null || bot == null || tbask == null || fbask == null) return;
        var ct = state.GetFlag("CAGE-TOP!-FLAG");
        if (verb == "RAISE")
        {
            if (ct) state.Output.WriteLine("It's already at the top.");
            else
            {
                tbask.MoveTo(top);
                fbask.MoveTo(bot);
                state.Output.WriteLine("The basket is raised to the top of the shaft.");
                state.SetFlag("CAGE-TOP!-FLAG", true);
            }
        }
        else if (verb == "LOWER")
        {
            if (!ct) state.Output.WriteLine("It's already at the bottom.");
            else
            {
                tbask.MoveTo(bot);
                fbask.MoveTo(top);
                state.Output.WriteLine("The basket is lowered to the bottom of the shaft.");
                state.SetFlag("CAGE-TOP!-FLAG", false);
            }
        }
        else if (verb == "TAKE")
        {
            if ((ct && state.Here == top) || (!ct && state.Here == bot))
                state.Output.WriteLine("The cage is securely fastened to the iron chain.");
            else
                state.Output.WriteLine("I can't see that here.");
        }
    }

    private static void RemoveFromWhere(GameObject obj) => obj.MoveTo(null);

    public static void TrapDoor(GameState state, string verb, GameObject? _)
    {
        if (state.Here.Id == "LROOM")
        {
            if (verb == "OPEN")
            {
                if (state.GetFlag("TRAP-DOOR!-FLAG"))
                    state.Output.WriteLine("It's open.");
                else
                {
                    state.Output.WriteLine("The door reluctantly opens to reveal a rickety staircase descending into darkness.");
                    state.SetFlag("TRAP-DOOR!-FLAG", true);
                }
            }
            else if (verb == "CLOSE")
            {
                if (state.GetFlag("TRAP-DOOR!-FLAG"))
                {
                    state.Output.WriteLine("The door swings shut and closes.");
                    state.SetFlag("TRAP-DOOR!-FLAG", false);
                }
                else
                    state.Output.WriteLine("It's closed.");
            }
        }
        else if (state.Here.Id == "CELLA")
        {
            if (verb == "OPEN")
                state.Output.WriteLine("The door is locked from above.");
            else
                state.Output.WriteLine("It's closed.");
        }
    }

    public static void ReadObject(GameState state, GameObject? obj)
    {
        // act1.38 READER
        if (obj == null) return;
        if (!GameState.IsLit(state)) { state.Output.WriteLine("It is impossible to read in the dark."); return; }
        var through = state.LastIndirectObject;
        if (through != null && !through.IsTransparent) { state.Output.WriteLine("How does one look through a " + through.DescriptionShort + "?"); return; }
        if ((obj.Flags & ObjectFlags.Readable) == 0) { state.Output.WriteLine("How can I read a " + obj.DescriptionShort + "?"); return; }
        if (Try(state, obj, "READ", through)) return;
        state.Output.WriteLine(obj.ReadText ?? "");
    }

    public static void EatObject(GameState state, GameObject? obj)
    {
        if (obj == null) return;
        if ((obj.Flags & ObjectFlags.Food) == 0) { state.Output.WriteLine("You can't eat that."); return; }
        if (Try(state, obj, "EAT", null)) return;
        RemoveFromWhere(obj);
        state.Output.WriteLine("Thank you, that was delicious.");
    }

    public static void DrinkObject(GameState state, GameObject? obj)
    {
        if (obj == null) return;
        if ((obj.Flags & ObjectFlags.Drinkable) == 0) { state.Output.WriteLine("You can't drink that."); return; }
        state.Output.WriteLine("You have drunk the water.  The bottle is now empty.");
        obj.MoveTo(state.Here);
    }

    public static void Diagnose(GameState state)
    {
        state.Output.WriteLine("You are in fine health.  Your strength is " + state.Winner.Strength + ".");
    }

    /// <summary>Mirror (REFL1/REFL2): RUB swaps MIRR1/MIRR2 contents and moves player; BREAK/THROW sets MIRROR-MUNG!-FLAG.</summary>
    public static void MirrorFunction(GameState state, string verb, GameObject? _)
    {
        var here = state.Here;
        if (here == null || state.World == null) return;
        var m1 = state.World.FindRoom("MIRR1");
        var m2 = state.World.FindRoom("MIRR2");
        if (m1 == null || m2 == null) return;
        if (verb == "RUB" || verb == "RUBB")
        {
            if (state.GetFlag("MIRROR-MUNG!-FLAG"))
            {
                state.Output.WriteLine("Haven't you done enough already?");
                return;
            }
            var from = here.Id == "MIRR1" ? m1 : m2;
            var to = here.Id == "MIRR1" ? m2 : m1;
            var objsFrom = from.Objects.Where(o => o.InRoom == from).ToList();
            var objsTo = to.Objects.Where(o => o.InRoom == to).ToList();
            foreach (var o in objsFrom) o.MoveTo(null);
            foreach (var o in objsTo) o.MoveTo(from);
            foreach (var o in objsFrom) o.MoveTo(to);
            state.Winner.CurrentRoom = to;
            state.Output.WriteLine("There is a rumble from deep within the earth and the room shakes.");
            return;
        }
        if (verb == "MUNG" || verb == "BREAK" || verb == "THROW")
        {
            if (state.GetFlag("MIRROR-MUNG!-FLAG"))
            {
                state.Output.WriteLine("Haven't you done enough already?");
                return;
            }
            state.SetFlag("MIRROR-MUNG!-FLAG", true);
            state.Output.WriteLine("You have broken the mirror.  I hope you have a seven years supply of good luck handy.");
        }
        if (verb == "TAKE")
            state.Output.WriteLine("Nobody but a greedy surgeon would allow you to attempt that trick.");
        if (verb == "LOOK" || verb == "EXAMI")
            state.Output.WriteLine(state.GetFlag("MIRROR-MUNG!-FLAG") ? "The mirror is broken into many pieces." : "There is an ugly person staring at you.");
    }

    /// <summary>
    /// act2.27 STICK-FUNCTION: waved at either end of the rainbow it turns solid (and the pot of gold appears) or
    /// ordinary again; waved on the rainbow itself, it drops you.
    /// </summary>
    public static void StickFunction(GameState state, string verb, GameObject? _)
    {
        if (verb != "WAVE") return;
        var here = state.Here.Id;
        if (here is "FALLS" or "POG")
        {
            if (!state.GetFlag("RAINBOW!-FLAG"))
            {
                var pot = state.World?.FindObject("POT");
                if (pot != null) pot.Flags |= ObjectFlags.Visible;
                state.Output.WriteLine("Suddenly, the rainbow appears to become solid and, I venture, walkable (I think the giveaway was the stairs and bannister).");
                state.SetFlag("RAINBOW!-FLAG", true);
            }
            else
            {
                state.Output.WriteLine("The rainbow seems to have become somewhat run-of-the-mill.");
                state.SetFlag("RAINBOW!-FLAG", false);
            }
        }
        else if (here == "RAINB")
        {
            state.SetFlag("RAINBOW!-FLAG", false);
            state.JigsUp("The structural integrity of the rainbow seems to have left it, leaving you about 450 feet in the air, supported by water vapor.");
        }
        else
            state.Output.WriteLine("Very good.");
    }

    /// <summary>act1.38 GHOST-FUNCTION: the spirits can't be touched.</summary>
    public static void GhostFunction(GameState state, string _1, GameObject? _2) =>
        state.Output.WriteLine("You seem unable to affect these spirits.");

    // act1.38 CYCLOMAD
    internal static readonly string[] CycloMad =
    [
        "The cyclops seems somewhat agitated.",
        "The cyclops appears to be getting more agitated.",
        "The cyclops is moving about the room, looking for something.",
        "The cyclops was looking for salt and pepper.  I think he is gathering condiments for his upcoming snack.",
        "The cyclops is moving toward you in an unfriendly manner.",
        "You have two choices: 1. Leave  2. Become dinner.",
    ];

    /// <summary>act1.38 AOS-SOS: the cyclops gets one step angrier (thirsty anger counts down), and shows it.</summary>
    private static int AosSos(GameState state, int count)
    {
        count = count < 0 ? count - 1 : count + 1;
        if (!state.GetFlag("CYCLOPS-FLAG!-FLAG"))
            state.Output.WriteLine(CycloMad[Math.Min(Math.Abs(count), CycloMad.Length) - 1]);
        return count;
    }

    /// <summary>
    /// act1.38 CYCLOPS. The room's RVARS counts his anger: up from 0 as you annoy him, below 0 once the hot peppers
    /// have made him thirsty. Past 5 he eats you. Water when he's thirsty puts him to sleep (CYCLOPS-FLAG); waking,
    /// hitting or burning him wakes him up fighting.
    /// </summary>
    public static void CyclopsFunction(GameState state, string verb, GameObject? gift)
    {
        var world = state.World!;
        var room = state.Here;
        var cyclops = world.FindObject("CYCLO")!;
        var count = room.RVars;
        if (state.GetFlag("CYCLOPS-FLAG!-FLAG"))
        {
            if (verb is "WAKE" or "MUNG" or "BURN" or "ATTACK")
            {
                state.Output.WriteLine("The cyclops yawns and stares at the thing that woke him up.");
                state.SetFlag("CYCLOPS-FLAG!-FLAG", false);
                cyclops.Flags |= ObjectFlags.Fighting;
                room.RVars = Math.Abs(room.RVars);
            }
            return;
        }
        if (Math.Abs(count) > 5)
        {
            state.JigsUp("The cyclops, tired of all of your games and trickery, eats you.\nThe cyclops says 'Mmm.  Just like mom used to make 'em.'");
            return;
        }
        if (verb == "GIVE" && gift != null)
        {
            switch (gift.Id)
            {
                case "FOOD":
                    if (count >= 0)
                    {
                        RemoveFromWhere(gift);
                        state.Output.WriteLine("The cyclops says 'Mmm Mmm.  I love hot peppers!  But oh, could I use a drink.  Perhaps I could drink the blood of that thing'.  From the gleam in his eye, it could be surmised that you are 'that thing'.");
                        room.RVars = Math.Min(-1, -count);
                    }
                    break;
                case "WATER":
                    if (count < 0)
                    {
                        RemoveFromWhere(gift);
                        cyclops.Flags &= ~ObjectFlags.Fighting;
                        state.Output.WriteLine("The cyclops looks tired and quickly falls fast asleep (what did you put in that drink, anyway?).");
                        state.SetFlag("CYCLOPS-FLAG!-FLAG", true);
                    }
                    else
                        state.Output.WriteLine("The cyclops apparently was not thirsty at the time and refuses your generous gesture.");
                    break;
                case "GARLI":
                    state.Output.WriteLine("The cyclops may be hungry, but there is a limit.");
                    room.RVars = AosSos(state, count);
                    break;
                default:
                    state.Output.WriteLine("The cyclops is not so stupid as to eat THAT!");
                    room.RVars = AosSos(state, count);
                    break;
            }
            return;
        }
        if (verb == "ATTACK") return; // FIGHT: the melee's business
        room.RVars = AosSos(state, count);
        if (verb is "THROW" or "MUNG")
            state.Output.WriteLine(state.Random.Next(100) < 50
                ? "Your actions don't appear to be doing much harm to the cyclops, but they do not exactly lower your insurance premiums, either."
                : "The cyclops ignores all injury to his body with a shrug.");
        else if (verb == "TAKE")
            state.Output.WriteLine("The cyclops is rather heavy and doesn't take kindly to being grabbed.");
        else if (verb == "TIE")
            state.Output.WriteLine("You cannot tie the cyclops, although he is fit to be tied.");
    }
}
