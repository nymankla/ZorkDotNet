namespace ZorkDotNet.Game;

/// <summary>
/// rooms.99 JIGS-UP: dying. The first two times you are asked whether to be patched up; yes puts you in the forest
/// with your possessions scattered over the dungeon, no (or a third death) ends the game.
/// </summary>
public static class Death
{
    /// <summary>dung.56 RANDOM-LIST: where your ordinary possessions land after a death, in order.</summary>
    private static readonly string[] RandomList = ["LROOM", "KITCH", "CLEAR", "FORE3", "FORE2", "SHOUS", "FORE2", "KITCH", "EHOUS"];

    /// <summary>
    /// The head of the original's ROOMS list, where possessions left over after RANDOM-LIST (treasures first) land.
    /// MDL builds ROOMS by adding each room at the front when it is first mentioned, so this is the reverse of the
    /// order rooms first appear in dung.56. Only the head matters: you never carry more than this.
    /// </summary>
    private static readonly string[] RoomsHead =
    [
        "ALITR", "ALISM", "TWELL", "CAGED", "CAGER", "MAGNE", "ALICE", "CMACH", "SAFE", "LIBRA",
        "LEDG4", "VAIR4", "VAIR3", "LEDG2", "VAIR2", "VAIR1", "VLBOT", "CLMID", "CLBOT", "POG",
    ];

    /// <summary>JIGS-UP: tell how you died, lose 10 points, and either ask about patching you up or end the game.</summary>
    public static void JigsUp(GameState state, string? message)
    {
        if (message != null) state.Output.WriteLine(message);
        state.Winner.Score -= 10;
        state.Winner.Vehicle = null;
        if (state.Progress.Deaths >= 2)
        {
            state.Output.WriteLine("You clearly are a suicidal maniac.  We don't allow psychotics in the cave, since they may harm other adventurers.  Your remains will installed in the Land of the Living Dead, where your fellow adventurers may gloat over them.");
            Scoring.Finish(state);
            return;
        }
        state.Progress.Deaths++;
        state.Output.WriteLine("Do you want me to try to patch you?");
        state.AwaitingPatchAnswer = true;
    }

    /// <summary>
    /// The answer to "Do you want me to try to patch you?" (defs.63 YES/NO with NO-IS-BAD: anything not starting
    /// with N or F is yes). Returns true when you were patched up.
    /// </summary>
    public static bool AnswerPatch(GameState state, string answer)
    {
        state.AwaitingPatchAnswer = false;
        if (answer.Length > 0 && "NnFf".Contains(answer[0], StringComparison.Ordinal))
        {
            state.Output.WriteLine("What?  You don't trust me?  Why, only last week I patched a running ITS and it survived for over 30 seconds.  Oh, well.");
            Scoring.Finish(state);
            return false;
        }
        state.Output.WriteLine("Now, let me see...");
        state.Output.WriteLine("Well, we weren't quite able to restore your state.  You can't have everything.");
        Resurrect(state);
        return true;
    }

    private static void Resurrect(GameState state)
    {
        var world = state.World!;
        var inventory = state.Winner.Inventory;

        // The lamp comes back to you wherever it was, at the head of your possessions.
        var lamp = world.FindObject(Obj.Lamp);
        if (lamp != null)
        {
            if (lamp.InRoom != null || lamp.Container != null || lamp.Carrier == state.Winner)
                lamp.MoveTo(state.Winner, first: true);
        }

        var door = world.FindObject(Obj.Door);
        if (door != null) door.Touched = false;
        var forest = world.FindRoom(RoomId.Fore1);
        if (forest != null) state.Winner.CurrentRoom = forest; // GOTO: no description
        state.SetFlag(Flag.Egypt, true);

        // defs.63 ROB-ADV: treasures (OTVAL > 0, not sacred) are taken out first.
        var valuables = inventory.Where(o => o.TrophyScore > 0 && (o.Flags & ObjectFlags.Sacred) == 0).ToList();
        // The rest go to RANDOM-LIST, one per room, in order.
        var rest = inventory.Except(valuables).ToList();
        var placed = Math.Min(rest.Count, RandomList.Length);
        for (var i = 0; i < placed; i++) Drop(state, rest[i], RandomList[i]);
        // Treasures, then whatever didn't fit, go to the rooms at the head of ROOMS.
        var leftovers = valuables.Concat(rest.Skip(placed)).ToList();
        for (var i = 0; i < leftovers.Count && i < RoomsHead.Length; i++) Drop(state, leftovers[i], RoomsHead[i]);
        foreach (var lost in inventory.ToList()) lost.MoveTo(null);
    }

    private static void Drop(GameState state, GameObject obj, string roomId)
    {
        var room = state.World!.FindRoom(roomId);
        if (room == null) return;
        obj.MoveTo(room);
    }
}
