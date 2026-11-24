using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// The robot and its rooms (act3.13 ROBOT-ACTOR, ROBOT-FUNCTION, BUTTONS, SPHERE-FUNCTION, MAGNET-ROOM, rooms.99
/// COMMAND) and the coal machine (act2.27 MACHINE-FUNCTION, MSWITCH-FUNCTION).
/// </summary>
public class RobotTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    private static GameRunner WithLight(string roomId)
    {
        var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport(roomId);
        return runner;
    }

    /// <summary>The robot walks along with you: you go first, it is told to follow.</summary>
    private static void GoTogether(GameRunner runner, string direction)
    {
        runner.Execute("TELL ROBOT '" + direction + "'");
        runner.Execute(direction);
    }

    [Fact]
    public void The_robot_is_in_the_low_room_and_can_be_told_to_walk()
    {
        using var runner = WithLight("MAGNE");

        Assert.Contains("There is a robot here.", Do(runner, "LOOK"));
        Assert.Contains("Done.", Do(runner, "TELL ROBOT 'EAST'"));
        Assert.Equal("CMACH", runner.State.Robot.CurrentRoom.Id);
    }

    [Fact]
    public void The_robot_frees_you_from_the_cage_and_you_get_the_sphere()
    {
        using var runner = WithLight("MAGNE");
        GoTogether(runner, "EAST");
        GoTogether(runner, "SOUTH");
        Assert.Equal("CAGER", runner.State.Here.Id);
        Assert.Equal("CAGER", runner.State.Robot.CurrentRoom.Id);

        Assert.Contains("an iron cage falls from the ceiling to entrap you", Do(runner, "TAKE SPHERE"));
        Assert.Equal("CAGED", runner.State.Here.Id);
        Assert.Contains("The cage shakes and is hurled across the room.", Do(runner, "TELL ROBOT 'RAISE CAGE'"));
        Assert.Equal("CAGER", runner.State.Here.Id);
        Assert.Contains("Taken.", Do(runner, "TAKE SPHERE"));
    }

    [Fact]
    public void Reaching_for_the_sphere_alone_is_fatal()
    {
        using var runner = WithLight("CAGER");

        Assert.Contains("Time passes...and you die from some obscure poisoning.", Do(runner, "TAKE SPHERE"));
    }

    [Fact]
    public void Caged_too_long_the_gas_gets_you()
    {
        using var runner = WithLight("MAGNE");
        GoTogether(runner, "EAST");
        GoTogether(runner, "SOUTH");
        Do(runner, "TAKE SPHERE");

        var output = "";
        for (var i = 0; i < 10 && !runner.State.AwaitingPatchAnswer; i++) output += Do(runner, "LOOK");
        Assert.Contains("you die from some obscure poisoning", output);
    }

    [Fact]
    public void The_robot_reaching_for_the_sphere_is_crushed()
    {
        using var runner = WithLight("MAGNE");
        GoTogether(runner, "EAST");
        GoTogether(runner, "SOUTH");

        var output = Do(runner, "TELL ROBOT 'TAKE SPHERE'");

        Assert.Contains("The robot has died.", output);
        Assert.Null(runner.State.World.FindObject("ROBOT")!.InRoom);
        Assert.Contains("There is an iron cage in the middle of the room.", Do(runner, "LOOK"));
    }

    [Fact]
    public void Pushing_a_machine_room_button_yourself_is_fatal()
    {
        using var runner = WithLight("CMACH");

        Assert.Contains("There is a giant spark and you are fried to a crisp.", Do(runner, "PUSH SQUARE BUTTON"));
    }

    [Fact]
    public void The_robot_works_the_carousel_buttons()
    {
        using var runner = WithLight("MAGNE");
        GoTogether(runner, "EAST");

        Assert.Contains("A dull thump is heard in the distance.", Do(runner, "TELL ROBOT 'PUSH TRIANGULAR BUTTON'"));
        Assert.True(runner.State.GetFlag("CAROUSEL-FLIP!-FLAG"));
        Assert.True(runner.State.World.FindObject("IRBOX")!.IsVisible);
        Assert.Contains("The whirring increases in intensity slightly.", Do(runner, "TELL ROBOT 'PUSH SQUARE BUTTON'"));
        Assert.Contains("The whirring decreases in intensity slightly.", Do(runner, "TELL ROBOT 'PUSH ROUND BUTTON'"));
    }

    [Fact]
    public void The_robot_takes_gifts_and_shows_what_it_carries()
    {
        using var runner = WithLight("MAGNE");
        runner.Give("SWORD");

        Assert.Contains("The robot gladly takes the sword and nods his head-like appendage in thanks.", Do(runner, "GIVE SWORD TO ROBOT"));
        Assert.Contains("The robot is carrying:" + Environment.NewLine + "A sword", Do(runner, "LOOK"));
    }

    [Theory]
    [InlineData("TELL ROBOT 'EAT LABEL'", "\"I am sorry but that action is difficult in the absence of a mouth.\"")]
    [InlineData("TELL ROBOT 'READ LABEL'", "\"My vision is not that good without eyes.\"")]
    [InlineData("TELL ROBOT 'INVENTORY'", "\"I am only a stupid robot and cannot perform that command.\"")]
    [InlineData("TELL WALL 'EAST'", "You cannot talk to that!")]
    public void The_robot_only_does_some_things(string command, string expected)
    {
        using var runner = WithLight("MAGNE");

        Assert.Contains(expected, Do(runner, command));
    }

    [Fact]
    public void From_the_low_room_only_southeast_and_east_lead_anywhere()
    {
        using var runner = WithLight("MAGNE");

        Assert.Contains("Nothing happens.", Do(runner, "NORTH"));
        Assert.Equal("MAGNE", runner.State.Here.Id);
        Do(runner, "SE");
        Assert.Equal("ALICE", runner.State.Here.Id);
    }

    [Fact]
    public void While_the_carousel_spins_the_low_room_spins_you_too()
    {
        using var runner = WithLight("ALICE");
        runner.State.SetFlag("CAROUSEL-FLIP!-FLAG", true);

        Assert.Contains("As you enter, your compass starts spinning wildly.", Do(runner, "NW"));
        Assert.Contains("You cannot get your bearings...", Do(runner, "NORTH"));
        Assert.NotEqual("MAGNE", runner.State.Here.Id);
    }

    [Fact]
    public void At_full_speed_the_magnetic_rooms_kill()
    {
        using var runner = WithLight("ALICE");
        runner.State.SetFlag("CAROUSEL-FLIP!-FLAG", true);
        runner.State.SetFlag("CAROUSEL-ZOOM!-FLAG", true);

        Assert.Contains("something just killed you", Do(runner, "NW"));
    }

    [Fact]
    public void The_machine_turns_coal_into_a_diamond()
    {
        using var runner = WithLight("MACHI");
        runner.Give("COAL");
        runner.Give("SCREW");

        Assert.Contains("The lid opens.", Do(runner, "OPEN MACHINE"));
        Do(runner, "PUT COAL IN MACHINE");
        Assert.Contains("The lid closes.", Do(runner, "CLOSE MACHINE"));
        Assert.Contains("The machine comes to life", Do(runner, "TURN SWITCH WITH SCREWDRIVER"));
        Do(runner, "OPEN MACHINE");
        Assert.Contains("Taken.", Do(runner, "TAKE DIAMOND"));
    }
}
