namespace ZorkDotNet.Game;

/// <summary>
/// The robot (act3.13 ROBOT-FUNCTION, ROBOT-ACTOR, BUTTONS, SPHERE-FUNCTION, CAGED-ROOM, MAGNET-ROOM, CMACH-ROOM) and
/// rooms.99 COMMAND: TELL ROBOT '...' makes it the actor (MDL WINNER) for one command. Like an adventurer it has a
/// room and things it carries (<see cref="GameState.Robot"/>); the ROBOT object is its body.
/// </summary>
public static class Robot
{
    /// <summary>dung.56 ROBOT-ACTIONS: what the robot can be told to do.</summary>
    private static readonly HashSet<string> Actions = ["WALK", "TAKE", "DROP", "PUT", "JUMP", "PUSH", "THROW", "TURN"];

    private const string Stupid = "\"I am only a stupid robot and cannot perform that command.\"";

    /// <summary>The robot's body, if it still exists in some room.</summary>
    private static GameObject? Body(GameState state)
    {
        var robot = state.World?.FindObject(Obj.Robot);
        return robot?.InRoom != null ? robot : null;
    }

    /// <summary>rooms.99 COMMAND: TELL obj '...'. Only an actor can be told anything.</summary>
    public static void Tell(GameState state, GameObject? actor, string[] command)
    {
        if (actor?.Id != Obj.Robot || Body(state) == null)
        {
            state.Output.WriteLine("You cannot talk to that!");
            return;
        }
        var words = command.Select(w => w.Trim('\'', '"')).Where(w => w.Length > 0).ToArray();
        if (words.Length == 0) return;
        state.RobotActing = true;
        try
        {
            Act(state, words);
        }
        finally
        {
            state.RobotActing = false;
        }
    }

    private static void Act(GameState state, string[] words)
    {
        var robot = state.Robot;
        var first = Parser.NormWord(words[0]);
        var direction = Parser.Direction(first);
        string action;
        GameObject? direct = null, indirect = null;
        if (direction != null)
            action = "WALK";
        else if (!VerbTable.Table.TryGetValue(first, out var spec))
        {
            state.Output.WriteLine("I don't know the word \"" + words[0] + "\".");
            return;
        }
        else
        {
            action = spec.ActionName;
            if (action == "WALK" && words.Length > 1) direction = Parser.Direction(Parser.NormWord(words[1]));
            var preps = new[] { "IN", "INTO", "INSID", "WITH", "AT", "TO", "ON" };
            var split = Array.FindIndex(words, 1, w => preps.Contains(Parser.NormWord(w)));
            var objectWords = split < 0 ? words.Skip(1) : words.Skip(1).Take(split - 1);
            direct = Find(state, objectWords);
            if (split >= 0) indirect = Find(state, words.Skip(split + 1));
            state.LastDirectObject = direct;
            state.LastIndirectObject = indirect;
        }

        // act3.13 ROBOT-ACTOR
        if (action is "RAISE" or "LIFT" && direct?.Id == Obj.Cage) { RaiseCage(state); return; }
        if (action is "EAT" or "DRINK") { state.Output.WriteLine("\"I am sorry but that action is difficult in the absence of a mouth.\""); return; }
        if (action == "READ") { state.Output.WriteLine("\"My vision is not that good without eyes.\""); return; }
        if (!Actions.Contains(action)) { state.Output.WriteLine(Stupid); return; }

        switch (action)
        {
            case "WALK": Walk(state, direction); break;
            case "TAKE":
                if (direct == null) state.Output.WriteLine("I can't see that here.");
                else if (!direct.CanTake)
                {
                    if (!ObjectActions.Try(state, direct, "TAKE", null)) state.Output.WriteLine("You can't take that.");
                }
                else
                {
                    direct.MoveTo(robot);
                    state.Output.WriteLine("Taken.");
                }
                break;
            case "DROP":
                if (direct == null || direct.Carrier != robot) state.Output.WriteLine("You're not carrying that.");
                else
                {
                    direct.MoveTo(robot.CurrentRoom);
                    state.Output.WriteLine("Dropped.");
                }
                break;
            case "PUT":
                if (direct == null || direct.Carrier != robot || indirect == null || !indirect.Open)
                    state.Output.WriteLine("I can't do that.");
                else if (indirect.Contents.Sum(o => o.Size) + direct.Size > indirect.Capacity)
                    state.Output.WriteLine("It won't fit.");
                else
                {
                    direct.MoveTo(indirect);
                    state.Output.WriteLine("Done.");
                }
                break;
            case "PUSH":
                if (direct == null) state.Output.WriteLine("I can't see that here.");
                else if (!ObjectActions.Try(state, direct, "PUSH", null))
                    state.Output.WriteLine("Pushing the " + direct.DescriptionShort + " doesn't do anything.");
                break;
            default:
                // JUMP, THROW and TURN are in ROBOT-ACTIONS, but nothing in the dungeon needs the robot to do them;
                // the port doesn't carry them out for it.
                state.Output.WriteLine(Stupid);
                break;
        }
    }

    /// <summary>An object the robot can reach: in its room (and open containers there) or in its hands.</summary>
    private static GameObject? Find(GameState state, IEnumerable<string> words)
    {
        var robot = state.Robot;
        var scope = robot.Inventory.Concat(robot.CurrentRoom.Objects)
            .SelectMany(o => o.IsOpenOrTransparent ? o.Contents.Where(c => c.IsVisible).Prepend(o) : [o])
            .Where(o => o.IsVisible || robot.Inventory.Contains(o)).ToList();
        var normalized = words.Select(Parser.NormWord).Where(w => w.Length > 0).ToList();
        foreach (var w in normalized)
        {
            var matches = scope.Where(o => Parser.Matches(o, w)).ToList();
            if (matches.Count == 0) continue;
            var adjectives = normalized.Where(a => a != w).ToList();
            return matches.FirstOrDefault(o => adjectives.All(a => o.Adjectives.Contains(a))) ?? matches[0];
        }
        return null;
    }

    private static void Detach(GameObject o) => o.MoveTo(null);

    /// <summary>The robot walks (rooms.99 WALK and GOTO for an actor). In the low room only SE and EAST lead anywhere.</summary>
    private static void Walk(GameState state, string? direction)
    {
        var robot = state.Robot;
        var here = robot.CurrentRoom;
        if (direction == null || !here.Exits.TryGetValue(direction, out var exit))
        {
            state.Output.WriteLine("There is no way to go in that direction.");
            return;
        }
        var magnet = here.Id == RoomId.Magne && direction is "SE" or "EAST"; // MAGNET-ROOM
        if (exit.IsBlocked)
        {
            state.Output.WriteLine(exit.BlockedMessage);
            return;
        }
        if (exit.IsConditional && !magnet && !state.GetFlag(exit.ConditionFlag!))
        {
            if (exit.BlockedMessage == null) state.Output.WriteLine("There is no way to go in that direction.");
            else if (exit.BlockedMessage.Length > 0) state.Output.WriteLine(exit.BlockedMessage);
            return;
        }
        var to = state.World!.FindRoom(exit.RoomId!)!;
        if (to.IsWater || to.IsAir)
        {
            state.Output.WriteLine("You can't go there without a vehicle.");
            return;
        }
        if (to.Munged)
        {
            state.Output.WriteLine(to.MungedMessage);
            return;
        }
        MoveTo(state, to);
        // ROOM-INFO for an actor elsewhere just says "Done."; one walking in to you is seen arriving.
        if (to == state.Here) Parser.Describe(state);
        else state.Output.WriteLine("Done.");
    }

    private static void MoveTo(GameState state, Room to)
    {
        var body = state.World!.FindObject(Obj.Robot)!;
        body.MoveTo(to);
        state.Robot.CurrentRoom = to;
    }

    /// <summary>act3.13 ROBOT-ACTOR for RAISE CAGE: the robot frees you.</summary>
    private static void RaiseCage(GameState state)
    {
        var world = state.World!;
        state.Output.WriteLine("The cage shakes and is hurled across the room.");
        state.ScheduledEvents.RemoveAll(e => e.Name == Clocks.Sphere);
        var closet = world.FindRoom(RoomId.Cager)!;
        Parser.Goto(state, closet);
        var cage = world.FindObject(Obj.Cage)!;
        cage.MoveTo(closet);
        cage.Flags = (cage.Flags | ObjectFlags.Take) & ~ObjectFlags.NoDescribe;
        var body = world.FindObject(Obj.Robot)!;
        body.Flags &= ~ObjectFlags.NoDescribe;
        world.FindObject(Obj.Spher)!.Flags |= ObjectFlags.Take;
        MoveTo(state, closet);
        state.SetFlag(Flag.CageSolve, true);
    }

    /// <summary>act3.13 ROBOT-FUNCTION: what is done to the robot. It takes gifts; thrown at or broken, it's destroyed.</summary>
    public static void RobotFunction(GameState state, string verb, GameObject? other)
    {
        var body = state.World!.FindObject(Obj.Robot)!;
        if (verb == "GIVE" && other != null)
        {
            other.MoveTo(state.Robot, first: true);
            state.Output.WriteLine("The robot gladly takes the " + other.DescriptionShort + " and nods his head-like appendage in thanks.");
        }
        else if (verb is "THROW" or "MUNG")
        {
            state.Output.WriteLine("The robot is injured (being of shoddy construction) and falls to the floor in a pile of garbage, which disintegrates before your eyes.");
            Detach(body);
        }
    }

    /// <summary>
    /// act3.13 BUTTONS: the machine room's buttons. For you a fatal shock; for the robot the square one speeds the
    /// carousel up (CAROUSEL-ZOOM), the round one slows it, and the triangular one stops or starts it (CAROUSEL-FLIP),
    /// dropping the iron box into the round room.
    /// </summary>
    public static void Buttons(GameState state, string verb, GameObject? _)
    {
        if (verb != "PUSH") return;
        if (!state.RobotActing)
        {
            state.JigsUp("There is a giant spark and you are fried to a crisp.");
            return;
        }
        switch (state.LastDirectObject?.Id)
        {
            case Obj.Sqbut:
                if (state.GetFlag(Flag.CarouselZoom)) state.Output.WriteLine("Nothing seems to happen.");
                else
                {
                    state.SetFlag(Flag.CarouselZoom, true);
                    state.Output.WriteLine("The whirring increases in intensity slightly.");
                }
                break;
            case Obj.Rnbut:
                if (state.GetFlag(Flag.CarouselZoom))
                {
                    state.SetFlag(Flag.CarouselZoom, false);
                    state.Output.WriteLine("The whirring decreases in intensity slightly.");
                }
                else state.Output.WriteLine("Nothing seems to happen.");
                break;
            case Obj.Trbut:
                state.SetFlag(Flag.CarouselFlip, !state.GetFlag(Flag.CarouselFlip));
                var box = state.World!.FindObject(Obj.Irbox)!;
                if (box.InRoom?.Id == RoomId.Carou)
                {
                    state.Output.WriteLine("A dull thump is heard in the distance.");
                    box.Flags ^= ObjectFlags.Visible;
                }
                break;
        }
    }

    // act3.13 POISON
    private const string Poison = "Time passes...and you die from some obscure poisoning.";

    /// <summary>
    /// act3.13 SPHERE-FUNCTION: reaching for the sphere drops an iron cage; with the robot there you're caged
    /// together while the gas comes (ten moves), alone you die at once. The robot reaching for it is crushed.
    /// </summary>
    public static void SphereFunction(GameState state, string verb, GameObject? _)
    {
        if (verb != "TAKE" || state.GetFlag(Flag.CageSolve)) return;
        var world = state.World!;
        var sphere = world.FindObject(Obj.Spher)!;
        var body = world.FindObject(Obj.Robot)!;
        var closet = world.FindRoom(RoomId.Cager)!;
        if (state.RobotActing)
        {
            state.Output.WriteLine("As the robot reaches for the sphere, an iron cage falls from the ceiling.  The robot attempts to fend it off, but is trapped below it. Alas, the robot short-circuits in his vain attempt to escape, and crushes the sphere beneath him as he falls to the floor.");
            state.Output.WriteLine("The robot has died."); // JIGS-UP for an actor
            Detach(body);
            sphere.Flags &= ~ObjectFlags.Visible;
            var rcage = world.FindObject(Obj.Rcage)!;
            rcage.MoveTo(state.Robot.CurrentRoom);
            return;
        }
        state.Output.WriteLine("As you reach for the sphere, an iron cage falls from the ceiling to entrap you.  To make matters worse, poisonous gas starts coming into the room.");
        if (body.InRoom == state.Here)
        {
            var caged = world.FindRoom(RoomId.Caged)!;
            Parser.Goto(state, caged);
            MoveTo(state, caged);
            body.Flags |= ObjectFlags.NoDescribe;
            state.Schedule(10, Clocks.Sphere);
        }
        else
        {
            sphere.Flags &= ~ObjectFlags.Visible;
            state.MungRoom("CAGER", "You are stopped by a cloud of poisonous gas.");
            state.JigsUp(Poison);
        }
    }

    /// <summary>SPHERE-FUNCTION on its clock (SPHIN): the gas gets you.</summary>
    public static void SphereClock(GameState state)
    {
        state.MungRoom("CAGER", "You are stopped by a cloud of poisonous gas.");
        state.JigsUp(Poison);
    }

    /// <summary>act3.13 CAGED-ROOM: once the robot has thrown the cage off, you are in the closet.</summary>
    public static void CagedRoom(GameState state, string _)
    {
        if (state.GetFlag(Flag.CageSolve) && state.Here.Id == RoomId.Caged)
            state.Winner.CurrentRoom = state.World!.FindRoom(RoomId.Cager)!;
    }

    /// <summary>act3.13 CMACH-ROOM</summary>
    public static void CmachRoom(GameState state, string verb)
    {
        if (verb == "LOOK")
            state.Output.WriteLine("You are in a large room full of assorted heavy machinery.  The room smells of burned resistors. The room is noisy from the whirring sounds of the machines. Along one wall of the room are three buttons which are, respectively, round, triangular, and square.  Naturally, above these buttons are instructions written in EBCDIC.  A large sign in English above all the buttons says\n\t\t'DANGER -- HIGH VOLTAGE '.\nThere are exits to the west and the south.");
    }

    // act3.13 SPINDIZZY
    public const string SpinDizzy = "According to Prof. TAA of MIT Tech, the rapidly changing magnetic fields in the room are so intense as to cause you to be electrocuted.  I really don't know, but in any event, something just killed you.";

    /// <summary>act3.13 MAGNET-ROOM: its description, and what walking in does while the carousel spins.</summary>
    public static void MagnetRoom(GameState state, string verb)
    {
        if (verb == "LOOK")
            state.Output.WriteLine("You are in a room with a low ceiling which is circular in shape.  There are exits to the east and the southeast.");
        else if (verb == "WALK-IN" && state.GetFlag(Flag.CarouselFlip))
        {
            if (state.GetFlag(Flag.CarouselZoom)) state.JigsUp(SpinDizzy);
            else state.Output.WriteLine("As you enter, your compass starts spinning wildly.");
        }
    }

    public static void MagnetEnter(GameState state) => MagnetRoom(state, "WALK-IN");

    /// <summary>
    /// act3.13 MAGNET-ROOM for WALK: with the carousel spinning you lose your bearings and go anywhere; otherwise
    /// only SE and EAST lead out (the others are FROBOZZ exits, which never open). Returns true when it moved you.
    /// </summary>
    public static bool MagnetWalk(GameState state, string direction)
    {
        var here = state.Here;
        if (state.GetFlag(Flag.CarouselFlip))
        {
            state.Output.WriteLine("You cannot get your bearings...");
            var exits = here.Exits.Values.ToList();
            var to = state.World!.FindRoom(exits[state.Random.Next(exits.Count)].RoomId!)!;
            Parser.MoveToRoom(state, to);
            return true;
        }
        if (direction is "SE" or "EAST" && here.Exits.TryGetValue(direction, out var exit))
        {
            Parser.MoveToRoom(state, state.World!.FindRoom(exit.RoomId!)!);
            return true;
        }
        return false;
    }

    /// <summary>ROOM-INFO for an actor in the room: what it carries (rooms.99 INVENT for another adventurer).</summary>
    public static void DescribeCarrying(GameState state)
    {
        var carried = state.Robot.Inventory.Where(o => o.IsVisible).ToList();
        if (carried.Count == 0) return;
        state.Output.WriteLine("The robot is carrying:");
        foreach (var o in carried) state.Output.WriteLine("A " + o.DescriptionShort);
    }
}
