namespace ZorkDotNet.Game;

/// <summary>
/// The hot-air balloon in the volcano (act2.27 BALLOON, RISE-AND-SHINE, DECLINE-AND-FALL, PUT-BALLOON, WIRE-FUNCTION,
/// BURNUP, LEDGE-FUNCTION, VOLGNOME, GNOME-FUNCTION). The balloon (BALLO, a basket) is a vehicle for the air rooms
/// VAIR1-4. While something burns in its receptacle (BINF), its clock (BINT) makes it rise every three moves, and
/// otherwise it sinks; <see cref="GameState.BalloonLocation"/> is MDL BLOC, where the balloon is or is heading.
/// </summary>
public static class Balloon
{
    private static bool InIt(GameState state) => state.Winner.Vehicle?.Id == Obj.Ballo;

    /// <summary>The object burning in the receptacle (MDL BINF), if any.</summary>
    public static GameObject? Fuel(GameState state) =>
        state.GetFlag(Flag.Binf) && state.World?.FindObject(Obj.Recep)?.Contents is [var fuel, ..] ? fuel : null;

    /// <summary>
    /// BALLOON with READ-IN, before a verb while you're in the basket: walking only chooses where it goes (or is
    /// refused); the burning fuel can't be taken, the occupied receptacle takes nothing more. Returns true when it
    /// dealt with the command.
    /// </summary>
    public static bool ReadIn(GameState state, string action, GameObject? direct, GameObject? indirect, string? direction)
    {
        if (!InIt(state)) return false;
        if (action == "WALK")
        {
            if (direction != null && state.Here.Exits.TryGetValue(direction, out var exit))
            {
                if (state.GetFlag(Flag.Btie))
                {
                    state.Output.WriteLine("You are tied to the ledge.");
                    return true;
                }
                var target = exit.RoomId != null ? state.World?.FindRoom(exit.RoomId) : null;
                if (target != null && !target.Munged) state.BalloonLocation = target.Id;
                return false;
            }
            state.Output.WriteLine("I'm afraid you can't control the balloon in this way.");
            return true;
        }
        if (action == "TAKE" && direct != null && direct == Fuel(state))
        {
            state.Output.WriteLine("You don't really want to hold a burning " + direct.DescriptionShort + ".");
            return true;
        }
        if (action == "PUT" && indirect?.Id == Obj.Recep && indirect.Contents.Count > 0)
        {
            state.Output.WriteLine("The receptacle is already occupied.");
            return true;
        }
        return false;
    }

    /// <summary>BALLOON with READ-OUT after LOOK, while you're in the basket.</summary>
    public static void ReadOut(GameState state)
    {
        if (!InIt(state)) return;
        var fuel = Fuel(state);
        state.Output.WriteLine(fuel != null
            ? "The cloth bag is inflated and there is a " + fuel.DescriptionShort + " burning in the receptacle."
            : "The cloth bag is draped over the the basket.");
        if (state.GetFlag(Flag.Btie)) state.Output.WriteLine("The balloon is tied to the hook.");
    }

    /// <summary>BALLOON for BURN: something in the receptacle burns, the bag inflates and the balloon's clock starts.</summary>
    public static void Burn(GameState state, GameObject obj)
    {
        state.Output.WriteLine("The " + obj.DescriptionShort + " burns inside the receptacle.");
        state.ScheduledEvents.RemoveAll(e => e.Name == Clocks.Burnup);
        state.Schedule(obj.Size * 20, Clocks.Burnup);
        obj.Flags = (obj.Flags | ObjectFlags.OnFire) & ~(ObjectFlags.Take | ObjectFlags.Readable);
        obj.LightAmount = 1;
        if (state.GetFlag(Flag.Binf)) return;
        state.Output.WriteLine("The cloth bag inflates as it fills with hot air.");
        if (!state.GetFlag(Flag.Blab))
        {
            var label = state.World!.FindObject(Obj.Blabe)!;
            var ball = state.World.FindObject(Obj.Ballo)!;
            label.MoveTo(ball, first: true);
        }
        state.SetFlag(Flag.Blab, true);
        state.SetFlag(Flag.Binf, true);
        StartClock(state);
    }

    /// <summary>CLOCK-INT BINT 3.</summary>
    public static void StartClock(GameState state)
    {
        StopClock(state);
        state.Schedule(3, Clocks.Balloon);
    }

    public static void StopClock(GameState state) => state.ScheduledEvents.RemoveAll(e => e.Name == Clocks.Balloon);

    /// <summary>
    /// BALLOON as the basket's own action. BURN (of what's in the receptacle) and the clock come in through
    /// <see cref="Burn"/> and <see cref="Tick"/>; here only DISEMBARK on land, which restarts the clock of an inflated
    /// balloon. It says nothing, so UNBOARD goes on as usual.
    /// </summary>
    public static void BalloonAction(GameState state, string verb, GameObject? _)
    {
        if (verb == "DISEMBARK" && !state.Here.IsWater && !state.Here.IsAir && state.GetFlag(Flag.Binf)) StartClock(state);
    }

    /// <summary>BALLOON on its clock (C-INT): it rises while the receptacle is open and burning, or from a ledge; else it sinks.</summary>
    public static void Tick(GameState state)
    {
        var recep = state.World!.FindObject(Obj.Recep)!;
        if ((recep.Open && state.GetFlag(Flag.Binf)) || state.Here.Id.StartsWith("LEDG", StringComparison.Ordinal))
            RiseAndShine(state);
        else
            DeclineAndFall(state);
    }

    private static void RiseAndShine(GameState state)
    {
        var world = state.World!;
        var ball = world.FindObject(Obj.Ballo)!;
        var bl = state.BalloonLocation;
        var inIt = InIt(state);
        StartClock(state);
        if (bl.StartsWith("VAIR", StringComparison.Ordinal))
        {
            if (bl == "VAIR4")
            {
                state.ScheduledEvents.RemoveAll(e => e.Name == Clocks.Burnup);
                StopClock(state);
                Remove(ball);
                Put(world.FindObject(Obj.Dball)!, world.FindRoom(RoomId.Vlbot)!);
                state.BalloonLocation = "VLBOT";
                if (inIt)
                    state.JigsUp("Your balloon has hit the rim of the volcano, ripping the cloth and causing you a 500 foot drop.  Did you get your flight insurance?");
                else
                    state.Output.WriteLine("You hear a boom and notice that the balloon is falling to the ground.");
                return;
            }
            var up = "VAIR" + (char)(bl[4] + 1);
            if (inIt) Fly(state, up, "The balloon ascends.");
            else PutBalloon(state, up, "ascends.");
        }
        else if (bl.StartsWith("LEDG", StringComparison.Ordinal))
        {
            var air = "VAIR" + bl[4];
            if (inIt) Fly(state, air, "The balloon leaves the ledge.");
            else
            {
                state.Schedule(10, Clocks.Volgnome);
                PutBalloon(state, air, "floats away.  It seems to be ascending, due to its light load.");
            }
        }
        else if (inIt) Fly(state, "VAIR1", "The balloon rises slowly from the ground.");
        else PutBalloon(state, "VAIR1", "lifts off.");
    }

    private static void DeclineAndFall(GameState state)
    {
        var world = state.World!;
        var bl = state.BalloonLocation;
        var inIt = InIt(state);
        StartClock(state);
        if (!bl.StartsWith("VAIR", StringComparison.Ordinal)) return;
        if (bl == "VAIR1")
        {
            if (!inIt)
            {
                PutBalloon(state, "VLBOT", "lands.");
                return;
            }
            state.BalloonLocation = "VLBOT";
            var bottom = world.FindRoom(RoomId.Vlbot)!;
            Parser.Goto(state, bottom);
            if (state.GetFlag(Flag.Binf))
            {
                state.Output.WriteLine("The balloon has landed.");
                Parser.Describe(state);
            }
            else
            {
                Remove(world.FindObject(Obj.Ballo)!);
                Put(world.FindObject(Obj.Dball)!, bottom);
                state.Winner.Vehicle = null;
                StopClock(state);
                state.Output.WriteLine("You have landed, but the balloon did not survive.");
            }
            return;
        }
        var down = "VAIR" + (char)(bl[4] - 1);
        if (inIt) Fly(state, down, "The balloon descends.");
        else PutBalloon(state, down, "descends.");
    }

    /// <summary>GOTO the new room in the balloon, tell how, and describe it (ROOM-INFO T).</summary>
    private static void Fly(GameState state, string roomId, string message)
    {
        state.BalloonLocation = roomId;
        var room = state.World!.FindRoom(roomId)!;
        var ball = state.World.FindObject(Obj.Ballo)!;
        Remove(ball);
        Put(ball, room);
        Parser.Goto(state, room);
        state.Output.WriteLine(message);
        Parser.Describe(state);
    }

    /// <summary>act2.27 PUT-BALLOON: the empty balloon moves; seen from a ledge, you watch it go.</summary>
    private static void PutBalloon(GameState state, string roomId, string how)
    {
        if (state.Here.Id.StartsWith("LEDG", StringComparison.Ordinal))
            state.Output.WriteLine("You watch as the balloon slowly " + how);
        var ball = state.World!.FindObject(Obj.Ballo)!;
        Remove(ball);
        Put(ball, state.World.FindRoom(roomId)!);
        state.BalloonLocation = roomId;
    }

    private static void Remove(GameObject o) => o.MoveTo(null);

    private static void Put(GameObject o, Room room) => o.MoveTo(room);

    /// <summary>act2.27 WIRE-FUNCTION: TIE the braided wire TO a hook to hold the balloon at a ledge, UNTIE to let go.</summary>
    public static void WireFunction(GameState state, string verb, GameObject? to)
    {
        if (verb == "TIE")
        {
            if (to?.Id is Obj.Hook1 or Obj.Hook2)
            {
                state.SetFlag(Flag.Btie, true);
                StopClock(state);
                state.Output.WriteLine("The balloon is fastened to the hook.");
            }
        }
        else if (verb == "UNTIE")
        {
            if (state.GetFlag(Flag.Btie))
            {
                StartClock(state);
                state.SetFlag(Flag.Btie, false);
                state.Output.WriteLine("The wire falls off of the hook.");
            }
            else
                state.Output.WriteLine("The wire is not tied to anything.");
        }
    }

    /// <summary>act2.27 BURNUP: the fuel has burned away and the bag collapses.</summary>
    public static void Burnup(GameState state)
    {
        var recep = state.World!.FindObject(Obj.Recep)!;
        state.SetFlag(Flag.Binf, false);
        if (recep.Contents.Count == 0) return;
        var obj = recep.Contents[0];
        obj.MoveTo(null);
        state.Output.WriteLine("It seems that the " + obj.DescriptionShort + " has burned out, and the cloth bag starts to collapse.");
    }

    /// <summary>act2.27 LEDGE-FUNCTION (the wide ledge outside the dusty room).</summary>
    public static void LedgeFunction(GameState state, string verb)
    {
        if (verb == "WALK-IN")
        {
            if (state.GetFlag(Flag.Safe))
            {
                state.Output.WriteLine("Behind you, the walls of the safe room collapse into rubble.");
                state.SetFlag(Flag.Safe, false);
            }
        }
        else if (verb == "LOOK")
        {
            state.Output.WriteLine("You are on a wide ledge high into the volcano.  The rim of the volcano is about 200 feet above and there is a precipitous drop below to the bottom.");
            state.Output.WriteLine(state.World?.FindRoom(RoomId.Safe)?.Munged == true
                ? " The way to the south is blocked by rubble."
                : " There is a small door to the south.");
        }
    }

    public static void LedgeEnter(GameState state) => LedgeFunction(state, "WALK-IN");

    /// <summary>act2.27 VOLGNOME: ten moves after the balloon floated off a ledge, a gnome turns up on whichever ledge you're on.</summary>
    public static void Volgnome(GameState state)
    {
        if (!state.Here.Id.StartsWith("LEDG", StringComparison.Ordinal))
        {
            state.Schedule(1, Clocks.Volgnome);
            return;
        }
        state.Output.WriteLine("A volcano gnome seems to walk straight out of the wall and says 'I have a very busy appointment schedule and little time to waste on tresspassers, but for a small fee, I'll show you the way out.'  You notice the gnome nervously glancing at his watch.");
        var gnome = state.World!.FindObject(Obj.Gnome)!;
        Put(gnome, state.Here);
    }

    /// <summary>
    /// act2.27 GNOME-FUNCTION: give him a treasure and he opens a door west; anything else he crunches. Whatever
    /// else you do makes him nervous, and five moves later he's gone (GNOME-FLAG, GNOIN).
    /// </summary>
    public static void GnomeFunction(GameState state, string verb, GameObject? gift)
    {
        if (verb is "GIVE" or "THROW" && gift != null)
        {
            if (gift.TrophyScore != 0)
            {
                state.Output.WriteLine("Thank you very much for the " + gift.DescriptionShort + ".  I don't believe I've ever seen one as beautiful. 'Follow me', he says, and a door appears on the west end of the ledge.  Through the door, you can see a narrow chimney sloping steeply downward.");
                state.SetFlag(Flag.GnomeDoor, true);
            }
            else
            {
                state.Output.WriteLine("'That wasn't quite what I had in mind', he says, crunching the " + gift.DescriptionShort + " in his rock-hard hands.");
                gift.MoveTo(null);
            }
            return;
        }
        state.Output.WriteLine("The gnome appears increasingly nervous.");
        if (!state.GetFlag(Flag.Gnome)) state.Schedule(5, Clocks.GnomeLeave);
        state.SetFlag(Flag.Gnome, true);
    }

    /// <summary>GNOME-FUNCTION on its clock: he leaves.</summary>
    public static void GnomeLeave(GameState state)
    {
        var gnome = state.World!.FindObject(Obj.Gnome)!;
        if (gnome.InRoom == null) return;
        state.Output.WriteLine("The gnome glances at his watch.  'Oops.  I'm late for an appointment!' He disappears, leaving you alone on the ledge.");
        Remove(gnome);
    }
}
