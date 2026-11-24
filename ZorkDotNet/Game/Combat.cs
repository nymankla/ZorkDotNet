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
        return state.Winner.Inventory.Any(o => (o.Flags & ObjectFlags.Weapon) != 0);
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

    /// <summary>How many blows the troll takes (the port's stand-in for the melee's strengths).</summary>
    public const int TrollStrength = 2;
    /// <summary>How many blows the thief takes.</summary>
    public const int ThiefStrength = 3;

    /// <summary>
    /// FIGHT-DEMON: each move, every villain here who is fighting you strikes. The original's melee (BLOW, the hero and
    /// villain blow tables, CURE-CLOCK) is not in the MIT archive, so this is the port's own simple melee.
    /// </summary>
    public static void FightDemon(GameState state)
    {
        if (state.World == null) return;
        foreach (var villain in state.Here.Objects.Where(o => o.IsVillain && o.Fighting).ToList())
        {
            if (!state.VillainStrength.ContainsKey(villain.Id)) continue;
            if (state.Winner.Staggered)
            {
                state.Winner.Staggered = false;
                continue;
            }
            state.Winner.Strength = Math.Max(0, state.Winner.Strength - GameState.WoundStrength);
            if (state.Random.Next(100) < 30) state.Winner.Staggered = true;
            state.Output.WriteLine(state.Winner.Staggered
                ? "The " + villain.DescriptionShort + "'s blow staggers you."
                : "The " + villain.DescriptionShort + " hits you with a glancing blow.");
            if (state.Winner.Strength <= 0)
            {
                state.Output.WriteLine("You have been killed.");
                state.JigsUp(null);
                return;
            }
        }
    }

    /// <summary>You strike a villain (ATTACK villain WITH weapon); the first blow starts the fight.</summary>
    public static void Attack(GameState state, GameObject villain, GameObject? weapon)
    {
        if (weapon == null || weapon.Carrier != state.Winner)
        {
            state.Output.WriteLine("You need a weapon to attack the " + villain.DescriptionShort + ".");
            return;
        }
        if ((weapon.Flags & ObjectFlags.Weapon) == 0)
        {
            state.Output.WriteLine("You can't attack with that.");
            return;
        }
        if (!villain.Fighting)
        {
            villain.Flags |= ObjectFlags.Fighting;
            state.VillainStrength[villain.Id] = villain.Id == "THIEF" ? ThiefStrength : TrollStrength;
            state.Output.WriteLine(villain.Id == "TROLL"
                ? "The troll blocks your blow with his axe, and you are thrown back."
                : "The " + villain.DescriptionShort + " parries your blow.");
            return;
        }
        var strength = state.VillainStrength.TryGetValue(villain.Id, out var s) ? s - 1 : 0;
        if (strength > 0)
        {
            state.VillainStrength[villain.Id] = strength;
            state.Output.WriteLine("You hit the " + villain.DescriptionShort + ".");
            return;
        }
        state.VillainStrength.Remove(villain.Id);
        villain.Flags &= ~ObjectFlags.Fighting;
        villain.MoveTo(null);
        state.Output.WriteLine("The " + villain.DescriptionShort + " is killed by the force of your attack.  The body has vanished.");
        if (villain.Id == "TROLL") state.SetFlag("TROLL-FLAG!-FLAG", true); // TROLL with DEAD!
        else if (villain.Id == "THIEF") Thief.Died(state); // ROBBER-FUNCTION with DEAD!
    }
}
