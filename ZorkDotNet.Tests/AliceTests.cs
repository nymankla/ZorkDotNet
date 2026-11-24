using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// The Alice rooms (act3.13 EATME-FUNCTION, CAKE-FUNCTION, FLASK-FUNCTION, ICEBOOM) and the painting in the gallery
/// (act1.38 PAINTING).
/// </summary>
public class AliceTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    private static GameRunner InTeaRoom()
    {
        var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport("ALICE");
        return runner;
    }

    [Fact]
    public void The_cakes_shrink_you_to_the_spices_and_grow_you_back()
    {
        using var runner = InTeaRoom();
        runner.Run("TAKE RED CAKE", "TAKE BLUE CAKE");

        Assert.Contains("Suddenly, the room appears to have become very large.", Do(runner, "EAT EATME CAKE"));
        Assert.Equal("ALISM", runner.State.Here.Id);
        var table = runner.State.World!.FindObject("ATABL")!;
        Assert.Equal("ALISM", table.InRoom?.Id); // the room's things come along, 64 times larger

        Do(runner, "EAST");
        Assert.Equal("ALITR", runner.State.Here.Id);
        Assert.Contains("The pool of water evaporates, revealing a tin of rare spices.", Do(runner, "THROW RED CAKE AT POOL"));
        Assert.Contains("Taken.", Do(runner, "TAKE SPICES"));

        Do(runner, "WEST");
        Assert.Contains("The room around you seems to be getting smaller.", Do(runner, "EAT BLUE CAKE"));
        Assert.Equal("ALICE", runner.State.Here.Id);
        Assert.Equal("ALICE", table.InRoom?.Id);
    }

    [Fact]
    public void The_icing_can_be_read_through_the_flask()
    {
        using var runner = InTeaRoom();
        runner.Give("FLASK");
        Do(runner, "TAKE RED CAKE");

        Assert.Contains("The only writing legible is a capital E.", Do(runner, "READ RED CAKE"));
        Assert.Contains("The icing, now visible, says 'Evaporate'.", Do(runner, "READ RED CAKE WITH FLASK"));
    }

    [Fact]
    public void The_orange_cake_explodes()
    {
        using var runner = InTeaRoom();

        Assert.Contains("You have been blasted to smithereens", Do(runner, "EAT ORANGE CAKE"));
        Assert.True(runner.State.World!.FindRoom("ALICE")!.Munged);
    }

    [Fact]
    public void The_blue_cake_in_the_tea_room_crushes_you()
    {
        using var runner = InTeaRoom();

        Assert.Contains("The room seems to have become too small to hold you.", Do(runner, "EAT BLUE CAKE"));
    }

    [Fact]
    public void Opening_the_flask_is_fatal()
    {
        using var runner = InTeaRoom();
        runner.Give("FLASK");

        Assert.Contains("the vapors from the flask's contents are fatal", Do(runner, "OPEN FLASK"));
        Assert.True(runner.State.AwaitingPatchAnswer);
    }

    [Fact]
    public void Breaking_the_painting_makes_it_worthless()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport("GALLE");

        Assert.Contains("on the far wall is a work of unparalleled beauty", Do(runner, "LOOK"));
        Assert.Contains("you have destroyed one", Do(runner, "BREAK PAINTING"));
        var art = runner.State.World!.FindObject("PAINT")!;
        Assert.Equal(0, art.TrophyScore);
        Assert.Equal("worthless piece of canvas", art.DescriptionShort);
    }
}
