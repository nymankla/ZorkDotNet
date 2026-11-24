namespace ZorkDotNet.Game;

/// <summary>
/// Verbs whose whole behavior is a short MDL routine (act1.38, act2.27, act3.13, rooms.99): mostly fixed or random
/// replies, and a few that move you (TREAS, PRAYER) or kill you (LEAPER, GERONIMO).
/// </summary>
public static class Verbs
{
    // act1.38 OFFENDED
    private static readonly string[] Offended =
    [
        "Such language in a high-class establishment like this!",
        "You ought to be ashamed of yourself.",
        "Its not so bad.  You could have been killed already.",
        "Tough shit, asshole.",
        "Oh, dear.  Such language from a supposed winning adventurer!",
    ];

    // act1.38 WHEEEEE
    private static readonly string[] Wheeeeee =
    [
        "Very good.  Now you can go to the second grade.",
        "Have you tried hopping around the dungeon, too?",
        "Are you enjoying yourself?",
        "Wheeeeeeeeee!!!!!",
        "Do you expect me to applaud?",
    ];

    // act1.38 JUMPLOSS
    private static readonly string[] JumpLoss =
    [
        "You should have looked before you leaped.",
        "I'm afraid that leap was a bit much for your weak frame.",
        "In the movies, your life would be passing in front of your eyes.",
        "Geronimo.....",
    ];

    // act1.38 HELLOS
    private static readonly string[] Hellos =
    [
        "Hello.",
        "Good day.",
        "Nice weather we've been having lately",
        "How are you?",
        "Goodbye.",
    ];

    // act1.38 HO-HUM
    private static readonly string[] HoHum =
    [
        " does not seem to do anything.",
        " is not notably useful.",
        " isn't very interesting.",
        " doesn't appear worthwhile.",
        " has no effect.",
        " doesn't do anything.",
    ];

    // act2.27 SWIMYUKS
    private static readonly string[] SwimYuks =
    [
        "I don't really see how.",
        "I think that swimming is best performed in water.",
        "Perhaps it is your head that is swimming.",
    ];

    /// <summary>dung.56: the rooms with RFILLBIT (water you could swim in).</summary>
    private static readonly HashSet<string> WaterRooms =
        ["RESEN", "RESES", "DAM", "STREA", "RIVR1", "RIVR2", "RIVR3", "RIVR4", "RIVR5", "BEACH", "RCAVE", "DOCK", "WCLF1", "WCLF2", "FANTE", "POG"];

    // rooms.99 REMARKABLY-DISGUSTING-CODE
    private static readonly string[] Months =
        ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    /// <summary>MDL PICK-ONE.</summary>
    private static string PickOne(string[] choices) => choices[Random.Shared.Next(choices.Length)];

    /// <summary>
    /// Dispatches the verbs of this class. Returns false when <paramref name="action"/> isn't one of them.
    /// </summary>
    public static bool Run(GameState state, string action, GameObject? direct, GameObject? indirect = null)
    {
        var output = state.Output;
        switch (action)
        {
            case "BACK": // rooms.99 BACKER
                output.WriteLine("He who puts his hand to the plow and looks back is not fit for the kingdom of winners.  In any case, \"back\" doesn't work.");
                return true;
            case "REPENT": output.WriteLine("It could very well be too late!"); return true; // act1.38 REPENT
            case "CURSE": output.WriteLine(PickOne(Offended)); return true; // act1.38 CURSES
            case "JARGON": output.WriteLine("Well, FOO, BAR, and BLETCH to you too!"); return true;
            case "CHOMP": output.WriteLine("I don't know how to do that.  I win in all cases!"); return true; // act3.13
            case "YELL": output.WriteLine("Aaaarrrrrrrrgggggggggggggghhhhhhhhhhhhhh!"); return true; // act3.13
            case "WIN": output.WriteLine("Naturally!"); return true; // act3.13
            case "FROBOZZ": output.WriteLine("The FROBOZZ Corporation created, owns, and operates this dungeon."); return true;
            case "SKIP": output.WriteLine(PickOne(Wheeeeee)); return true; // act1.38 SKIPPER
            case "MUMBLE": output.WriteLine("You'll have to speak up if you expect me to hear you!"); return true;
            case "ZORK": output.WriteLine("That word is replaced henceforth with DUNGEON."); return true;
            case "DUNGEON": output.WriteLine("At your service!"); return true;
            case "GRANITE": output.WriteLine("I think you are taking this thing for granite."); return true;
            case "BLAST": // act2.27 BLAST (the safe explosion isn't ported yet; elsewhere this is the reply)
                if (state.Here?.Id != RoomId.Safe) output.WriteLine("I don't really know how to do that.");
                return true;
            case "TREASURE": Teleport(state, from: "TEMP1", to: "TREAS"); return true; // act1.38 TREAS
            case "TEMPLE": Teleport(state, from: "TREAS", to: "TEMP1"); return true;
            case "PRAY": Prayer(state); return true;
            case "JUMP": Leaper(state); return true;
            case "GERONIMO": Geronimo(state); return true;
            case "ODYSSEUS": Sinbad(state); return true;
            case "HELLO": Hello(state, direct); return true;
            case "EXAMINE": Examine(state, direct); return true;
            case "TIME": PlayTime(state); return true;
            case "FIRST": return true; // dung.56 FIRST? is an internal verb (TIME); typed, it does nothing
            case "HELP": output.Write(HelpText.Text); return true;
            case "VERSION": Version(state); return true;
            case "WAIT": Wait(state); return true;
            case "SWIM": // act2.27 SWIMMER
                output.WriteLine(WaterRooms.Contains(state.Here.Id) ? "Swimming is not allowed in this dungeon." : PickOne(SwimYuks));
                return true;
            case "KICK": HackHack(state, direct, "KICK", "Munging a "); return true; // act1.38 KICKER
            case "PUSH": Pusher(state, direct); return true;
            case "MUNG": Munger(state, direct, indirect); return true;
            case "RING": // act1.38 RING
                output.WriteLine(direct?.Id == Obj.Bell ? "Ding, dong." : "How, exactly, can I ring that?");
                return true;
            case "BRUSH": Brush(state, direct, indirect); return true;
            case "WAKE": Alarm(state, direct); return true;
            case "RAISE" or "LOWER": HackHack(state, direct, action, "Playing in this way with a "); return true; // act1.38 R/L
            case "MELT": // act1.38 MELTER
                if (direct != null && !ObjectAction(state, direct, "MELT", indirect))
                    output.WriteLine("I'm not sure that a " + direct.DescriptionShort + " can be melted.");
                return true;
            case "PLUG": // act1.38 PLUGGER
                if (direct == null || !ObjectAction(state, direct, "PLUG", indirect)) output.WriteLine("This has no effect.");
                return true;
            case "DIG": Digger(state, direct); return true;
            case "INFLATE": Inflater(state, direct, indirect); return true;
            case "DEFLATE": // act1.38 DEFLATER
                if (direct?.Id != Obj.Rboat || !ObjectAction(state, direct, "DEFLATE", null)) output.WriteLine("Come on, now!");
                return true;
            case "TELL": // rooms.99 COMMAND: there are no actors to command yet (the robot isn't ported)
                output.WriteLine("You cannot talk to that!");
                return true;
            case "KNOCK": // act3.13 KNOCK
                if (direct == null) output.WriteLine("Knock on what?");
                else if (direct.Id == Obj.Door || direct.Synonyms.Contains("DOOR")) output.WriteLine("I don't think that anybody's home.");
                else output.WriteLine("Why knock on a " + direct.DescriptionShort + "?");
                return true;
            case "FIND": Find(state, direct); return true;
            default: return false;
        }
    }

    /// <summary>
    /// MDL OBJECT-ACTION for one object: run its action and report whether it dealt with the verb, which here means
    /// it said something.
    /// </summary>
    private static bool ObjectAction(GameState state, GameObject obj, string verb, GameObject? indirect) =>
        ObjectActions.Try(state, obj, verb, indirect);

    /// <summary>act1.38 HACK-HACK: the object's own action, or "Munging a mailbox does not seem to do anything."</summary>
    private static void HackHack(GameState state, GameObject? obj, string verb, string prefix)
    {
        if (obj == null)
            state.Output.WriteLine("What do you want to do that to?");
        else if (!ObjectAction(state, obj, verb, null))
            state.Output.WriteLine(prefix + obj.DescriptionShort + PickOne(HoHum));
    }

    /// <summary>
    /// act1.38 PUSHER: the object's action; for a button, nothing here, and RDCOM then lets the room's action
    /// handle the push (the maintenance room's panel).
    /// </summary>
    private static void Pusher(GameState state, GameObject? obj)
    {
        if (obj == null)
        {
            state.Output.WriteLine("What do you want to push?");
            return;
        }
        if (ObjectAction(state, obj, "PUSH", null)) return;
        // A button is left to the room's action, which RDCOM runs after every verb.
        if (obj.Id != Obj.Butto && !obj.Synonyms.Contains("BUTTO"))
            state.Output.WriteLine("Pushing the " + obj.DescriptionShort + PickOne(HoHum));
    }

    /// <summary>act1.38 MUNGER for something that isn't a villain (POKE/JAB at a villain is an attack).</summary>
    private static void Munger(GameState state, GameObject? obj, GameObject? with)
    {
        if (obj == null || !obj.IsVillain)
        {
            HackHack(state, obj, "MUNG", "Munging a ");
            return;
        }
        if (with == null)
            state.Output.WriteLine("Munging a " + obj.DescriptionShort + " with your bare hands is suicidal.");
        else
            state.Output.WriteLine("Munging a " + obj.DescriptionShort + " with a " + with.DescriptionShort + " is quite self-destructive.");
    }

    /// <summary>act1.38 BRUSH (the teeth belong to the unported mirror-room puzzle).</summary>
    private static void Brush(GameState state, GameObject? obj, GameObject? with)
    {
        if (obj?.Id != Obj.Teeth)
            state.Output.WriteLine("If you wish, but I can't understand why??");
        else if (with?.Id == Obj.Putty && with.Carrier == state.Winner)
            state.JigsUp("Well, you seem to have been brushing your teeth with some sort of glue. As a result, your mouth gets glued together (with your nose) and you die of respiratory failure.");
        else if (with == null)
            state.Output.WriteLine("Dental hygiene is highly recommended, but I'm not sure what you want to brush them with.");
        else
            state.Output.WriteLine("A nice idea, but with a " + with.DescriptionShort + "?");
    }

    /// <summary>act1.38 ALARM: only something asleep (the drugged cyclops) can be woken.</summary>
    private static void Alarm(GameState state, GameObject? obj)
    {
        if (obj == null) return;
        var asleep = obj.Id == Obj.Cyclo && state.GetFlag(Flag.Cyclops) && !state.GetFlag(Flag.Magic);
        if (!asleep || !ObjectAction(state, obj, "WAKE", null))
            state.Output.WriteLine("The " + obj.DescriptionShort + " isn't sleeping.");
    }

    /// <summary>act2.27 DIGGER (DIG WITH tool). Digging with the shovel belongs to the unported sandy cave.</summary>
    private static void Digger(GameState state, GameObject? tool)
    {
        if (tool == null)
            state.Output.WriteLine("Dig with what?");
        else if (tool.Id == Obj.Shove)
            return;
        else if ((tool.Flags & ObjectFlags.Tool) != 0)
            state.Output.WriteLine("Digging with the " + tool.DescriptionShort + " is slow and tedious.");
        else
            state.Output.WriteLine("Digging with a " + tool.DescriptionShort + " is silly.");
    }

    /// <summary>act1.38 INFLATER (INFLATE obj WITH tool).</summary>
    private static void Inflater(GameState state, GameObject? obj, GameObject? tool)
    {
        tool ??= ObjectActions.FindOnlyTool(state); // GWIM: INFLATE BOAT with the one tool you carry
        if (obj?.Id == Obj.Iboat)
        {
            if (tool?.Id != Obj.Pump || !ObjectAction(state, obj, "INFLATE", tool))
                state.Output.WriteLine("You would inflate it with that?");
        }
        else if (obj?.Id == Obj.Rboat)
            state.Output.WriteLine("Inflating it further would probably burst it.");
        else
            state.Output.WriteLine("How can you inflate that?");
    }

    /// <summary>rooms.99 FIND (WHERE, SEEK, SEE): the object's own action, or where it is relative to you.</summary>
    private static void Find(GameState state, GameObject? obj)
    {
        if (obj == null)
        {
            state.Output.WriteLine("I can't see that here.");
            return;
        }
        if (ObjectAction(state, obj, "FIND", null)) return;
        var told = FindFrob(state, obj, state.Here.Objects, ", which is in the room.", "There is a ", " here.");
        told |= FindFrob(state, obj, state.Winner.Inventory, ", which you are carrying.", "You are carrying a ", ".");
        if (!told) state.Output.WriteLine("I can't see that here.");
    }

    /// <summary>rooms.99 FIND-FROB: look for the object in a list, and one level into open or transparent containers.</summary>
    private static bool FindFrob(GameState state, GameObject obj, IReadOnlyList<GameObject> list, string where, string before, string after)
    {
        var told = false;
        foreach (var x in list)
        {
            if (x == obj)
            {
                state.Output.WriteLine(before + x.DescriptionShort + after);
                told = true;
            }
            else if (x.IsTransparent || (x.IsContainer && x.Open))
            {
                foreach (var y in x.Contents.Where(y => y == obj))
                {
                    state.Output.WriteLine(before + y.DescriptionShort + after);
                    state.Output.WriteLine("It is in the " + x.DescriptionShort + where);
                    told = true;
                }
            }
        }
        return told;
    }

    /// <summary>rooms.99 VERSION: the date this program was made (the original printed the date of its saved image).</summary>
    private static void Version(GameState state)
    {
        var path = Environment.ProcessPath;
        var date = path != null && File.Exists(path) ? File.GetLastWriteTime(path) : DateTime.Now;
        state.Output.WriteLine("This version created " + Months[date.Month - 1] + " " + date.Day + ".");
    }

    /// <summary>rooms.99 WAIT: "Time passes..." while the clock runs up to three more times, stopping when something happens.</summary>
    private static void Wait(GameState state)
    {
        state.Output.WriteLine("Time passes...");
        for (var i = 0; i < 3; i++)
            if (state.ClockTick()) break;
    }

    /// <summary>act1.38 TREAS: the magic words between the temple and the treasure room.</summary>
    private static void Teleport(GameState state, string from, string to)
    {
        var target = state.World?.FindRoom(to);
        if (state.Here?.Id == from && target != null) Parser.MoveToRoom(state, target);
        else state.Output.WriteLine("Nothing happens.");
    }

    /// <summary>act1.38 PRAYER: praying at the altar takes you to the forest.</summary>
    private static void Prayer(GameState state)
    {
        var forest = state.World?.FindRoom(RoomId.Fore1);
        if (state.Here?.Id == RoomId.Temp2 && forest != null) Parser.MoveToRoom(state, forest);
        else state.Output.WriteLine("If you pray enough, your prayers may be answered.");
    }

    /// <summary>
    /// act1.38 LEAPER: jumping where the way down is blocked is fatal; where it's open nothing happens; with no way
    /// down at all, a joke.
    /// </summary>
    private static void Leaper(GameState state)
    {
        if (state.Here.Exits.TryGetValue("DOWN", out var down))
        {
            if (down.IsBlocked || (down.IsConditional && !state.GetFlag(down.ConditionFlag!)))
                state.JigsUp(PickOne(JumpLoss));
            else
                state.Output.WriteLine("Nothing happens."); // RDCOM's reply when a command says nothing
        }
        else
            state.Output.WriteLine(PickOne(Wheeeeee));
    }

    /// <summary>act2.27 GERONIMO</summary>
    private static void Geronimo(GameState state)
    {
        if (state.Here?.Id == RoomId.Barre)
            state.JigsUp("I didn't think you would REALLY try to go over the falls in a barrel. It seems that some 450 feet below, you were met by a number of  unfriendly rocks and boulders, causing your immediate demise.  Is this what 'over a barrel' means?");
        else
            state.Output.WriteLine("Wasn't he an Indian?");
    }

    /// <summary>act1.38 SINBAD (the words ODYSSEUS and ULYSSES): the cyclops flees through the north wall.</summary>
    private static void Sinbad(GameState state)
    {
        var cyclops = state.World?.FindObject(Obj.Cyclo);
        if (state.Here?.Id == RoomId.Cyclo && cyclops != null && cyclops.InRoom == state.Here)
        {
            state.SetFlag(Flag.Cyclops, true);
            state.Output.WriteLine("The cyclops, hearing the name of his deadly nemesis, flees the room by knocking down the wall on the north of the room.");
            state.SetFlag(Flag.Magic, true);
            cyclops.MoveTo(null);
        }
        else
            state.Output.WriteLine("Wasn't he a sailor?");
    }

    /// <summary>act1.38 HELLO; HS counts every hello, as in the original.</summary>
    private static void Hello(GameState state, GameObject? obj)
    {
        var count = ++state.Progress.HelloCount;
        if (obj == null)
            state.Output.WriteLine(PickOne(Hellos));
        else if (obj.Id == Obj.Sailo)
            state.Output.WriteLine(count % 20 == 0 ? "You seem to be repeating yourself."
                : count % 10 == 0 ? "I think that phrase is getting a bit worn out."
                : "Nothing happens here.");
        else if (obj.Id == Obj.Aviat)
            state.Output.WriteLine("Here, nothing happens.");
        else
            state.Output.WriteLine("I think that only schizophrenics say 'Hello' to a " + obj.DescriptionShort + ".");
    }

    /// <summary>rooms.99 ROOM-INFO for an object (EXAMINE x, LOOK AT x): its action, its reading text, or nothing special.</summary>
    private static void Examine(GameState state, GameObject? obj)
    {
        if (obj == null)
        {
            state.Output.WriteLine("What do you want to examine?");
            return;
        }
        if (!ObjectAction(state, obj, "EXAMINE", null))
            state.Output.WriteLine(obj.ReadText ?? "I see nothing special about the " + obj.DescriptionShort + ".");
    }

    /// <summary>rooms.99 PLAY-TIME: how long this game has been played, in real time.</summary>
    private static void PlayTime(GameState state)
    {
        var seconds = (long)(state.Clock.GetUtcNow() - state.StartedAt).TotalSeconds;
        static string Plural(long n, string unit) => n + " " + unit + (n == 1 ? "" : "s");
        var text = "You have been playing DUNGEON for ";
        if (seconds / 3600 > 0) text += Plural(seconds / 3600, "hour") + ", ";
        if (seconds / 60 % 60 > 0) text += Plural(seconds / 60 % 60, "minute") + ", and ";
        state.Output.WriteLine(text + Plural(seconds % 60, "second") + ".");
    }
}
