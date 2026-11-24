using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// Scenery and global objects (dung.56 SOBJECT, ADD-STAR): doors that never open (act1.38 DDOOR-FUNCTION), the
/// grue (act3.13 GRUE-FUNCTION), and walls, engravings and the like that can be read or examined.
/// </summary>
public class SceneryTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    [Theory]
    [InlineData("OPEN DOOR", "The door cannot be opened.")]
    [InlineData("BURN DOOR", "You cannot burn this door.")]
    public void The_front_door_never_opens(string command, string expected)
    {
        using var runner = new GameRunner();
        runner.Give("TORCH");

        Assert.Contains(expected, Do(runner, command));
    }

    [Fact]
    public void The_front_door_is_not_described()
    {
        using var runner = new GameRunner();

        Assert.DoesNotContain("The door is closed.", Do(runner, "LOOK"));
    }

    [Fact]
    public void The_grue_is_everywhere_and_never_seen()
    {
        using var runner = new GameRunner();

        Assert.Contains("The grue is a sinister, lurking presence", Do(runner, "EXAMINE GRUE"));
        Assert.Contains("There is no grue here, but I'm sure there is at least one lurking", Do(runner, "WHERE IS GRUE"));
    }

    [Theory]
    [InlineData("EXAMINE WALL", "I see nothing special about the wall.")]
    [InlineData("EXAMINE HANDS", "I see nothing special about the pair of hands.")]
    public void Global_objects_are_within_reach_anywhere(string command, string expected)
    {
        using var runner = new GameRunner();

        Assert.Contains(expected, Do(runner, command));
    }

    [Theory]
    [InlineData("CAVE4", "READ ENGRAVINGS", "The engravings were incised in the living rock")]
    [InlineData("TEMP1", "READ PRAYER", "The prayer is inscribed in an ancient script")]
    [InlineData("BWELL", "READ ETCHINGS", "o  b  o")]
    public void Inscriptions_can_be_read(string room, string command, string expected)
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport(room);

        Assert.Contains(expected, Do(runner, command));
    }

    [Fact]
    public void The_rainbow_is_seen_from_both_ends()
    {
        using var runner = new GameRunner();
        var rainbow = runner.State.World.FindObject("RAINB")!;

        Assert.Contains(rainbow, runner.State.World.FindRoom("FALLS")!.Objects);
        Assert.Contains(rainbow, runner.State.World.FindRoom("POG")!.Objects);
    }
}
