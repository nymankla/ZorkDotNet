using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// Moving around as in rooms.99 WALK, and exits with their own conditions: the riddle (act1.38 WELL), the studio
/// chimney (CHIMNEY-FUNCTION), the round room (CAROUSEL-EXIT, CAROUSEL-OUT), the dome rope and the rainbow.
/// </summary>
public class ExitTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    /// <summary>A runner with a lit lamp, so rooms are described and WALK-IN actions run.</summary>
    private static GameRunner WithLight(string roomId)
    {
        var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport(roomId);
        return runner;
    }

    [Fact]
    public void A_direction_with_no_exit_has_the_original_message()
    {
        using var runner = new GameRunner();

        Assert.Contains("There is no way to go in that direction.", Do(runner, "UP"));
        Assert.Equal("WHOUS", runner.State.Here.Id);
    }

    [Fact]
    public void Saying_well_in_the_riddle_room_opens_the_east_door()
    {
        using var runner = WithLight("RIDDL");

        Assert.Contains("Your way is blocked by an invisible force.", Do(runner, "EAST"));
        Assert.Equal("RIDDL", runner.State.Here.Id);

        Assert.Contains("There is a clap of thunder and the east door opens.", Do(runner, "WELL"));
        Do(runner, "EAST");
        Assert.Equal("MPEAR", runner.State.Here.Id);
    }

    [Fact]
    public void Well_elsewhere_or_twice_is_just_a_word()
    {
        using (var outside = new GameRunner())
            Assert.Contains("Well what?", Do(outside, "WELL"));

        using var runner = WithLight("RIDDL");
        Do(runner, "WELL");
        Assert.Contains("Well what?", Do(runner, "WELL"));
    }

    [Fact]
    public void The_riddle_room_is_below_the_engravings_cave()
    {
        using var runner = WithLight("CAVE4");

        Do(runner, "SE");
        Assert.Equal("RIDDL", runner.State.Here.Id);
        Do(runner, "DOWN");
        Assert.Equal("CAVE4", runner.State.Here.Id);
    }

    [Fact]
    public void The_chimney_takes_you_up_with_the_lamp_and_little_else()
    {
        using var runner = WithLight("STUDI");
        runner.Give("SWORD");

        Do(runner, "UP");
        Assert.Equal("KITCH", runner.State.Here.Id);
    }

    [Fact]
    public void The_chimney_is_too_narrow_with_more_baggage_or_without_the_lamp()
    {
        using var runner = WithLight("STUDI");
        runner.Give("SWORD");
        runner.Give("ROPE");

        Assert.Contains("The chimney is too narrow for you and all of your baggage.", Do(runner, "UP"));
        Assert.Equal("STUDI", runner.State.Here.Id);
    }

    [Fact]
    public void While_the_carousel_spins_every_direction_is_random()
    {
        string[] compassRooms = ["CAVE4", "MGRAI", "PASS1", "CANY1", "PASS5", "PASS4", "MAZE1"];
        using var runner = WithLight("CAROU");

        Assert.Contains("Unfortunately, it is impossible to tell directions in here.", Do(runner, "NORTH"));
        Assert.Contains(runner.State.Here.Id, compassRooms);
    }

    [Fact]
    public void Exit_from_the_round_room_is_always_random()
    {
        string[] compassRooms = ["CAVE4", "MGRAI", "PASS1", "CANY1", "PASS5", "PASS4", "MAZE1"];
        using var runner = WithLight("CAROU");
        runner.State.SetFlag("CAROUSEL-FLIP!-FLAG", true);

        var output = Do(runner, "EXIT");

        Assert.DoesNotContain("impossible to tell directions", output);
        Assert.Contains(runner.State.Here.Id, compassRooms);
    }

    [Fact]
    public void Once_the_carousel_stops_directions_work()
    {
        using var runner = WithLight("CAROU");
        runner.State.SetFlag("CAROUSEL-FLIP!-FLAG", true);

        Do(runner, "EAST");
        Assert.Equal("MGRAI", runner.State.Here.Id);
    }

    [Fact]
    public void The_dome_can_be_descended_only_on_the_rope()
    {
        using var runner = WithLight("DOME");

        Assert.Contains("You cannot go down without fracturing many bones.", Do(runner, "DOWN"));
        runner.State.SetFlag("DOME-FLAG!-FLAG", true);
        Do(runner, "CLIMB");
        Assert.Equal("MTORC", runner.State.Here.Id);
        Assert.Contains("You cannot reach the rope.", Do(runner, "UP"));
    }

    [Fact]
    public void The_rainbow_can_be_climbed_from_the_end_of_the_rainbow_once_solid()
    {
        using var runner = WithLight("POG");

        Assert.Contains("There is no way to go in that direction.", Do(runner, "NORTH"));
        Assert.Equal("POG", runner.State.Here.Id);
        // A closed #CEXIT without a message of its own gets WALK's default.
        Assert.Contains("There is no way to go in that direction.", Do(runner, "WEST"));
        Assert.Equal("POG", runner.State.Here.Id);

        runner.State.SetFlag("RAINBOW!-FLAG", true);
        Do(runner, "UP");
        Assert.Equal("RAINB", runner.State.Here.Id);
    }
}
