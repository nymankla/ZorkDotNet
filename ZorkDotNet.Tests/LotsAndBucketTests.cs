using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// TAKE / DROP / PUT ALL and VALUABLES (rooms.99 EVERYTHING, VALUABLES, FROB-LOTS), the well bucket (act3.13 BUCKET,
/// PASS-THE-BUCKET), and the pot of gold (act2.27 STICK-FUNCTION).
/// </summary>
public class LotsAndBucketTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    [Fact]
    public void Take_all_takes_each_thing_in_turn()
    {
        using var runner = new GameRunner();
        runner.Teleport("KITCH");

        var output = Do(runner, "TAKE ALL");

        Assert.Contains("sandwich bag:" + Environment.NewLine + "  Taken.", output);
        Assert.Contains("glass bottle:" + Environment.NewLine + "  Taken.", output);
        Assert.Contains(runner.State.World!.FindObject("SBAG")!, runner.State.Winner.Inventory);
    }

    [Fact]
    public void Drop_all_drops_everything_you_carry()
    {
        using var runner = new GameRunner();
        runner.Give("SWORD");
        runner.Give("ROPE");

        var output = Do(runner, "DROP ALL");

        Assert.Contains("sword:" + Environment.NewLine + "  Dropped.", output);
        Assert.Empty(runner.State.Winner.Inventory);
    }

    [Fact]
    public void Take_valuables_takes_only_treasures()
    {
        using var runner = new GameRunner();
        runner.Teleport("LROOM");
        var pearl = runner.State.World!.FindObject("PEARL")!;
        pearl.MoveTo(runner.State.Here);

        var output = Do(runner, "TAKE VALUABLES");

        Assert.Contains("pearl necklace:", output);
        Assert.DoesNotContain("sword:", output);
    }

    [Fact]
    public void With_nothing_to_take_it_says_so()
    {
        using var runner = new GameRunner();
        runner.Teleport("NHOUS");

        Assert.Contains("I couldn't find anything.", Do(runner, "TAKE ALL"));
    }

    [Fact]
    public void Water_in_the_bucket_raises_it_and_it_comes_down_again_when_it_dries()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Give("BOTTL");
        runner.Teleport("BWELL");

        Assert.Contains("You are in the wooden bucket.", Do(runner, "BOARD BUCKET"));
        Do(runner, "OPEN BOTTLE");
        var rise = Do(runner, "PUT WATER IN BUCKET");
        Assert.Contains("There is now a puddle in the bottom of the wooden bucket.", rise);
        Assert.Contains("The bucket rises and comes to a stop.", rise);
        Assert.Equal("TWELL", runner.State.Here.Id);
        Assert.Contains("You can't go there in a wooden bucket.", Do(runner, "EAST"));

        var later = "";
        for (var i = 0; i < 101 && runner.State.Here.Id == "TWELL"; i++) later += Do(runner, "LOOK");
        Assert.Contains("The bucket descends and comes to a stop.", later);
        Assert.Equal("BWELL", runner.State.Here.Id);
    }

    [Fact]
    public void Waving_the_stick_at_the_end_of_the_rainbow_shows_the_pot_of_gold()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Give("STICK");
        runner.Teleport("POG");
        Assert.DoesNotContain("pot of gold", Do(runner, "LOOK"));

        Assert.Contains("Suddenly, the rainbow appears to become solid", Do(runner, "WAVE STICK"));
        Assert.Contains("At the end of the rainbow is a pot of gold.", Do(runner, "LOOK"));
    }
}
