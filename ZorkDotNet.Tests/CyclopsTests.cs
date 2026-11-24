using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>The cyclops (act1.38 CYCLOPS, CYCLOPS-ROOM, AOS-SOS, CYCLOMAD, SINBAD).</summary>
public class CyclopsTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    private static GameRunner InCyclopsRoom()
    {
        var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport("CYCLO");
        return runner;
    }

    [Fact]
    public void The_cyclops_blocks_the_staircase()
    {
        using var runner = InCyclopsRoom();

        Assert.Contains("A cyclops, who looks prepared to eat horses", Do(runner, "LOOK"));
        Assert.Contains("The cyclops doesn't look like he'll let you past.", Do(runner, "UP"));
        Assert.Equal("CYCLO", runner.State.Here.Id);
    }

    [Fact]
    public void Peppers_make_him_thirsty_and_water_puts_him_to_sleep()
    {
        using var runner = InCyclopsRoom();
        runner.Give("FOOD");
        runner.Give("BOTTL");

        Assert.Contains("I love hot peppers!", Do(runner, "GIVE FOOD TO CYCLOPS"));
        Assert.True(runner.State.Here.RVars < 0);
        Assert.Contains("appears to be gasping", Do(runner, "LOOK"));
        Assert.Contains("I can't reach that.", Do(runner, "GIVE WATER TO CYCLOPS")); // the bottle is closed
        Do(runner, "OPEN BOTTLE");
        Assert.Contains("falls fast asleep", Do(runner, "GIVE WATER TO CYCLOPS"));
        Assert.True(runner.State.GetFlag("CYCLOPS-FLAG!-FLAG"));
        Assert.Contains("is sleeping blissfully at the foot of the stairs", Do(runner, "LOOK"));

        Do(runner, "UP");
        Assert.Equal("TREAS", runner.State.Here.Id);
    }

    [Fact]
    public void He_is_not_thirsty_before_the_peppers()
    {
        using var runner = InCyclopsRoom();
        runner.Give("BOTTL");
        Do(runner, "OPEN BOTTLE");

        Assert.Contains("was not thirsty at the time and refuses your generous gesture", Do(runner, "GIVE WATER TO CYCLOPS"));
        Assert.False(runner.State.GetFlag("CYCLOPS-FLAG!-FLAG"));
    }

    [Fact]
    public void Annoying_him_makes_him_angrier_until_he_eats_you()
    {
        using var runner = InCyclopsRoom();

        Assert.Contains("The cyclops is rather heavy and doesn't take kindly to being grabbed.", Do(runner, "TAKE CYCLOPS"));
        Assert.Contains("The cyclops seems somewhat agitated.", runner.GetOutput());
        Assert.Contains("The cyclops is standing in the corner, eyeing you closely.", Do(runner, "LOOK"));
        for (var i = 0; i < 4; i++) Do(runner, "TAKE CYCLOPS");
        Assert.Contains("You have two choices: 1. Leave  2. Become dinner.", Do(runner, "TAKE CYCLOPS"));

        Assert.Contains("The cyclops, tired of all of your games and trickery, eats you.", Do(runner, "TAKE CYCLOPS"));
    }

    [Fact]
    public void He_will_not_eat_garlic_or_other_things()
    {
        using var runner = InCyclopsRoom();
        runner.Give("GARLI");
        runner.Give("SWORD");

        var garlic = Do(runner, "GIVE GARLIC TO CYCLOPS");
        Assert.Contains("The cyclops may be hungry, but there is a limit.", garlic);
        Assert.Contains("The cyclops seems somewhat agitated.", garlic);
        Assert.Contains("The cyclops is not so stupid as to eat THAT!", Do(runner, "GIVE SWORD TO CYCLOPS"));
        Assert.Equal(2, runner.State.Here.RVars);
    }

    [Fact]
    public void Waking_him_makes_him_fight_again()
    {
        using var runner = InCyclopsRoom();
        runner.State.SetFlag("CYCLOPS-FLAG!-FLAG", true);
        runner.State.Here.RVars = -2;

        Assert.Contains("The cyclops yawns and stares at the thing that woke him up.", Do(runner, "WAKE CYCLOPS"));
        Assert.False(runner.State.GetFlag("CYCLOPS-FLAG!-FLAG"));
        Assert.Equal(2, runner.State.Here.RVars);
        Assert.True(runner.State.World.FindObject("CYCLO")!.Fighting);
    }

    [Fact]
    public void Odysseus_scares_him_through_the_north_wall()
    {
        using var runner = InCyclopsRoom();

        Assert.Contains("flees the room by knocking down the wall", Do(runner, "ODYSSEUS"));
        Assert.Contains("now has a cyclops-sized hole in it", Do(runner, "LOOK"));
        Do(runner, "NORTH");
        Assert.Equal("BLROO", runner.State.Here.Id);
    }
}
