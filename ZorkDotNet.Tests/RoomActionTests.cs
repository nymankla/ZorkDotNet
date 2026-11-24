using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// Room and object actions around the dungeon: the Loud Room (act1.38 ECHO-ROOM), dome and torch room, the grating
/// room (MAZE-11), the empty-handed passages (act2.27 NO-OBJS), the rusty knife, and the bat (FLY-ME).
/// </summary>
public class RoomActionTests
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
    public void The_loud_room_echoes_everything_until_you_say_echo()
    {
        using var runner = WithLight("PASS5");
        var entering = Do(runner, "NE");
        Assert.Equal("ECHO", runner.State.Here.Id);
        Assert.Contains("There is a large platinum bar here.", entering); // described before the noise takes over
        Assert.True(runner.State.EchoListening);

        Assert.Equal("> take bar" + Environment.NewLine + "take bar" + Environment.NewLine, Do(runner, "take bar"));
        Assert.DoesNotContain(runner.State.World!.FindObject("BAR")!, runner.State.Winner.Inventory);

        Assert.Contains("The acoustics of the room change subtly.", Do(runner, "echo"));
        Assert.False(runner.State.EchoListening);
        Assert.Contains("Taken.", Do(runner, "TAKE BAR"));
    }

    [Fact]
    public void Walking_out_of_the_loud_room_works_while_it_echoes()
    {
        using var runner = WithLight("PASS5");
        Do(runner, "NE");

        Do(runner, "EAST");

        Assert.Equal("CHAS3", runner.State.Here.Id);
        Assert.False(runner.State.EchoListening);
        Assert.True(runner.State.World!.FindObject("BAR")!.IsVisible);
    }

    [Fact]
    public void The_rope_shows_from_the_dome_and_the_torch_room()
    {
        using var runner = WithLight("DOME");
        runner.State.SetFlag("DOME-FLAG!-FLAG", true);

        Assert.Contains("Hanging down from the railing is a rope", Do(runner, "LOOK"));
        runner.Teleport("MTORC");
        Assert.Contains("A large piece of rope descends from the railing above", Do(runner, "LOOK"));
    }

    [Fact]
    public void The_grating_room_describes_the_grating()
    {
        using var runner = WithLight("MGRAT");

        Assert.Contains("Above you is a grating locked with a skull-and-crossbones lock.", Do(runner, "LOOK"));
        runner.State.SetFlag("GRUNLOCK!-FLAG", true);
        Assert.Contains("Above you is a grating.", Do(runner, "LOOK"));
    }

    [Fact]
    public void Only_the_empty_handed_fit_through_the_timber_passage()
    {
        using var runner = new GameRunner();
        runner.Teleport("TIMBE");
        runner.Give("SWORD");

        Assert.Contains("You cannot fit through this passage with that load.", Do(runner, "SW"));
        Do(runner, "DROP SWORD");
        Do(runner, "SW");
        Assert.Equal("BSHAF", runner.State.Here.Id);
    }

    [Fact]
    public void Lighting_the_bottom_of_the_shaft_scores_once()
    {
        using var runner = new GameRunner();
        runner.Teleport("BSHAF");
        var lamp = runner.State.World!.FindObject("LAMP")!;
        lamp.InRoom?.Objects.Remove(lamp);
        lamp.InRoom = runner.State.Here;
        runner.State.Here.Objects.Add(lamp);
        lamp.LightAmount = 1;
        var score = runner.State.Winner.Score;

        Do(runner, "LOOK");
        Do(runner, "LOOK");

        Assert.Equal(score + 10, runner.State.Winner.Score);
    }

    [Fact]
    public void The_rusty_knife_pulses_with_the_sword_and_turns_on_you()
    {
        using var runner = WithLight("MAZE5");
        runner.Give("SWORD");

        Assert.Contains("your sword gives a single pulse of blinding blue light", Do(runner, "TAKE RUSTY KNIFE"));
        runner.Teleport("MTROL");
        Assert.Contains("The knife seems to sing as it savagely slits your throat.", Do(runner, "ATTACK TROLL WITH RUSTY KNIFE"));
    }

    [Fact]
    public void Touching_the_bat_gets_you_carried_off()
    {
        using var runner = WithLight("BATS");
        runner.Give("GARLI");

        Assert.Contains("lifts you away....", Do(runner, "TAKE BAT"));
        Assert.NotEqual("BATS", runner.State.Here.Id);
    }
}
