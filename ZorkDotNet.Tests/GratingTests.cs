using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// UNLOCK / LOCK and the grating between the maze (MGRAT) and the clearing (act1.38 UNLOCKER, LOCKER,
/// GRAT1-FUNCTION, GRAT2-FUNCTION).
/// </summary>
public class GratingTests
{
    private static readonly string[] DummyReplies = ["Look around.", "You think it isn't?", "I think you've already done that."];

    /// <summary>Player in the (dark) grating room with a lit lamp and, optionally, the skeleton keys.</summary>
    private static GameRunner InGratingRoom(bool withKeys = true)
    {
        var runner = new GameRunner();
        runner.Give("LAMP");
        runner.Execute("TURN ON LAMP");
        if (withKeys) runner.Give("KEYS");
        runner.Teleport("MGRAT");
        return runner;
    }

    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    [Fact]
    public void Grating_puzzle_unlock_open_and_climb_out()
    {
        using var runner = InGratingRoom();

        Assert.Contains("The grating is locked.", Do(runner, "OPEN GRATING"));
        Assert.Contains("The grating is locked", Do(runner, "UP"));
        Assert.Equal("MGRAT", runner.State.Here.Id);

        Assert.Contains("The grate is unlocked.", Do(runner, "UNLOCK GRATING WITH KEYS"));
        Assert.True(runner.State.GetFlag("GRUNLOCK!-FLAG"));
        Assert.Contains("The grate is closed.", Do(runner, "UP"));
        Assert.Equal("MGRAT", runner.State.Here.Id);

        Assert.Contains("The grating opens to reveal trees above you.", Do(runner, "OPEN GRATING"));
        Assert.True(runner.State.World.FindObject("GRAT1")!.IsVisible);
        runner.Execute("UP");
        Assert.Equal("CLEAR", runner.State.Here.Id);
    }

    [Fact]
    public void Unlock_without_naming_the_tool_uses_the_only_tool_carried()
    {
        using var runner = InGratingRoom();

        Assert.Contains("The grate is unlocked.", Do(runner, "UNLOCK GRATING"));
    }

    [Fact]
    public void Unlock_without_any_tool_asks_with_what()
    {
        using var runner = InGratingRoom(withKeys: false);

        Assert.Contains("With what?", Do(runner, "UNLOCK GRATING"));
        Assert.False(runner.State.GetFlag("GRUNLOCK!-FLAG"));
    }

    [Fact]
    public void Unlock_with_the_wrong_tool_is_refused()
    {
        using var runner = InGratingRoom(withKeys: false);
        runner.Give("TORCH");

        Assert.Contains("Can you unlock a grating with a torch?", Do(runner, "UNLOCK GRATING WITH TORCH"));
        Assert.False(runner.State.GetFlag("GRUNLOCK!-FLAG"));
    }

    [Fact]
    public void Unlock_anything_but_the_grating_does_not_work()
    {
        using var runner = new GameRunner();
        runner.Give("KEYS");

        Assert.Contains("It doesn't seem to work.", Do(runner, "UNLOCK DOOR WITH KEYS"));
        Assert.Contains("It doesn't seem to work.", Do(runner, "LOCK DOOR"));
    }

    [Fact]
    public void Lock_locks_the_grating_again()
    {
        using var runner = InGratingRoom();
        runner.Execute("UNLOCK GRATING WITH KEYS");

        Assert.Contains("The grate is locked.", Do(runner, "LOCK GRATING"));
        Assert.False(runner.State.GetFlag("GRUNLOCK!-FLAG"));
        Assert.Contains("The grating is locked.", Do(runner, "OPEN GRATING"));
        Assert.Contains("The grate is locked.", Do(runner, "UP"));
    }

    [Fact]
    public void Closing_the_open_grating_blocks_the_way_up()
    {
        using var runner = InGratingRoom();
        runner.Run("UNLOCK GRATING WITH KEYS", "OPEN GRATING");

        Assert.Contains("The grating is closed.", Do(runner, "CLOSE GRATING"));
        Assert.False(runner.State.GetFlag("KEY-FLAG!-FLAG"));
        runner.Execute("UP");
        Assert.Equal("MGRAT", runner.State.Here.Id);
    }

    [Fact]
    public void Opening_an_open_grating_gives_a_dummy_reply()
    {
        using var runner = InGratingRoom();
        runner.Run("UNLOCK GRATING WITH KEYS", "OPEN GRATING");

        var reply = Do(runner, "OPEN GRATING");
        Assert.Contains(DummyReplies, d => reply.Contains(d, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("UNLOCK", "Unlock what?")]
    [InlineData("LOCK", "Lock what?")]
    public void Unlock_and_lock_without_an_object_ask_what(string command, string reply)
    {
        using var runner = new GameRunner();

        Assert.Contains(reply, Do(runner, command));
    }

    [Theory]
    [InlineData("MOVE LEAVES", "Done.")]
    [InlineData("TAKE LEAVES", "Taken.")]
    [InlineData("BURN LEAVES", "The leaves burn and the neighbors start to complain.")]
    public void Disturbing_the_leaves_reveals_the_grating(string command, string reply)
    {
        // act1.38 CLEARING: after TAKE, MOVE or BURN on the leaves, the grating appears (once).
        using var runner = new GameRunner();
        runner.Give("TORCH"); // the flame for BURN
        runner.Teleport("CLEAR");
        var grating = runner.State.World.FindObject("GRAT1")!;
        Assert.False(grating.IsVisible);

        var output = Do(runner, command);

        Assert.Contains(reply, output);
        Assert.Contains("A grating appears on the ground.", output);
        Assert.True(grating.IsVisible);
        Assert.Contains("There is a grating securely fastened into the ground.", Do(runner, "LOOK"));
        Assert.DoesNotContain("A grating appears", Do(runner, "DROP LEAVES"));
    }
}
