namespace ZorkDotNet.Game;

/// <summary>
/// Combat and demon support (np.92/np.93, dung.56): ARMED?, LIGHT-SOURCE, ROB-ROOM,
/// SWORD-DEMON (sword glow), FIGHT-DEMON / TROLL-MELEE (ongoing melee, strength, STAGGERED).
/// </summary>
public static class Combat
{
    /// <summary>ARMED?: true if player has a weapon in inventory.</summary>
    public static bool Armed(GameState state)
    {
        return state.Winner.Inventory.Exists(o => (o.Flags & ObjectFlags.Weapon) != 0);
    }

    /// <summary>ROB-ROOM: pick valuables from room with probability (thief demon). Returns list to steal.</summary>
    public static List<GameObject> RobRoom(GameState state, Room room, int probabilityPercent)
    {
        var list = new List<GameObject>();
        if (state.World == null) return list;
        var lit = GameState.IsLit(state, room);
        var candidates = room.Objects.Where(o =>
            o.IsVisible && o.CanTake && o.FindScore > 0 &&
            (o.Flags & ObjectFlags.Sacred) == 0).ToList();
        foreach (var o in candidates)
        {
            if (Random.Shared.Next(100) < probabilityPercent)
                list.Add(o);
        }
        return list;
    }

    /// <summary>act1.38 INFESTED?: a villain is in this room.</summary>
    public static bool Infested(Room room) => room.Objects.Any(o => o.IsVillain);

    /// <summary>
    /// act1.38 SWORD-GLOW: 2 if a villain is here, 1 if one is in a room an exit leads to (plain or conditional, whether
    /// or not it's open; blocked exits don't count), else 0.
    /// </summary>
    public static int SwordGlow(GameState state, Room here)
    {
        if (Infested(here)) return 2;
        if (state.World == null) return 0;
        foreach (var target in here.Exits.Values)
        {
            var adj = target.RoomId != null ? state.World.FindRoom(target.RoomId) : null;
            if (adj != null && Infested(adj)) return 1;
        }
        return 0;
    }

    /// <summary>SWORD-DEMON: when player has sword, update glow from INFESTED? and print message.</summary>
    public static void SwordDemon(GameState state)
    {
        if (state.World == null) return;
        var sword = state.World.FindObject("SWORD");
        if (sword == null || !state.Winner.Inventory.Contains(sword)) return;
        var here = state.Here;
        var ng = SwordGlow(state, here);
        var g = state.ObjectState.TryGetValue(sword.Id, out var v) ? v : 0;
        if (ng == g) return;
        state.ObjectState[sword.Id] = ng;
        if (ng == 2) state.Output.WriteLine("Your sword has begun to glow very brightly.");
        else if (ng == 1) state.Output.WriteLine("Your sword is glowing with a faint blue glow.");
        else state.Output.WriteLine("Your sword is no longer glowing.");
    }

    /// <summary>FIGHT-DEMON / TROLL-MELEE: one round if troll in room has Fighting.</summary>
    public static void FightDemon(GameState state)
    {
        if (state.World == null || state.Here == null) return;
        var troll = state.World.FindObject("TROLL");
        if (troll == null || !troll.Fighting || troll.InRoom != state.Here) return;
        if (!state.VillainStrength.TryGetValue(troll.Id, out var trollStr)) return;

        // Staggered: skip attack this turn
        if (state.Winner.Staggered)
        {
            state.Winner.Staggered = false;
            return;
        }

        // Player hits troll (with weapon if armed)
        var damageToTroll = Armed(state) ? 1 : 0;
        if (damageToTroll > 0) trollStr -= damageToTroll;
        state.VillainStrength[troll.Id] = trollStr;

        if (trollStr <= 0)
        {
            state.VillainStrength.Remove(troll.Id);
            troll.Flags &= ~ObjectFlags.Fighting;
            state.Here.Objects.Remove(troll);
            troll.InRoom = null;
            state.SetFlag("TROLL-FLAG!-FLAG", true);
            state.Output.WriteLine("The troll is killed by the force of your attack.  The body has vanished.");
            return;
        }

        // Troll hits player (strength damage)
        var damageToPlayer = 20;
        state.Winner.Strength = Math.Max(0, state.Winner.Strength - damageToPlayer);
        if (Random.Shared.Next(100) < 30)
            state.Winner.Staggered = true;
        if (state.Winner.Staggered)
            state.Output.WriteLine("The troll's blow staggers you.");
        else
            state.Output.WriteLine("The troll hits you with a glancing blow.");
        if (state.Winner.Strength <= 0)
        {
            state.Output.WriteLine("You have been killed.");
            state.Running = false;
        }
    }
}
