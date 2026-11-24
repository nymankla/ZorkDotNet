using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// Dying and being patched up (rooms.99 JIGS-UP), the grue in the dark (rooms.99 WALK), and RDCOM's "Nothing
/// happens." for a command that says nothing.
/// </summary>
public class DeathTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    /// <summary>Jumping into the chasm (its DOWN is a #NEXIT) kills you every time.</summary>
    private static string Die(GameRunner runner)
    {
        runner.Teleport("CHAS1");
        return Do(runner, "JUMP");
    }

    [Fact]
    public void Dying_costs_ten_points_and_asks_to_patch_you_up()
    {
        using var runner = new GameRunner();
        runner.State.Winner.Score = 25;

        var output = Die(runner);

        Assert.EndsWith("Do you want me to try to patch you?", output.TrimEnd());
        Assert.True(runner.State.AwaitingPatchAnswer);
        Assert.Equal(15, runner.State.Winner.Score);
        Assert.Equal(1, runner.State.Progress.Deaths);
    }

    [Fact]
    public void Being_patched_up_puts_you_in_the_forest_and_scatters_your_things()
    {
        using var runner = new GameRunner();
        var world = runner.State.World;
        runner.Give("SWORD");
        runner.Give("ROPE");
        runner.Give("PEARL"); // a treasure
        Die(runner);
        var moves = runner.State.Moves;

        var output = Do(runner, "Y");

        Assert.Contains("Now, let me see..." + Environment.NewLine + "Well, we weren't quite able to restore your state.  You can't have everything.", output);
        Assert.Equal("FORE1", runner.State.Here.Id);
        Assert.Equal(moves, runner.State.Moves); // the answer is not a move
        Assert.Empty(runner.State.Winner.Inventory);
        Assert.Equal("LROOM", world.FindObject("LAMP")!.InRoom?.Id); // the lamp always comes back, to the living room
        Assert.Equal("KITCH", world.FindObject("SWORD")!.InRoom?.Id);
        Assert.Equal("CLEAR", world.FindObject("ROPE")!.InRoom?.Id);
        Assert.Equal("ALITR", world.FindObject("PEARL")!.InRoom?.Id); // treasures go to the head of ROOMS
        Assert.True(runner.State.Running);
    }

    [Fact]
    public void An_empty_answer_counts_as_yes()
    {
        using var runner = new GameRunner();
        Die(runner);

        Do(runner, "");

        Assert.Equal("FORE1", runner.State.Here.Id);
    }

    [Fact]
    public void Refusing_to_be_patched_ends_the_game_with_the_score()
    {
        using var runner = new GameRunner();
        Die(runner);

        var output = Do(runner, "NO");

        Assert.Contains("What?  You don't trust me?  Why, only last week I patched a running ITS and it survived for over 30 seconds.  Oh, well.", output);
        Assert.Contains("Your score is ", output);
        Assert.False(runner.State.Running);
    }

    [Fact]
    public void The_third_death_is_final()
    {
        using var runner = new GameRunner();
        Die(runner);
        Do(runner, "Y");
        Die(runner);
        Do(runner, "Y");

        var output = Die(runner);

        Assert.Contains("You clearly are a suicidal maniac.", output);
        Assert.DoesNotContain("patch you", output);
        Assert.False(runner.State.Running);
    }

    [Fact]
    public void In_the_dark_a_grue_gets_you_on_the_way_to_another_dark_room()
    {
        using var runner = new GameRunner();
        runner.State.Random = new FixedRandom(0);
        runner.Teleport("CELLA");

        Assert.Contains("Oh, no!  A fearsome grue slithered into the room and devoured you.", Do(runner, "EAST"));
        Assert.True(runner.State.AwaitingPatchAnswer);
    }

    [Fact]
    public void In_the_dark_a_grue_gets_you_where_there_is_no_way()
    {
        using var runner = new GameRunner();
        runner.State.Random = new FixedRandom(0);
        runner.Teleport("CELLA");

        Assert.Contains("Oh, no!  You walked into the slavering fangs of a lurking grue.", Do(runner, "NORTH"));
    }

    [Fact]
    public void In_the_dark_you_can_still_reach_a_lit_room()
    {
        using var runner = new GameRunner();
        runner.State.Random = new FixedRandom(0);
        runner.State.SetFlag("TRAP-DOOR!-FLAG", true);
        runner.Teleport("CELLA");

        Do(runner, "UP");

        Assert.Equal("LROOM", runner.State.Here.Id);
        Assert.False(runner.State.AwaitingPatchAnswer);
    }

    [Fact]
    public void One_time_in_four_you_get_through_the_dark()
    {
        using var runner = new GameRunner();
        runner.State.Random = new FixedRandom(75);
        runner.Teleport("CELLA");

        Do(runner, "EAST");

        Assert.Equal("MTROL", runner.State.Here.Id);
    }

    [Fact]
    public void A_command_that_says_nothing_gets_nothing_happens()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport("SAFE"); // BLAST there is left to the (not yet ported) safe explosion

        Assert.Equal("> BLAST" + Environment.NewLine + "Nothing happens." + Environment.NewLine, Do(runner, "BLAST"));
    }
}
