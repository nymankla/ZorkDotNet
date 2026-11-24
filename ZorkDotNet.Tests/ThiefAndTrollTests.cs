using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// The thief (act1.38 ROBBER, ROBBER-FUNCTION, CHALICE, TREASURE-ROOM) and the troll (TROLL). Fights use the port's
/// melee (<c>Combat</c>), since the original's melee code is not in the MIT archive.
/// </summary>
public class ThiefAndTrollTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    /// <summary>A lit player in <paramref name="roomId"/>, with the thief's demon on and working that same room.</summary>
    private static GameRunner WithThief(string roomId, int random)
    {
        var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport(roomId);
        runner.State.ThiefActive = true;
        runner.State.ThiefRoom = roomId;
        runner.State.Random = new FixedRandom(random);
        return runner;
    }

    [Fact]
    public void The_thief_shows_himself_leaning_against_the_wall()
    {
        using var runner = WithThief("MAZE5", 0); // PROB 30 succeeds

        Assert.Contains("Someone carrying a large bag is casually leaning against one of the walls here.", Do(runner, "LOOK"));
        Assert.Equal("MAZE5", runner.State.World!.FindObject("THIEF")!.InRoom?.Id);
    }

    [Fact]
    public void The_thief_wanders_through_and_robs_you_of_your_treasures()
    {
        using var runner = WithThief("MAZE5", 80); // PROB 30 and PROB 70 fail: he robs
        var pearl = runner.Give("PEARL");

        Assert.Contains("he quietly abstracted all valuables from the room and from your possession", Do(runner, "LOOK"));
        Assert.DoesNotContain(pearl, runner.State.Winner.Inventory);
        Assert.Contains("PEARL", runner.State.ThiefBag);
    }

    [Fact]
    public void Finding_nothing_he_leaves_disgruntled()
    {
        using var runner = WithThief("MAZE1", 80); // nothing of value there

        Assert.Contains("A 'lean and hungry' gentleman just wandered through.  Finding nothing of value, he left disgruntled.", Do(runner, "LOOK"));
    }

    [Fact]
    public void Treasures_in_his_bag_end_up_in_his_hideaway()
    {
        using var runner = WithThief("MAZE5", 99);
        runner.State.ThiefRoom = "TREAS";
        runner.State.ThiefBag.Add("PEARL");

        Do(runner, "LOOK");

        Assert.Equal("TREAS", runner.State.World!.FindObject("PEARL")!.InRoom?.Id);
        Assert.Empty(runner.State.ThiefBag);
    }

    [Fact]
    public void The_thief_defends_his_hideaway_and_guards_the_chalice_until_he_dies()
    {
        using var runner = WithThief("CYCLO", 0);
        runner.State.ThiefRoom = "MAZE1";
        runner.State.SetFlag("CYCLOPS-FLAG!-FLAG", true);
        runner.Give("SWORD");

        Assert.Contains("You hear a scream of anguish as you violate the robber's hideaway.", Do(runner, "UP"));
        Assert.Equal("TREAS", runner.State.Here.Id);
        Assert.Contains("Realizing just in time that you'd be stabbed in the back", Do(runner, "TAKE CHALICE"));

        var fight = "";
        for (var i = 0; i < 3 && runner.State.ThiefActive; i++) fight += Do(runner, "ATTACK THIEF WITH SWORD");
        Assert.Contains("The thief is killed by the force of your attack.", fight);
        Assert.False(runner.State.ThiefActive);
        Assert.Contains("Taken.", Do(runner, "TAKE CHALICE"));
    }

    [Fact]
    public void The_thief_bags_gifts_and_cannot_be_taken()
    {
        using var runner = WithThief("MAZE5", 0);
        Do(runner, "LOOK"); // he appears
        runner.State.ThiefActive = false;
        runner.Give("SWORD");

        Assert.Contains("The thief places the sword in his bag and thanks you politely.", Do(runner, "GIVE SWORD TO THIEF"));
        Assert.Contains("SWORD", runner.State.ThiefBag);
        Assert.Contains("Once you got him, what would you do with him?", Do(runner, "TAKE THIEF"));
    }

    [Fact]
    public void Swinging_a_weapon_at_a_villain_attacks_him_with_it()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Give("SWORD");
        runner.Teleport("MTROL");

        Do(runner, "SWING SWORD AT TROLL");

        Assert.True(runner.State.World!.FindObject("TROLL")!.Fighting);
        Assert.Same(runner.State.World.FindObject("SWORD"), runner.State.LastIndirectObject);
    }

    [Fact]
    public void Attacking_with_the_rusty_knife_turns_it_on_you()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Give("RKNIF");
        runner.Teleport("MTROL");

        Assert.Contains("The knife seems to sing as it savagely slits your throat.", Do(runner, "ATTACK TROLL WITH RUSTY KNIFE"));
    }

    [Fact]
    public void Wounds_heal_one_at_a_time_with_the_cure_clock()
    {
        using var runner = new GameRunner();
        runner.State.Winner.Strength = 40;

        for (var i = 0; i < 30; i++) runner.Execute("WAIT");
        var once = runner.State.Winner.Strength;
        for (var i = 0; i < 300; i++) runner.Execute("WAIT");

        Assert.InRange(once, 60, 80); // one or two wounds, depending on where the 30-move cycle falls
        Assert.Equal(100, runner.State.Winner.Strength); // never above full strength
    }

    [Fact]
    public void The_troll_eats_gifts_and_throws_the_knife_back()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Give("SWORD");
        runner.Give("KNIFE");
        runner.Teleport("MTROL");

        var eaten = Do(runner, "GIVE SWORD TO TROLL");
        Assert.Contains("The troll, who is not overly proud, graciously accepts the gift", eaten);
        Assert.Contains("and not having the most discriminating tastes, gleefully eats it.", eaten);
        Assert.Contains("and being for the moment sated, throws it back.", Do(runner, "THROW KNIFE AT TROLL"));
        Assert.True(runner.State.World!.FindObject("TROLL")!.Fighting);
        Assert.Contains("The troll spits in your face, saying \"Better luck next time.\"", Do(runner, "TAKE TROLL"));
    }
}
