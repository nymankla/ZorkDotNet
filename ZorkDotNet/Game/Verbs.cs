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

    /// <summary>MDL PICK-ONE.</summary>
    private static string PickOne(string[] choices) => choices[Random.Shared.Next(choices.Length)];

    /// <summary>
    /// Dispatches the verbs of this class. Returns false when <paramref name="action"/> isn't one of them.
    /// </summary>
    public static bool Run(GameState state, string action, GameObject? direct)
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
                if (state.Here?.Id != "SAFE") output.WriteLine("I don't really know how to do that.");
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
            default: return false;
        }
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
        var forest = state.World?.FindRoom("FORE1");
        if (state.Here?.Id == "TEMP2" && forest != null) Parser.MoveToRoom(state, forest);
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
        if (state.Here?.Id == "BARRE")
            state.JigsUp("I didn't think you would REALLY try to go over the falls in a barrel. It seems that some 450 feet below, you were met by a number of  unfriendly rocks and boulders, causing your immediate demise.  Is this what 'over a barrel' means?");
        else
            state.Output.WriteLine("Wasn't he an Indian?");
    }

    /// <summary>act1.38 SINBAD (the words ODYSSEUS and ULYSSES): the cyclops flees through the north wall.</summary>
    private static void Sinbad(GameState state)
    {
        var cyclops = state.World?.FindObject("CYCLO");
        if (state.Here?.Id == "CYCLO" && cyclops != null && cyclops.InRoom == state.Here)
        {
            state.SetFlag("CYCLOPS-FLAG!-FLAG", true);
            state.Output.WriteLine("The cyclops, hearing the name of his deadly nemesis, flees the room by knocking down the wall on the north of the room.");
            state.SetFlag("MAGIC-FLAG!-FLAG", true);
            state.Here.Objects.Remove(cyclops);
            cyclops.InRoom = null;
        }
        else
            state.Output.WriteLine("Wasn't he a sailor?");
    }

    /// <summary>act1.38 HELLO; HS counts every hello, as in the original.</summary>
    private static void Hello(GameState state, GameObject? obj)
    {
        var count = ++state.HelloCount;
        if (obj == null)
            state.Output.WriteLine(PickOne(Hellos));
        else if (obj.Id == "SAILO")
            state.Output.WriteLine(count % 20 == 0 ? "You seem to be repeating yourself."
                : count % 10 == 0 ? "I think that phrase is getting a bit worn out."
                : "Nothing happens here.");
        else if (obj.Id == "AVIAT")
            state.Output.WriteLine("Here, nothing happens.");
        else
            state.Output.WriteLine("I think that only schizophrenics say 'Hello' to a " + obj.DescriptionShort + ".");
    }

    /// <summary>
    /// rooms.99 ROOM-INFO for an object (EXAMINE x, LOOK AT x): its reading text, or nothing special. (The original
    /// asks the object's action first; the port's actions don't know EXAMINE yet.)
    /// </summary>
    private static void Examine(GameState state, GameObject? obj)
    {
        if (obj == null)
        {
            state.Output.WriteLine("What do you want to examine?");
            return;
        }
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
