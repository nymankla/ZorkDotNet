namespace ZorkDotNet.Game;

/// <summary>
/// Named clock events (MDL CEVENTs, dung.56). A <see cref="ScheduledEvent"/> stores one of these names instead
/// of a delegate, so pending events survive SAVE/RESTORE. Add a constant and a case in <see cref="Run"/> for
/// each new clock.
/// </summary>
public static class Clocks
{
    /// <summary>MATCH-FUNCTION (MATIN): a lit match burns out. Argument: the match's object id.</summary>
    public const string MatchOut = "MATCH-FUNCTION";

    /// <summary>FUSE-FUNCTION (FUSIN): the lit fuse burns down.</summary>
    public const string Fuse = "FUSE-FUNCTION";

    /// <summary>SAFE-MUNG (SAFIN): the room where the brick exploded collapses. Argument: that room's id.</summary>
    public const string SafeMung = "SAFE-MUNG";

    /// <summary>LEDGE-MUNG (LEDIN): the volcano ledge collapses.</summary>
    public const string LedgeMung = "LEDGE-MUNG";

    /// <summary>VOLGNOME (VLGIN): the volcano gnome appears.</summary>
    public const string Volgnome = "VOLGNOME";

    /// <summary>GNOME-FUNCTION (GNOIN): the gnome leaves.</summary>
    public const string GnomeLeave = "GNOME-FUNCTION";

    /// <summary>BUCKET (BCKIN): the bucket in the well rises or descends.</summary>
    public const string Bucket = "BUCKET";

    /// <summary>BURNUP (BRNIN): what burns in the balloon's receptacle burns out.</summary>
    public const string Burnup = "BURNUP";

    /// <summary>MAINT-ROOM (MNTIN): every move, the water in the maintenance room rises.</summary>
    public const string MaintRoom = "MAINT-ROOM";

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        MatchOut, Fuse, SafeMung, LedgeMung, Volgnome, GnomeLeave, Bucket, Burnup, MaintRoom,
    };

    /// <summary>True if <paramref name="name"/> is a clock event this port knows (used to validate save files).</summary>
    public static bool IsKnown(string name) => Known.Contains(name);

    public static void Run(GameState state, ScheduledEvent e)
    {
        switch (e.Name)
        {
            case MatchOut: ObjectActions.MatchBurnout(state, e.Argument); break;
            case Fuse: ObjectActions.FuseBurnedDown(state); break;
            case SafeMung: ObjectActions.SafeMung(state, e.Argument); break;
            case LedgeMung: ObjectActions.LedgeMung(state); break;
            case Volgnome: ObjectActions.Volgnome(state); break;
            case GnomeLeave: ObjectActions.GnomeLeave(state); break;
            case Bucket: ObjectActions.BucketTick(state); break;
            case Burnup: ObjectActions.Burnup(state); break;
            case MaintRoom: RoomActions.MaintRoomRising(state); break;
            default: throw new InvalidOperationException("Unknown clock event: " + e.Name);
        }
    }
}
