using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// The default verb routines and when they ask the objects (rooms.99 TAKE, DROPPER, PUTTER; act1.38 TIE, UNTIE, R/L,
/// TURNER). OBJECT-ACTION asks the indirect object first, then the direct one.
/// </summary>
public class VerbDefaultTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    private static GameRunner InLivingRoom()
    {
        var runner = new GameRunner();
        runner.Teleport("LROOM");
        return runner;
    }

    [Fact]
    public void What_you_take_goes_to_the_head_of_your_possessions()
    {
        using var runner = InLivingRoom();
        runner.Give("ROPE");

        Assert.Contains("Taken.", Do(runner, "TAKE SWORD"));
        Assert.Equal("SWORD", runner.State.Winner.Inventory[0].Id);
    }

    [Fact]
    public void The_rusty_knife_makes_the_sword_pulse_and_is_still_taken()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Give("SWORD");
        runner.Teleport("MAZE5");

        var output = Do(runner, "TAKE RUSTY KNIFE");

        Assert.Contains("your sword gives a single pulse of blinding blue light.", output);
        Assert.Contains("Taken.", output);
        Assert.Contains(runner.State.World.FindObject("RKNIF")!, runner.State.Winner.Inventory);
    }

    [Fact]
    public void A_load_too_heavy_is_refused()
    {
        using var runner = InLivingRoom();
        runner.Give("ROPE").Size = 95;

        Assert.Contains("Your load is too heavy.  You will have to leave something behind.", Do(runner, "TAKE SWORD"));
        Assert.DoesNotContain(runner.State.World.FindObject("SWORD")!, runner.State.Winner.Inventory);
    }

    [Fact]
    public void Something_that_cannot_be_taken_gets_a_yuk()
    {
        using var runner = InLivingRoom();

        var output = Do(runner, "TAKE RUG");
        Assert.Contains("The rug is extremely heavy and cannot be carried.", output);
        var yuk = Do(runner, "TAKE DOOR");
        Assert.Contains(new[] { "Nice try.", "You can't be serious.", "Chomp, Chomp.", "Not a prayer.", "I don't think so." }, yuk.Contains);
    }

    [Fact]
    public void A_refused_gift_is_left_on_the_floor()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        var garlic = runner.Give("GARLI");
        runner.Teleport("CYCLO");

        Assert.Contains("The cyclops may be hungry, but there is a limit.", Do(runner, "GIVE GARLIC TO CYCLOPS"));
        Assert.Same(runner.State.Here, garlic.InRoom);
    }

    [Fact]
    public void Nothing_can_be_taken_out_of_a_closed_bottle()
    {
        using var runner = new GameRunner();
        runner.Give("BOTTL");

        Assert.Contains("I can't reach that.", Do(runner, "DROP WATER"));
        Do(runner, "OPEN BOTTLE");
        Do(runner, "DROP WATER");
        Assert.Null(runner.State.World.FindObject("WATER")!.Container);
    }

    [Fact]
    public void Put_puts_the_object_at_the_head_of_the_contents()
    {
        using var runner = InLivingRoom();
        runner.Give("ROPE");
        runner.Give("KNIFE");
        Do(runner, "OPEN CASE");

        Do(runner, "PUT ROPE IN CASE");
        Do(runner, "PUT KNIFE IN CASE");

        Assert.Equal("KNIFE", runner.State.World.FindObject("TCASE")!.Contents[0].Id);
    }

    [Theory]
    [InlineData("TIE SWORD TO RUG", "How can you tie that to anything.")]
    [InlineData("UNTIE SWORD", "This cannot be tied, so it cannot be untied!")]
    [InlineData("UNTIE ROPE", "It is not tied to anything.")] // the rope's own action answers first
    [InlineData("RAISE SWORD", "Playing in this way with a sword")]
    [InlineData("TURN SWORD", "You can't turn that!")]
    public void Default_replies(string command, string reply)
    {
        using var runner = InLivingRoom();
        runner.Give("SWORD");
        runner.Give("ROPE");

        Assert.Contains(reply, Do(runner, command));
    }

    [Fact]
    public void Lighting_something_with_an_unlit_match_does_not_strike_it()
    {
        using var runner = new GameRunner();
        runner.Give("CANDL");
        var match = runner.Give("MATCH");

        Assert.Contains("You have to light them with something that's burning, you know.", Do(runner, "LIGHT CANDLES WITH MATCH"));
        Assert.Equal(0, match.LightAmount);
    }
}
