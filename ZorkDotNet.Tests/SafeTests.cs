using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// The safe in the Dusty Room and the brick and fuse (act2.27 SAFE-FUNCTION, FUSE-FUNCTION, SAFE-MUNG, BRICK-FUNCTION,
/// rooms.99 PUTTER).
/// </summary>
public class SafeTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    private static GameRunner InDustyRoom()
    {
        var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Give("BRICK");
        runner.Give("FUSE");
        runner.Teleport("SAFE");
        return runner;
    }

    [Fact]
    public void Blowing_the_safe_open_with_the_brick_in_the_slot()
    {
        using var runner = InDustyRoom();
        Assert.Contains("The box is rusted and will not open.", Do(runner, "OPEN BOX"));

        Assert.Contains("Done.", Do(runner, "PUT WIRE IN BRICK"));
        Assert.Contains("Done.", Do(runner, "PUT BRICK IN SLOT"));
        Assert.Contains("The wire starts to burn.", Do(runner, "BURN WIRE"));
        Do(runner, "NORTH");
        Assert.Contains("There is an explosion nearby.", Do(runner, "WAIT"));
        Assert.True(runner.State.GetFlag("SAFE-FLAG!-FLAG"));

        Do(runner, "SOUTH");
        Assert.Contains("On the far wall is a rusty box, whose door has been blown off.", Do(runner, "LOOK"));
        Assert.Contains("Taken.", Do(runner, "TAKE CROWN"));
        Do(runner, "NORTH");

        var later = "";
        for (var i = 0; i < 5; i++) later += Do(runner, "LOOK");
        Assert.Contains("you hear an ominous rumbling", later);
        Assert.True(runner.State.World!.FindRoom("SAFE")!.Munged);
    }

    [Fact]
    public void A_brick_going_off_where_you_are_is_fatal()
    {
        using var runner = InDustyRoom();
        runner.Run("PUT WIRE IN BRICK", "BURN WIRE");

        var output = Do(runner, "LOOK") + Do(runner, "LOOK");

        Assert.Contains("the ability to blow you to smithereens", output);
        Assert.True(runner.State.AwaitingPatchAnswer);
    }

    [Fact]
    public void Burning_the_brick_itself_is_fatal()
    {
        using var runner = InDustyRoom();

        Assert.Contains("the ability to blow you to smithereens", Do(runner, "BURN BRICK"));
    }

    [Fact]
    public void A_wire_burning_by_itself_just_burns_away()
    {
        using var runner = InDustyRoom();
        Do(runner, "BURN WIRE");

        Assert.Contains("The wire rapidly burns into nothingness.", Do(runner, "LOOK") + Do(runner, "LOOK"));
    }

    [Theory]
    [InlineData("TAKE BOX", "The box is imbedded in the wall.")]
    [InlineData("CLOSE BOX", "The box is not open, chomper!")]
    public void The_box_is_part_of_the_wall(string command, string expected)
    {
        using var runner = InDustyRoom();

        Assert.Contains(expected, Do(runner, command));
    }

    [Fact]
    public void Putting_things_follows_putter()
    {
        using var runner = InDustyRoom();
        runner.Give("SWORD");

        Assert.Contains("I can't reach inside.", Do(runner, "PUT SWORD IN BOX"));
        Assert.Contains("It won't fit.", Do(runner, "PUT SWORD IN SLOT"));
        Assert.Contains("I can't do that.", Do(runner, "PUT SWORD IN LAMP"));
    }
}
