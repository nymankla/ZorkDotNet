using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// The temple, the candles and matches (act1.38 CANDLES, MATCH-FUNCTION, CAVE2-ROOM), the exorcism at the entrance
/// to Hades (LLD-ROOM), and the Land of the Living Dead and the tomb (BLACK-BOOK, BODY-FUNCTION, HEAD-FUNCTION,
/// COKE-BOTTLES).
/// </summary>
public class TempleTests
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

    [Fact]
    public void The_candles_burn_on_the_altar()
    {
        using var runner = WithLight("TEMP2");

        Assert.Contains("On the two ends of the altar are burning candles.", Do(runner, "LOOK"));
        Assert.True(runner.State.World!.FindObject("CANDL")!.LightAmount > 0);
    }

    [Fact]
    public void Candles_are_lit_with_a_burning_match()
    {
        using var runner = new GameRunner();
        var candles = runner.Give("CANDL");
        runner.Give("MATCH");

        Assert.Contains("The flame is extinguished.", Do(runner, "TURN OFF CANDLES"));
        Assert.Contains("With what?", Do(runner, "LIGHT CANDLES"));
        Assert.Contains("You have to light them with something that's burning, you know.", Do(runner, "LIGHT CANDLES WITH MATCH"));
        Assert.Contains("One of the matches starts to burn.", Do(runner, "LIGHT MATCH"));
        Assert.Contains("The candles are lighted.", Do(runner, "LIGHT CANDLES WITH MATCH"));
        Assert.Equal(1, candles.LightAmount);
    }

    [Fact]
    public void The_matchbook_has_four_matches_that_burn_two_moves_each()
    {
        using var runner = new GameRunner();
        runner.Give("MATCH");

        for (var i = 0; i < 4; i++)
        {
            Assert.Contains("One of the matches starts to burn.", Do(runner, "LIGHT MATCH"));
            Assert.Contains("The match has gone out.", Do(runner, "LOOK") + Do(runner, "LOOK"));
        }
        Assert.Contains("I'm afraid that you have run out of matches.", Do(runner, "LIGHT MATCH"));
    }

    [Fact]
    public void Taken_candles_burn_down_in_fifty_moves_and_then_shorten()
    {
        using var runner = new GameRunner();
        runner.Teleport("TEMP2");
        Do(runner, "TAKE CANDLES");

        var output = "";
        for (var i = 0; i < 50; i++) output += Do(runner, "LOOK");
        Assert.Contains("The candles grow shorter.", output);
        Assert.Equal(19, runner.State.Lights.CandleTicks); // 50 counted down (the TAKE turn included), then 20, one gone
    }

    [Fact]
    public void The_wind_in_the_cave_can_blow_the_candles_out()
    {
        using var runner = WithLight("CAVE2");
        runner.State.Random = new FixedRandom(0);
        var candles = runner.Give("CANDL");
        var exit = runner.State.Here.Exits.First(e => e.Value.RoomId != null && !e.Value.IsConditional);
        Do(runner, exit.Key);

        Assert.Contains("The cave is very windy at the moment and your candles have blown out.", Do(runner, OppositeOf(exit.Key)));
        Assert.Equal(-1, candles.LightAmount);
    }

    private static string OppositeOf(string direction) => direction switch
    {
        "NORTH" => "SOUTH",
        "SOUTH" => "NORTH",
        "EAST" => "WEST",
        "WEST" => "EAST",
        "UP" => "DOWN",
        "DOWN" => "UP",
        "NE" => "SW",
        "SW" => "NE",
        "NW" => "SE",
        "SE" => "NW",
        _ => direction,
    };

    [Fact]
    public void The_exorcism_drives_out_the_spirits()
    {
        using var runner = WithLight("LLD1");
        Assert.Contains("The way through the gate is barred by evil spirits", Do(runner, "LOOK"));
        Assert.Contains("You are not equipped for an exorcism.", Do(runner, "EXORCISE"));

        runner.Give("BELL");
        runner.Give("BOOK");
        runner.Give("CANDL");
        Assert.Contains("\"Begone, fiends!\"", Do(runner, "EXORCISE"));
        Assert.True(runner.State.GetFlag("LLD-FLAG!-FLAG"));
        Assert.DoesNotContain("barred by evil spirits", Do(runner, "LOOK"));
    }

    [Fact]
    public void Exorcising_again_once_the_spirits_are_gone_dismisses_you()
    {
        using var runner = WithLight("LLD1");
        runner.Give("BELL");
        runner.Give("BOOK");
        runner.Give("CANDL");
        Do(runner, "EXORCISE");

        Assert.Contains("\"Begone, chomper!\"", Do(runner, "EXORCISE"));
        Assert.True(runner.State.AwaitingPatchAnswer);
    }

    [Fact]
    public void Exorcising_anywhere_else_does_nothing()
    {
        using var runner = new GameRunner();

        Assert.Contains("Nothing happens.", Do(runner, "EXORCISE"));
    }

    [Fact]
    public void The_spirits_cannot_be_touched()
    {
        using var runner = WithLight("LLD1");

        Assert.Contains("You seem unable to affect these spirits.", Do(runner, "TAKE SPIRITS"));
    }

    [Fact]
    public void Burning_the_black_book_is_fatal()
    {
        using var runner = new GameRunner();
        runner.Give("BOOK");
        runner.Give("TORCH");

        Assert.Contains("A booming voice says 'Wrong, cretin!'", Do(runner, "BURN BOOK"));
        Assert.True(runner.State.AwaitingPatchAnswer);
    }

    [Fact]
    public void The_bodies_in_the_land_of_the_dead()
    {
        using var runner = WithLight("LLD2");
        runner.Give("TORCH");

        Assert.Contains("A force keeps you from taking the bodies.", Do(runner, "TAKE BODIES"));
        Assert.Contains("places your head on a pole", Do(runner, "BURN BODIES"));
        Do(runner, "Y");
        runner.Teleport("LLD2");
        Assert.Contains("Amid the desolation, you spot what appears to be your head, at the end of a long pole.", Do(runner, "LOOK"));
    }

    [Fact]
    public void Tampering_with_the_tomb_costs_your_valuables_and_your_life()
    {
        using var runner = WithLight("TOMB");
        var pearl = runner.Give("PEARL");

        Assert.Contains("Therefore, they took steps to prevent this.", Do(runner, "TAKE HEADS"));
        Assert.True(runner.State.AwaitingPatchAnswer);
        var lcase = runner.State.World!.FindObject("LCASE")!;
        Assert.Equal("LROOM", lcase.InRoom?.Id);
        Assert.Contains(pearl, lcase.Contents);
    }

    [Fact]
    public void Reading_the_tomb_is_safe()
    {
        using var runner = WithLight("TOMB");

        Assert.False(runner.State.AwaitingPatchAnswer);
        Do(runner, "READ TOMB");
        Assert.False(runner.State.AwaitingPatchAnswer);
    }

    [Fact]
    public void Breaking_the_coke_bottles_makes_them_vanish()
    {
        using var runner = WithLight("TOMB");
        Do(runner, "TAKE BOTTLES");

        Assert.Contains("they were made of magic glass and disappear immediately", Do(runner, "THROW BOTTLES"));
    }
}
