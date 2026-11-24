using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// The opening of the game as in the original: the mailbox and leaflet, the kitchen, the rug and trap door, the
/// lamp, the cellar, and the sword (dung.56 data; act1.38 RUG, CELLAR, SWORD-GLOW, READER; rooms.99 ROOM-INFO,
/// OPENER, PRINT-CONT).
/// </summary>
public class StartOfGameTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    private static readonly string[] ToLivingRoom = ["NORTH", "EAST", "OPEN WINDOW", "WEST", "WEST"];

    [Fact]
    public void The_mailbox_holds_the_leaflet_and_stays_where_it_is()
    {
        using var runner = new GameRunner();

        Assert.Contains("There is a small mailbox here.", Do(runner, "LOOK"));
        Assert.Contains("Opening the mailbox reveals a leaflet.", Do(runner, "OPEN MAILBOX"));
        Assert.Contains("Taken.", Do(runner, "TAKE LEAFLET"));

        var leaflet = Do(runner, "READ LEAFLET");
        Assert.Contains("WELCOME TO DUNGEON", leaflet);
        Assert.Contains("Tim Anderson, Marc Blank,", leaflet);

        Do(runner, "TAKE MAILBOX");
        Assert.DoesNotContain(runner.State.World!.FindObject("MAILB")!, runner.State.Winner.Inventory);
    }

    [Fact]
    public void The_living_room_has_the_newspaper()
    {
        using var runner = new GameRunner();
        runner.Run(ToLivingRoom);

        Assert.Contains("Taken.", Do(runner, "TAKE NEWSPAPER"));
        Assert.Contains("US NEWS & DUNGEON REPORT", Do(runner, "READ NEWSPAPER"));
    }

    [Fact]
    public void Reading_in_the_dark_is_impossible()
    {
        using var runner = new GameRunner();
        runner.Give("PAPER");
        runner.Teleport("CELLA");

        Assert.Contains("It is impossible to read in the dark.", Do(runner, "READ NEWSPAPER"));
    }

    [Fact]
    public void The_kitchen_describes_untouched_objects_and_the_bottle_contents()
    {
        using var runner = new GameRunner();
        runner.Run("NORTH", "EAST", "OPEN WINDOW");

        var kitchen = Do(runner, "WEST");

        Assert.Contains("On the table is an elongated brown sack, smelling of hot peppers.", kitchen);
        Assert.Contains("A bottle is sitting on the table." + Environment.NewLine +
                        "The glass bottle contains:" + Environment.NewLine +
                        " A quantity of water", kitchen);
        Assert.DoesNotContain("match", kitchen, StringComparison.OrdinalIgnoreCase);

        // Once taken, an object is described by its ordinary text.
        runner.Run("TAKE BOTTLE", "DROP BOTTLE");
        Assert.Contains("A clear glass bottle is here.", Do(runner, "LOOK"));
    }

    [Fact]
    public void The_matchbook_is_in_the_dam_lobby()
    {
        using var runner = new GameRunner();
        runner.Teleport("LOBBY");

        Assert.Contains("There is a matchbook whose cover says 'Visit Beautiful FCD#3' here.", Do(runner, "LOOK"));
    }

    [Fact]
    public void Moving_the_rug_reveals_the_trap_door_once()
    {
        using var runner = new GameRunner();
        runner.Run(ToLivingRoom);

        Assert.Contains("The rug is extremely heavy and cannot be carried.", Do(runner, "TAKE RUG"));
        Assert.Contains("With a great effort, the rug is moved to one side of the room.", Do(runner, "MOVE RUG"));
        Assert.Contains("Having moved the carpet previously, you find it impossible to move it again.", Do(runner, "MOVE RUG"));
        Assert.Contains("The door reluctantly opens", Do(runner, "OPEN TRAP DOOR"));
    }

    [Theory]
    [InlineData("TURN ON LAMP", "The lamp is now on.")]
    [InlineData("TURN OFF LAMP", "The lamp is now off.")]
    public void TURN_ON_and_TURN_OFF_take_the_preposition_before_the_object(string command, string expected)
    {
        using var runner = new GameRunner();
        var lamp = runner.Give("LAMP");
        if (command.Contains("OFF", StringComparison.Ordinal)) lamp.LightAmount = 1;

        var output = Do(runner, command);

        Assert.Contains(expected, output);
        Assert.DoesNotContain("I can't see", output);
    }

    [Fact]
    public void Going_down_the_trap_door_describes_the_cellar_before_the_door_slams()
    {
        using var runner = new GameRunner();
        runner.Run(ToLivingRoom);
        runner.Run("TAKE LAMP", "TURN ON LAMP", "MOVE RUG", "OPEN TRAP DOOR");

        var cellar = Do(runner, "DOWN");

        var description = cellar.IndexOf("You are in a dark and damp cellar", StringComparison.Ordinal);
        var slam = cellar.IndexOf("The trap door crashes shut, and you hear someone barring it.", StringComparison.Ordinal);
        Assert.True(description >= 0 && slam > description, cellar);
        Assert.DoesNotContain("The trap door is open.", cellar);
        Assert.False(runner.State.GetFlag("TRAP-DOOR!-FLAG"));
        Assert.Contains("The trap door has been barred from the other side.", Do(runner, "UP"));
    }

    [Fact]
    public void Without_light_the_trap_door_does_not_slam()
    {
        // In the dark, ROOM-INFO stops at the grue warning, so CELLAR's WALK-IN never runs.
        using var runner = new GameRunner();
        runner.Run(ToLivingRoom);
        runner.Run("MOVE RUG", "OPEN TRAP DOOR");

        var cellar = Do(runner, "DOWN");

        Assert.Contains("It is pitch black.  You are likely to be eaten by a grue.", cellar);
        Assert.DoesNotContain("crashes shut", cellar);
        Assert.True(runner.State.GetFlag("TRAP-DOOR!-FLAG"));
    }

    [Fact]
    public void The_sword_glows_only_near_a_villain()
    {
        using var runner = new GameRunner();
        runner.Run(ToLivingRoom);
        Assert.DoesNotContain("glow", Do(runner, "TAKE SWORD"));

        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport("CELLA"); // the troll room is east of the cellar
        Assert.Contains("Your sword is glowing with a faint blue glow.", Do(runner, "LOOK"));

        Assert.Contains("Your sword has begun to glow very brightly.", Do(runner, "EAST"));
        Assert.Contains("Your sword is glowing with a faint blue glow.", Do(runner, "WEST"));
    }

    [Fact]
    public void The_bat_carries_you_off_and_the_new_room_is_described()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport("BATS");
        var bats = runner.State.Here;

        var output = Do(runner, "LOOK");
        Assert.DoesNotContain("swoops down", output); // only WALK-IN, not LOOK, sends the bat

        var room = runner.State.World!.FindRoom("BATS")!;
        runner.Teleport(room.Exits["EAST"].RoomId!);
        output = Do(runner, "WEST");

        var flown = output.IndexOf("lifts you away....", StringComparison.Ordinal);
        Assert.True(flown >= 0, output);
        Assert.NotEqual(bats, runner.State.Here);
        Assert.False(string.IsNullOrWhiteSpace(output[(flown + "lifts you away....".Length)..]), output);
    }
}
