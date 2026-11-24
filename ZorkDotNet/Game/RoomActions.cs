namespace ZorkDotNet.Game;

/// <summary>
/// Room-action handlers ported from act1.37, act1.38 (e.g. EAST-HOUSE, KITCHEN, LIVING-ROOM, BOOM-ROOM, BATS-ROOM, GLACIER-ROOM).
/// </summary>
public static class RoomActions
{
    public static void EastHouse(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        var windowOpen = state.GetFlag("KITCHEN-WINDOW!-FLAG");
        state.Output.WriteLine(
            "You are behind the white house.  In one corner of the house there is a small window which is " +
            (windowOpen ? "open." : "slightly ajar."));
    }

    public static void Kitchen(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        state.Output.WriteLine(
            "You are in the kitchen of the white house.  A table seems to have been used recently for the preparation of food.  A passage leads to the west and a dark staircase can be seen leading upward.  To the east is a small window which is " +
            (state.GetFlag("KITCHEN-WINDOW!-FLAG") ? "open." : "slightly ajar."));
    }

    public static void LivingRoom(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        var magic = state.GetFlag("MAGIC-FLAG!-FLAG");
        var trap = state.GetFlag("TRAP-DOOR!-FLAG");
        var rugMoved = state.GetFlag("RUG-MOVED!-FLAG");
        state.Output.Write(
            magic
                ? "You are in the living room.  There is a door to the east.  To the west is a cyclops-shaped hole in an old wooden door, above which is some strange gothic lettering "
                : "You are in the living room.  There is a door to the east, a wooden door with strange gothic lettering to the west, which appears to be nailed shut, ");
        if (rugMoved && trap)
            state.Output.WriteLine("and a rug lying beside an open trap-door.");
        else if (rugMoved)
            state.Output.WriteLine("and a closed trap-door at your feet.");
        else if (trap)
            state.Output.WriteLine("and an open trap-door at your feet.");
        else
            state.Output.WriteLine("and a large oriental rug in the center of the room.");
    }

    public static void BoomRoom(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        state.Output.WriteLine("You are in a small room which smells strongly of coal gas.");
    }

    /// <summary>OVER-FALLS (act2.27, FCHMP room action): going over Aragain Falls is fatal (JIGS-UP).</summary>
    /// <summary>act2.27 OVER-FALLS on LOOK: prints nothing (the room has no description); the death comes on WALK-IN.</summary>
    public static void OverFallsLook(GameState _1, string _2) { }

    public static void OverFalls(GameState state)
    {
        state.Output.WriteLine("Oh dear, you seem to have gone over Aragain Falls.  Not a very smart thing to do, apparently.");
        state.JigsUp(null);
    }

    /// <summary>On entering BOOM room: if carrying lit candle or lit torch, JIGS-UP.</summary>
    public static void OnEnterBoom(GameState state)
    {
        var torch = state.World?.FindObject("TORCH");
        var candl = state.World?.FindObject("CANDL");
        var lit = (torch != null && state.Winner.Inventory.Contains(torch) && torch.LightAmount > 0) ||
                  (candl != null && state.Winner.Inventory.Contains(candl) && candl.LightAmount > 0);
        if (!lit) return;
        var o = torch != null && state.Winner.Inventory.Contains(torch) ? torch : candl;
        state.Output.WriteLine("Oh dear.  It appears that the smell coming from this room was coal gas.  I would have thought twice about carrying a " + (o?.DescriptionShort ?? "light") + " in here.");
        state.Output.WriteLine("   BOOOOOOOOOOOM      ");
        state.JigsUp(null);
    }

    /// <summary>On entering BATS room without garlic: bat flies player to random room.</summary>
    public static void OnEnterBats(GameState state)
    {
        if (!state.Winner.Inventory.Any(o => o.Id == "GARLI")) FlyMe(state);
    }

    /// <summary>act1.38 WELL: answering the riddle in the Riddle Room opens its east door.</summary>
    public static void Well(GameState state)
    {
        if (!state.GetFlag("RIDDLE-FLAG!-FLAG") && state.Here?.Id == "RIDDL")
        {
            state.SetFlag("RIDDLE-FLAG!-FLAG", true);
            state.Output.WriteLine("There is a clap of thunder and the east door opens.");
        }
        else
            state.Output.WriteLine("Well what?");
    }

    /// <summary>act1.38 CELLAR on LOOK: the room's description (CELLA has no RDESC1 of its own).</summary>
    public static void Cellar(GameState state, string verb)
    {
        if (verb == "LOOK")
            state.Output.WriteLine("You are in a dark and damp cellar with a narrow passageway leading east, and a crawlway to the south.  On the west is the bottom of a steep metal ramp which is unclimbable.");
    }

    /// <summary>act1.38 CELLAR on WALK-IN: the first time you come down an open trap door, it slams shut.</summary>
    public static void OnEnterCellar(GameState state)
    {
        if (!state.GetFlag("TRAP-DOOR!-FLAG")) return;
        var door = state.World?.FindObject("DOOR");
        if (door != null && door.Touched) return;
        state.SetFlag("TRAP-DOOR!-FLAG", false);
        if (door != null) door.Touched = true;
        state.Output.WriteLine("The trap door crashes shut, and you hear someone barring it.");
    }

    public static void BatsRoom(GameState state, string verb)
    {
        if (verb == "LOOK")
        {
            state.Output.WriteLine("You are in a small room which has only one door, to the east.");
            var hasGarlic = state.Winner.Inventory.Any(o => o.Id == "GARLI");
            if (hasGarlic)
                state.Output.WriteLine("In the corner of the room on the ceiling is a large vampire bat who is obviously deranged and holding his nose.");
        }
    }

    public static void GlacierRoom(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        var flag = state.GetFlag("GLACIER-FLAG!-FLAG");
        state.Output.WriteLine("You are in a large room, with giant icicles hanging from the walls and ceiling.  There are passages to the north and east." +
            (flag ? " There is a large passageway leading westward." : ""));
    }

    public static void MachineRoom(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        state.Output.WriteLine("You are in a large room which seems to be air-conditioned.  In one corner there is a machine (?) which is shaped somewhat like a clothes dryer.  On the 'panel' there is a switch which is labelled in a dialect of Swahili.  Fortunately, I know this dialect and the label translates to START.  The switch does not appear to be manipulable by any human hand (unless the fingers are about 1/16 by 1/4 inch).  On the front of the machine is a large lid.");
        var mach = state.World?.FindObject("MACHI");
        if (mach != null)
            state.Output.WriteLine(mach.Open ? "The lid on the machine is open." : "The lid on the machine is closed.");
    }

    /// <summary>CLEARING: LOOK with grating; leaves can be burned/moved to reveal grating.</summary>
    public static void Clearing(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        state.Output.WriteLine("You are in a clearing, with a forest surrounding you on the west and south.");
        if (state.GetFlag("KEY-FLAG!-FLAG"))
            state.Output.WriteLine("There is an open grating, descending into darkness.");
        else if (state.Here.RVars != 0)
            state.Output.WriteLine("There is a grating securely fastened into the ground.");
    }

    /// <summary>DAM: LOOK text (Flood Control Dam #3).</summary>
    public static void DamRoom(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        state.Output.WriteLine("You are standing on the top of the Flood Control Dam #3, which was quite a tourist attraction in times far distant.  There are paths to the north, south, east, and down.");
        var lowTide = state.GetFlag("LOW-TIDE!-FLAG");
        if (lowTide)
            state.Output.WriteLine("It appears that the dam has been opened since the water level behind it is low and the sluice gate has been opened.  Water is rushing downstream through the gates.");
        else
            state.Output.WriteLine("The sluice gates on the dam are closed.  Behind the dam, there can be seen a wide lake.  A small stream is formed by the runoff from the lake.");
        state.Output.WriteLine("There is a control panel here.  There is a large metal bolt on the panel. Above the bolt is a small green plastic bubble.");
        if (state.GetFlag("GATE-FLAG!-FLAG"))
            state.Output.WriteLine("The green bubble is glowing.");
    }

    // act1.38 RESDESC
    private const string ResDesc = "However, with the water level lowered, there is merely a wide stream running through the center of the room.";

    /// <summary>act1.38 RESERVOIR-SOUTH</summary>
    public static void ReservoirSouth(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        if (state.GetFlag("LOW-TIDE!-FLAG"))
        {
            state.Output.WriteLine("You are in the south end of a large cavernous room which was formerly a reservoir.");
            state.Output.WriteLine(ResDesc);
        }
        else
            state.Output.WriteLine("You are at the south end of a large reservoir.");
        state.Output.WriteLine("There is a western exit, a passageway south, and a steep pathway climbing up along the edge of a cliff.");
    }

    /// <summary>act1.38 RESERVOIR-NORTH</summary>
    public static void ReservoirNorth(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        if (state.GetFlag("LOW-TIDE!-FLAG"))
        {
            state.Output.WriteLine("You are in the north end of a large cavernous room which was formerly a reservoir.");
            state.Output.WriteLine(ResDesc);
        }
        else
            state.Output.WriteLine("You are at the north end of a large reservoir.");
        state.Output.WriteLine("There is a tunnel leaving the room to the north.");
    }

    // act1.38 DROWNINGS
    private static readonly string[] Drownings =
    [
        "up to your ankles.", "up to your shin.", "up to your knees.", "up to your hips.", "up to your waist.",
        "up to your chest.", "up to your neck.", "over your head.", "high in your lungs.",
    ];

    /// <summary>
    /// act1.38 MAINT-ROOM for PUSH: the four buttons. The room's RVARS is the water level: 0 before the leak, rising
    /// while it floods, -1 once the leak is plugged.
    /// </summary>
    public static void MaintRoom(GameState state, string verb)
    {
        if (verb != "PUSH") return;
        var here = state.Here;
        switch (state.LastDirectObject?.Id)
        {
            case "BLBUT":
                if (here.RVars == 0)
                {
                    state.Output.WriteLine("There is a rumbling sound and a stream of water appears to burst from the east wall of the room (apparently, a leak has occurred in a pipe.)");
                    here.RVars = 1;
                    state.Schedule(0, Clocks.MaintRoom); // CLOCK-INT MNTIN -1: every move, starting with this one
                }
                else
                    state.Output.WriteLine("The blue button appears to be jammed.");
                break;
            case "RBUTT":
                here.HasLight = !here.HasLight;
                state.Output.WriteLine(here.HasLight ? "The lights within the room come on." : "The lights within the room shut off.");
                break;
            case "BRBUT":
                state.SetFlag("GATE-FLAG!-FLAG", false);
                state.Output.WriteLine("Click.");
                break;
            case "YBUTT":
                state.SetFlag("GATE-FLAG!-FLAG", true);
                state.Output.WriteLine("Click.");
                break;
        }
    }

    /// <summary>act1.38 MAINT-ROOM clock (C-INT): the water rises; at 16 the room is flooded for good.</summary>
    public static void MaintRoomRising(GameState state)
    {
        var maint = state.World?.FindRoom("MAINT");
        if (maint == null || maint.RVars <= 0) return;
        maint.RVars++;
        var here = state.Here == maint;
        if (here) state.Output.WriteLine("The water level here is now " + Drownings[Math.Min(maint.RVars / 2, Drownings.Length - 1)]);
        if (maint.RVars >= 16)
        {
            maint.Munged = true;
            maint.MungedMessage = "The room is full of water and cannot be entered.";
            if (here) state.JigsUp("I'm afraid you have done drowned yourself.");
            return;
        }
        state.Schedule(1, Clocks.MaintRoom);
    }

    // act2.27 BDIGS / CDIGS
    private static readonly string[] BeachDigs =
        ["You seem to be digging a hole here.", "The hole is getting deeper, but that's about it.", "You are surrounded by a wall of sand on all sides."];
    private static readonly string[] CaveDigs =
        ["You are digging into a pile of bat guano.", "You seem to be getting knee deep in guano.", "You are covered with bat turds, cretin."];

    /// <summary>The shovel was the thing dug with (DIG WITH SHOVEL makes it the direct object).</summary>
    private static bool DugWithShovel(GameState state, string verb) => verb == "DIG" && state.LastDirectObject?.Id == "SHOVE";

    /// <summary>act2.27 BEACH-ROOM: the fourth spadeful uncovers the statue; the fifth buries you.</summary>
    public static void BeachRoom(GameState state, string verb)
    {
        if (!DugWithShovel(state, verb)) return;
        var here = state.Here;
        var count = ++here.RVars;
        if (count > 4)
        {
            here.RVars = 0;
            state.JigsUp("The hole collapses, smothering you.");
        }
        else if (count == 4)
        {
            state.Output.WriteLine("You can see a small statue here in the sand.");
            var statue = state.World?.FindObject("STATU");
            if (statue != null) statue.Flags |= ObjectFlags.Visible;
        }
        else if (count > 0)
            state.Output.WriteLine(BeachDigs[count - 1]);
    }

    /// <summary>act2.27 TCAVE-ROOM: digging in the guano gets you nowhere.</summary>
    public static void TcaveRoom(GameState state, string verb)
    {
        if (!DugWithShovel(state, verb)) return;
        var here = state.Here;
        if (!here.Objects.Exists(o => o.Id == "GUANO"))
        {
            state.Output.WriteLine("There's nothing to dig into here.");
            return;
        }
        var count = ++here.RVars;
        state.Output.WriteLine(count > 3 ? "This is getting you nowhere." : CaveDigs[count - 1]);
    }

    /// <summary>
    /// act2.27 CLIFF-FUNCTION: the narrow path along the White Cliffs is passable only without an inflated boat
    /// (DEFLATE). It runs after every command here, and (through a before-exit hook) when you set off.
    /// </summary>
    public static void CliffFunction(GameState state, string _) => CliffPath(state);

    public static void CliffPath(GameState state) =>
        state.SetFlag("DEFLATE!-FLAG", !state.Winner.Inventory.Exists(o => o.Id == "RBOAT"));

    /// <summary>act2.27 RIVR4-ROOM: carrying the buoy here, you notice it is heavier than it should be (the emerald).</summary>
    public static void Rivr4Room(GameState state, string _)
    {
        if (state.Winner.Inventory.Exists(o => o.Id == "BUOY") && state.GetFlag("BUOY-FLAG!-FLAG"))
        {
            state.Output.WriteLine("Something seems funny about the feel of the buoy.");
            state.SetFlag("BUOY-FLAG!-FLAG", false);
        }
    }

    public static void Rivr4Enter(GameState state) => Rivr4Room(state, "WALK-IN");

    /// <summary>
    /// act1.38 ECHO-ROOM: until ECHO-FLAG, the Loud Room hides its objects and echoes every line you type
    /// (Parser.EchoTurn) until you walk out or say ECHO.
    /// </summary>
    public static void EchoRoom(GameState state, string _)
    {
        if (state.GetFlag("ECHO-FLAG!-FLAG") || state.EchoListening) return;
        foreach (var o in state.Here.Objects.Where(o => o.IsVisible))
        {
            o.Flags &= ~ObjectFlags.Visible;
            state.EchoHidden.Add(o.Id);
        }
        state.EchoListening = true;
    }

    public static void EchoEnter(GameState state) => EchoRoom(state, "WALK-IN");

    /// <summary>The objects ECHO-ROOM hid (ECHO-ROOM-BIT) come back.</summary>
    public static void EchoRestore(GameState state)
    {
        foreach (var id in state.EchoHidden)
        {
            var o = state.World?.FindObject(id);
            if (o != null) o.Flags |= ObjectFlags.Visible;
        }
        state.EchoHidden.Clear();
    }

    /// <summary>act1.38 DOME-ROOM (its JUMP branch can't be reached: LEAPER never succeeds here).</summary>
    public static void DomeRoom(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        state.Output.WriteLine("You are at the periphery of a large dome, which forms the ceiling of another room below.  Protecting you from a precipitous drop is a wooden railing which circles the dome.");
        if (state.GetFlag("DOME-FLAG!-FLAG"))
            state.Output.WriteLine("Hanging down from the railing is a rope which ends about ten feet from the floor below.");
    }

    /// <summary>act1.38 TORCH-ROOM</summary>
    public static void TorchRoom(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        state.Output.WriteLine("You are in a large room with a prominent doorway leading to a down staircase. To the west is a narrow twisting tunnel.  Above you is a large dome painted with scenes depicting elfin hacking rites. Up around the edge of the dome (20 feet up) is a wooden railing. In the center of the room there is a white marble pedestal.");
        if (state.GetFlag("DOME-FLAG!-FLAG"))
            state.Output.WriteLine("A large piece of rope descends from the railing above, ending some five feet above your head.");
    }

    /// <summary>act1.38 MAZE-11 (the grating room)</summary>
    public static void Maze11(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        state.Output.WriteLine("You are in a small room near the maze. There are twisty passages in the immediate vicinity.");
        if (state.GetFlag("KEY-FLAG!-FLAG"))
            state.Output.WriteLine("Above you is an open grating with sunlight pouring in.");
        else if (state.GetFlag("GRUNLOCK!-FLAG"))
            state.Output.WriteLine("Above you is a grating.");
        else
            state.Output.WriteLine("Above you is a grating locked with a skull-and-crossbones lock.");
    }

    /// <summary>The strange passage's room action is MDL TIME, which does nothing.</summary>
    public static void Time(GameState _1, string _2) { }

    /// <summary>
    /// act2.27 NO-OBJS (timber room and shaft bottom): only the empty-handed fit through (EMPTY-HANDED), and lighting
    /// the bottom of the shaft is worth LIGHT-SHAFT points, once.
    /// </summary>
    public static void NoObjs(GameState state, string _)
    {
        EmptyHanded(state);
        if (state.Here.Id == "BSHAF" && !state.LightShaftScored && GameState.IsLit(state))
        {
            state.Winner.Score += Scoring.LightShaftPoints;
            state.LightShaftScored = true;
        }
    }

    public static void NoObjsEnter(GameState state) => NoObjs(state, "WALK-IN");

    public static void EmptyHanded(GameState state) =>
        state.SetFlag("EMPTY-HANDED!-FLAG", state.Winner.Inventory.Count == 0);

    /// <summary>act2.27 FLY-ME: the bat carries you to a random room of BAT-DROPS, which is then described.</summary>
    public static void FlyMe(GameState state)
    {
        var batDrops = new[] { "MINE1", "MINE2", "MINE3", "MINE4", "MINE5", "MINE6", "MINE7", "TLADD", "BLADD" };
        var pick = batDrops[state.Random.Next(batDrops.Length)];
        state.Output.WriteLine("A deranged giant vampire bat (a reject from WUMPUS) swoops down from his belfry and lifts you away....");
        var next = state.World?.FindRoom(pick);
        if (next != null) Parser.MoveToRoom(state, next);
    }

    /// <summary>act1.38 CAVE2-ROOM: walking in, PROB 50 that the wind blows the candles you carry out.</summary>
    public static void Cave2Room(GameState state, string verb)
    {
        if (verb != "WALK-IN") return;
        var candles = state.Winner.Inventory.FirstOrDefault(o => o.Id == "CANDL");
        if (candles != null && candles.LightAmount > 0 && state.Random.Next(100) < 50)
        {
            candles.LightAmount = -1;
            state.Output.WriteLine("The cave is very windy at the moment and your candles have blown out.");
        }
    }

    public static void Cave2Enter(GameState state) => Cave2Room(state, "WALK-IN");

    /// <summary>
    /// act1.38 LLD-ROOM (the entrance to Hades): its description, and EXORCISE. With the bell, the book and lit
    /// candles the spirits flee; without the spirits here, you are the one dismissed.
    /// </summary>
    public static void LldRoom(GameState state, string verb)
    {
        if (verb == "LOOK")
        {
            state.Output.WriteLine("You are outside a large gateway, on which is inscribed \"Abandon every hope, all ye who enter here.\"  The gate is open; through it you can see a desolation, with a pile of mangled corpses in one corner.  Thousands of voices, lamenting some hideous fate, can be heard.");
            if (!state.GetFlag("LLD-FLAG!-FLAG"))
                state.Output.WriteLine("The way through the gate is barred by evil spirits, who jeer at your attempts to pass.");
        }
        else if (verb == "EXORCISE")
        {
            var ghost = state.World?.FindObject("GHOST");
            if (ghost != null && ghost.InRoom == state.Here)
            {
                var inv = state.Winner.Inventory;
                var candles = inv.FirstOrDefault(o => o.Id == "CANDL");
                if (inv.Exists(o => o.Id == "BELL") && inv.Exists(o => o.Id == "BOOK") && candles != null && candles.LightAmount > 0)
                {
                    state.Output.WriteLine("There is a clap of thunder, and a voice echoes through the cavern: \"Begone, fiends!\"  The spirits, sensing the presence of a greater power, flee through the walls.");
                    state.Here.Objects.Remove(ghost);
                    ghost.InRoom = null;
                    state.SetFlag("LLD-FLAG!-FLAG", true);
                }
                else
                    state.Output.WriteLine("You are not equipped for an exorcism.");
            }
            else
                state.JigsUp("There is a clap of thunder, and a voice echoes through the cavern: \"Begone, chomper!\"  Apparently, the voice thinks you are an evil spirit, and dismisses you from the realm of the living.");
        }
    }

    /// <summary>CAROUSEL (Round room): LOOK text; compass message when CAROUSEL-FLIP is false.</summary>
    /// <summary>act1.38 CAROUSEL-ROOM on WALK-IN: with the carousel at full speed (CAROUSEL-ZOOM), it kills you.</summary>
    public static void CarouselEnter(GameState state)
    {
        if (state.GetFlag("CAROUSEL-ZOOM!-FLAG")) state.JigsUp(Robot.SpinDizzy);
    }

    public static void CarouselRoom(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        state.Output.WriteLine("You are in a circular room with passages off in eight directions.");
        if (!state.GetFlag("CAROUSEL-FLIP!-FLAG"))
            state.Output.WriteLine("Your compass needle spins wildly, and you can't get your bearings.");
    }

    /// <summary>SAFE (Dusty Room): LOOK text.</summary>
    public static void SafeRoom(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        state.Output.WriteLine("You are in a dusty old room which is virtually featureless, except for an exit on the north side.");
        state.Output.WriteLine(state.GetFlag("SAFE-FLAG!-FLAG")
            ? "On the far wall is a rusty box, whose door has been blown off."
            : "Imbedded in the far wall, there is a rusty old box.  It appears that the box is somewhat damaged, since an oblong hole has been chipped out of the front of it.");
    }

    /// <summary>Mirror room (MIRR1/MIRR2): LOOK text; MIRROR-MUNG message when broken.</summary>
    public static void MirrorRoom(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        state.Output.WriteLine("You are in a large square room with tall ceilings.  On the south wall is an enormous mirror which fills the entire wall.  There are exits on the other three sides of the room.");
        if (state.GetFlag("MIRROR-MUNG!-FLAG"))
            state.Output.WriteLine("Unfortunately, you have managed to destroy it by your reckless actions.");
    }

    /// <summary>Cyclops room (CYCLO): LOOK text with cyclops state (RVARS, CYCLOPS-FLAG, MAGIC-FLAG).</summary>
    public static void CyclopsRoom(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        state.Output.WriteLine("You are in a room with an exit on the west side, and a staircase leading up.");
        var here = state.Here;
        var cyclops = state.World?.FindObject("CYCLO");
        var inRoom = cyclops != null && here != null && here.Objects.Contains(cyclops);
        if (state.GetFlag("CYCLOPS-FLAG!-FLAG") && !state.GetFlag("MAGIC-FLAG!-FLAG"))
            state.Output.WriteLine("The cyclops, perhaps affected by a drug in your drink, is sleeping blissfully at the foot of the stairs.");
        else if (state.GetFlag("MAGIC-FLAG!-FLAG"))
            state.Output.WriteLine("On the north of the room is a wall which used to be solid, but which now has a cyclops-sized hole in it.");
        else if (inRoom)
        {
            var v = here!.RVars;
            if (v == 0)
                state.Output.WriteLine("A cyclops, who looks prepared to eat horses (much less mere adventurers), blocks the staircase.  From his state of health, and the bloodstains on the walls, you gather that he is not very friendly, though he likes people.");
            else if (v > 0)
                state.Output.WriteLine("The cyclops is standing in the corner, eyeing you closely.  I don't think he likes you very much.  He looks extremely hungry even for a cyclops.");
            else
                state.Output.WriteLine("The cyclops, having eaten the hot peppers, appears to be gasping. His enflamed tongue protrudes from his man-sized mouth.");
        }
    }

    /// <summary>FALLS (Aragain Falls): LOOK text; RAINBOW flag shows solid rainbow.</summary>
    public static void FallsRoom(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        state.Output.WriteLine("You are at the top of Aragain Falls, an enormous waterfall with a drop of about 450 feet.  The only path here is on the north end.");
        state.Output.WriteLine(state.GetFlag("RAINBOW!-FLAG")
            ? "A solid rainbow spans the falls."
            : "A beautiful rainbow can be seen over the falls and to the east.");
    }

    /// <summary>LLD2 (Land of the Living Dead): LOOK text.</summary>
    public static void Lld2Room(GameState state, string verb)
    {
        if (verb != "LOOK") return;
        state.Output.WriteLine("You have entered the Land of the Living Dead, a large desolate room. Although it is apparently uninhabited, you can hear the sounds of thousands of lost souls weeping and moaning.  In the east corner are stacked the remains of dozens of previous adventurers who were less fortunate than yourself.  To the east is an ornate passage, apparently recently constructed.");
        if (state.GetFlag("ON-POLE!-FLAG"))
            state.Output.WriteLine("Amid the desolation, you spot what appears to be your head, at the end of a long pole.");
    }
}
