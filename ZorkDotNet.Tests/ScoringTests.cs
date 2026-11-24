using Xunit;
using ZorkDotNet.Game;

namespace ZorkDotNet.Tests;

/// <summary>SCORE, ranks, SCORE-MAX and QUIT (rooms.99 SCORE / FINISH, defs.63 YES/NO).</summary>
public class ScoringTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    [Fact]
    public void Max_score_is_room_and_object_values_plus_the_light_shaft()
    {
        using var runner = new GameRunner();
        var world = runner.State.World!;
        var expected = world.Rooms.Sum(r => r.VisitScore) + world.Objects.Sum(o => o.FindScore + o.TrophyScore) + 10;

        Assert.Equal(expected, runner.State.MaxScore);
    }

    [Fact]
    public void Score_command_prints_the_original_report()
    {
        using var runner = new GameRunner();
        var output = Do(runner, "SCORE");

        Assert.Contains($"Your score would be 0 [total of {runner.State.MaxScore} points], in 1 move.", output);
        Assert.Contains("This score gives you the rank of Beginner.", output);
    }

    [Fact]
    public void Score_counts_moves_in_plural()
    {
        using var runner = new GameRunner();
        runner.Execute("LOOK");

        Assert.Contains("in 2 moves.", Do(runner, "SCORE"));
    }

    [Theory]
    [InlineData(100, 100, "Cheater")]
    [InlineData(96, 100, "Wizard")]
    [InlineData(95, 100, "Master")]
    [InlineData(90, 100, "Master")]
    [InlineData(81, 100, "Winner")]
    [InlineData(61, 100, "Hacker")]
    [InlineData(60, 100, "Adventurer")]
    [InlineData(40, 100, "Adventurer")]
    [InlineData(20, 100, "Junior Adventurer")]
    [InlineData(10, 100, "Novice Adventurer")]
    [InlineData(5, 100, "Amateur Adventurer")]
    [InlineData(4, 100, "Beginner")]
    [InlineData(0, 100, "Beginner")]
    public void Rank_follows_the_original_table(int score, int max, string rank)
    {
        Assert.Equal(rank, Scoring.Rank(score, max));
    }

    [Fact]
    public void Quit_shows_the_score_and_asks_before_leaving()
    {
        using var runner = new GameRunner();
        var output = Do(runner, "QUIT");

        Assert.Contains("Your score would be 0 [total of", output);
        Assert.Contains("Do you wish to leave the game? (Y is affirmative):", output);
        Assert.True(runner.State.Running);
    }

    [Theory]
    [InlineData("Y")]
    [InlineData("yes")]
    [InlineData("t")]
    public void Answering_yes_ends_the_game(string answer)
    {
        using var runner = new GameRunner();
        runner.Run("QUIT", answer);

        Assert.False(runner.State.Running);
    }

    [Theory]
    [InlineData("N")]
    [InlineData("no")]
    [InlineData("maybe")]
    public void Any_other_answer_keeps_playing_and_is_not_a_move(string answer)
    {
        using var runner = new GameRunner();
        runner.Run("QUIT", answer);

        Assert.True(runner.State.Running);
        Assert.False(runner.State.AwaitingQuitConfirmation);
        Assert.Equal(1, runner.State.Moves);

        runner.Execute("NORTH");
        Assert.Equal("NHOUS", runner.State.Here.Id);
    }
}
