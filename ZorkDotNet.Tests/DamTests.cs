using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// Flood Control Dam #3: the maintenance room's buttons and leak (act1.38 MAINT-ROOM, LEAK-FUNCTION), the bolt and
/// sluice gates (BOLT-FUNCTION), and the drained reservoir (RESERVOIR-NORTH/SOUTH).
/// </summary>
public class DamTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    /// <summary>A player with a lit lamp in the maintenance room.</summary>
    private static GameRunner InMaintenanceRoom()
    {
        var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport("MAINT");
        return runner;
    }

    [Fact]
    public void Opening_the_dam_drains_the_reservoir_and_uncovers_the_trunk()
    {
        using var runner = InMaintenanceRoom();

        Assert.Contains("Click.", Do(runner, "PUSH YELLOW BUTTON"));
        Do(runner, "TAKE WRENCH");
        runner.Run("WEST", "SOUTH");
        Assert.Equal("DAM", runner.State.Here.Id);
        Assert.Contains("The green bubble is glowing.", Do(runner, "LOOK"));

        Assert.Contains("The sluice gates open and water pours through the dam.", Do(runner, "TURN BOLT WITH WRENCH"));
        Assert.Contains("the water level behind it is low and the sluice gate has been opened", Do(runner, "LOOK"));

        runner.Teleport("RESES");
        var reservoir = Do(runner, "LOOK");
        Assert.Contains("You are in the south end of a large cavernous room which was formerly a reservoir.", reservoir);
        Assert.Contains("Lying half buried in the mud is an old trunk, bulging with jewels.", reservoir);

        var score = runner.State.Winner.Score;
        Assert.Contains("Taken.", Do(runner, "TAKE TRUNK"));
        Assert.Equal(score + 15, runner.State.Winner.Score);

        Do(runner, "CROSS");
        Assert.Equal("RESEN", runner.State.Here.Id);
    }

    [Fact]
    public void Before_the_reservoir_drains_it_cannot_be_crossed_and_the_trunk_is_hidden()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport("RESES");

        var look = Do(runner, "LOOK");
        Assert.Contains("You are at the south end of a large reservoir.", look);
        Assert.DoesNotContain("trunk", look);
        Assert.Contains("You are not equipped for swimming.", Do(runner, "NORTH"));
    }

    [Fact]
    public void The_bolt_needs_the_yellow_button_and_the_wrench()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Give("WRENC");
        runner.Give("SCREW");
        runner.Teleport("DAM");

        Assert.Contains("The bolt won't turn with your best effort.", Do(runner, "TURN BOLT WITH WRENCH"));
        Assert.Contains("The bolt won't turn using the screwdriver.", Do(runner, "TURN BOLT WITH SCREWDRIVER"));
        Assert.False(runner.State.GetFlag("LOW-TIDE!-FLAG"));
    }

    [Fact]
    public void Turning_the_bolt_again_closes_the_gates()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Give("WRENC");
        runner.State.SetFlag("GATE-FLAG!-FLAG", true);
        runner.Teleport("DAM");
        Do(runner, "TURN BOLT WITH WRENCH");

        Assert.Contains("The sluice gates close and water starts to collect behind the dam.", Do(runner, "TURN BOLT WITH WRENCH"));
        Assert.False(runner.State.GetFlag("LOW-TIDE!-FLAG"));
        Assert.False(runner.State.World!.FindObject("TRUNK")!.IsVisible);
    }

    [Fact]
    public void The_brown_button_turns_the_bubble_off()
    {
        using var runner = InMaintenanceRoom();
        Do(runner, "PUSH YELLOW BUTTON");

        Assert.Contains("Click.", Do(runner, "PRESS BROWN BUTTON"));
        Assert.False(runner.State.GetFlag("GATE-FLAG!-FLAG"));
    }

    [Fact]
    public void The_red_button_switches_the_lights()
    {
        using var runner = InMaintenanceRoom();

        Assert.Contains("The lights within the room come on.", Do(runner, "PUSH RED BUTTON"));
        Assert.True(runner.State.Here.HasLight);
        Assert.Contains("The lights within the room shut off.", Do(runner, "PUSH RED BUTTON"));
    }

    [Fact]
    public void Which_button_has_to_be_said()
    {
        using var runner = InMaintenanceRoom();

        Assert.Contains("Which", Do(runner, "PUSH BUTTON"));
    }

    [Fact]
    public void The_blue_button_starts_a_leak_that_putty_stops()
    {
        using var runner = InMaintenanceRoom();

        var leak = Do(runner, "PUSH BLUE BUTTON");
        Assert.Contains("a stream of water appears to burst from the east wall of the room", leak);
        Assert.Contains("The water level here is now up to your shin.", leak);
        Assert.Contains("The water level here is now up to your shin.", Do(runner, "LOOK"));
        Assert.Contains("The water level here is now up to your knees.", Do(runner, "LOOK"));

        runner.Give("PUTTY");
        Assert.Contains("By some miracle of elven technology, you have managed to stop the leak in the dam.", Do(runner, "PLUG LEAK WITH PUTTY"));
        Assert.DoesNotContain("water level", Do(runner, "LOOK"));
        Assert.Contains("The blue button appears to be jammed.", Do(runner, "PUSH BLUE BUTTON"));
    }

    [Fact]
    public void Plugging_the_leak_with_something_else()
    {
        using var runner = InMaintenanceRoom();
        Do(runner, "PUSH BLUE BUTTON");
        runner.Give("SWORD");

        Assert.Contains("With a sword?", Do(runner, "PLUG LEAK WITH SWORD"));
    }

    [Fact]
    public void Staying_in_the_flooding_room_drowns_you_and_floods_it_for_good()
    {
        using var runner = InMaintenanceRoom();
        Do(runner, "PUSH BLUE BUTTON");

        var output = "";
        for (var i = 0; i < 20 && !runner.State.AwaitingPatchAnswer; i++) output += Do(runner, "LOOK");

        Assert.Contains("The water level here is now high in your lungs.", output);
        Assert.Contains("I'm afraid you have done drowned yourself.", output);
        Do(runner, "Y");
        runner.Teleport("LOBBY");
        Assert.Contains("The room is full of water and cannot be entered.", Do(runner, "NORTH"));
    }

    [Fact]
    public void The_tube_holds_putty_and_takes_nothing_else()
    {
        using var runner = new GameRunner();
        runner.Give("TUBE");
        runner.Give("SWORD");
        runner.Give("FUSE");

        Assert.Contains("Opening the tube reveals a viscous material.", Do(runner, "OPEN TUBE"));
        Assert.Contains("It won't fit.", Do(runner, "PUT SWORD IN TUBE")); // rooms.99 PUTTER checks the fit first
        Assert.Contains("The tube refuses to accept anything.", Do(runner, "PUT WIRE IN TUBE"));
    }

    [Fact]
    public void The_guidebooks_are_in_the_lobby()
    {
        using var runner = new GameRunner();
        runner.Teleport("LOBBY");

        Assert.Contains("Some guidebooks entitled 'Flood Control Dam #3' are on the reception desk.", Do(runner, "LOOK"));
        Do(runner, "TAKE GUIDEBOOK");
        Assert.Contains("Guide Book to", Do(runner, "READ GUIDEBOOK"));
    }
}
