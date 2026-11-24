using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// The balloon in the volcano (act2.27 BALLOON, RISE-AND-SHINE, DECLINE-AND-FALL, WIRE-FUNCTION, BURNUP, VOLGNOME,
/// GNOME-FUNCTION, LEDGE-FUNCTION) and the air rooms (rooms.99 GOTO with RAIRBIT).
/// </summary>
public class BalloonTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    /// <summary>In the basket at the bottom of the volcano, with a book burning in the open receptacle.</summary>
    private static GameRunner Aloft()
    {
        var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Give("GUIDE");
        runner.Give("TORCH"); // the flame for BURN
        runner.Teleport("VLBOT");
        runner.Run("BOARD BASKET", "OPEN RECEPTACLE", "PUT GUIDEBOOK IN RECEPTACLE");
        return runner;
    }

    /// <summary>Plain moves until the balloon's clock has done something (it runs every three).</summary>
    private static string NextTick(GameRunner runner)
    {
        var output = "";
        for (var i = 0; i < 3; i++) output += Do(runner, "LOOK");
        return output;
    }

    [Fact]
    public void Burning_something_in_the_receptacle_inflates_the_balloon_and_it_rises()
    {
        using var runner = Aloft();

        var burn = Do(runner, "BURN GUIDEBOOK");
        Assert.Contains("The tour guidebook burns inside the receptacle.", burn);
        Assert.Contains("The cloth bag inflates as it fills with hot air.", burn);
        Assert.Contains("The cloth bag is inflated and there is a tour guidebook burning in the receptacle.", Do(runner, "LOOK"));

        Assert.Contains("The balloon rises slowly from the ground.", NextTick(runner));
        Assert.Equal("VAIR1", runner.State.Here.Id);
        Assert.Contains("The balloon ascends.", NextTick(runner));
        Assert.Equal("VAIR2", runner.State.Here.Id);
    }

    [Fact]
    public void Landing_on_the_narrow_ledge_and_tying_up()
    {
        using var runner = Aloft();
        Do(runner, "BURN GUIDEBOOK");
        NextTick(runner);
        NextTick(runner);
        Assert.Equal("VAIR2", runner.State.Here.Id);

        Do(runner, "LAND");
        Assert.Equal("LEDG2", runner.State.Here.Id);
        Assert.Contains("The balloon is fastened to the hook.", Do(runner, "TIE WIRE TO HOOK"));
        Assert.Contains("You are tied to the ledge.", Do(runner, "LAUNCH"));

        NextTick(runner);
        Assert.Equal("LEDG2", runner.State.Here.Id); // tied: the clock is stopped
        Assert.Contains("The wire falls off of the hook.", Do(runner, "UNTIE WIRE"));
        Assert.Contains("The wire is not tied to anything.", Do(runner, "UNTIE WIRE"));
    }

    [Fact]
    public void Rising_past_the_rim_wrecks_the_balloon()
    {
        using var runner = Aloft();
        Do(runner, "BURN GUIDEBOOK");
        for (var i = 0; i < 4; i++) NextTick(runner);
        Assert.Equal("VAIR4", runner.State.Here.Id);

        Assert.Contains("Your balloon has hit the rim of the volcano", NextTick(runner));
        Assert.Equal("VLBOT", runner.State.World!.FindObject("DBALL")!.InRoom?.Id);
    }

    [Fact]
    public void The_balloon_can_only_go_where_balloons_go()
    {
        using var runner = Aloft();

        Assert.Contains("You can't go there in a basket.", Do(runner, "NORTH"));
        Do(runner, "BURN GUIDEBOOK");
        NextTick(runner);
        Assert.Contains("I'm afraid you can't control the balloon in this way.", Do(runner, "NORTH"));
        Assert.Contains("You realize, just in time, that disembarking here would probably be fatal.", Do(runner, "DISEMBARK"));
    }

    [Fact]
    public void Without_the_balloon_the_air_cannot_be_entered()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport("LEDG2");

        Assert.Contains("You can't go there without a vehicle.", Do(runner, "LAUNCH"));
        Assert.Contains("I wouldn't jump from here.", Do(runner, "DOWN"));
    }

    [Fact]
    public void With_the_receptacle_closed_the_balloon_sinks_and_lands()
    {
        using var runner = Aloft();
        Do(runner, "BURN GUIDEBOOK");
        NextTick(runner);
        NextTick(runner);
        Assert.Equal("VAIR2", runner.State.Here.Id);

        Do(runner, "CLOSE RECEPTACLE");
        Assert.Contains("The balloon descends.", NextTick(runner));
        Assert.Contains("The balloon has landed.", NextTick(runner));
        Assert.Equal("VLBOT", runner.State.Here.Id);
    }

    [Fact]
    public void A_deflated_balloon_does_not_survive_landing()
    {
        using var runner = Aloft();
        Do(runner, "BURN GUIDEBOOK");
        NextTick(runner);
        runner.State.SetFlag("BINF!-FLAG", false); // the fuel is spent

        Assert.Contains("You have landed, but the balloon did not survive.", NextTick(runner));
        Assert.Null(runner.State.Winner.Vehicle);
        Assert.Equal("VLBOT", runner.State.World!.FindObject("DBALL")!.InRoom?.Id);
    }

    [Fact]
    public void The_fuel_burns_out()
    {
        using var runner = Aloft();
        runner.Give("CARD"); // size 1: burns for 20 moves
        runner.Run("TAKE GUIDEBOOK", "PUT CARD IN RECEPTACLE", "BURN CARD", "CLOSE RECEPTACLE"); // closed: it stays down
        var output = "";
        for (var i = 0; i < 20; i++) output += Do(runner, "LOOK");

        Assert.Contains("It seems that the card has burned out, and the cloth bag starts to collapse.", output);
        Assert.False(runner.State.GetFlag("BINF!-FLAG"));
    }

    [Fact]
    public void The_gnome_shows_the_way_out_for_a_treasure()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Give("ZORKM");
        runner.Teleport("LEDG2");
        runner.State.Schedule(0, ZorkDotNet.Game.Clocks.Volgnome);

        Assert.Contains("A volcano gnome seems to walk straight out of the wall", Do(runner, "LOOK"));
        Assert.Contains("a door appears on the west end of the ledge", Do(runner, "GIVE ZORKMID TO GNOME"));
        Do(runner, "WEST");
        Assert.Equal("VLBOT", runner.State.Here.Id);
    }

    [Fact]
    public void The_gnome_crunches_worthless_gifts_and_leaves_when_nervous()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Give("ADVER");
        runner.Teleport("LEDG4");
        runner.State.Schedule(0, ZorkDotNet.Game.Clocks.Volgnome);
        Do(runner, "LOOK");

        Assert.Contains("'That wasn't quite what I had in mind'", Do(runner, "GIVE LEAFLET TO GNOME"));
        Assert.Contains("The gnome appears increasingly nervous.", Do(runner, "EXAMINE GNOME"));
        var later = "";
        for (var i = 0; i < 6; i++) later += Do(runner, "LOOK");
        Assert.Contains("'Oops.  I'm late for an appointment!'", later);
    }

    [Fact]
    public void The_wide_ledge_shows_the_way_south()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport("LEDG4");

        Assert.Contains("There is a small door to the south.", Do(runner, "LOOK"));
    }

    [Fact]
    public void The_library_has_the_stamp_in_the_purple_book()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport("LIBRA");

        Assert.Contains("Opening the purple book reveals a stamp.", Do(runner, "OPEN PURPLE BOOK"));
        Assert.Contains("This book is written in a tongue with which I am unfamiliar.", Do(runner, "READ GREEN BOOK"));
    }
}
