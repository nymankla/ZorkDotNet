using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// The river trip: the boat (act2.27 IBOAT-, RBOAT-, DBOAT-FUNCTION), going on the water (rooms.99 GOTO, BOARD,
/// UNBOARD), the buoy and emerald (RIVR4-ROOM), the cliff path (CLIFF-FUNCTION) and digging (BEACH-ROOM, TCAVE-ROOM).
/// </summary>
public class RiverTests
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
    public void The_river_trip_from_the_dam_to_the_buoy_and_ashore()
    {
        using var runner = WithLight("DOCK");
        runner.Give("PUMP");

        Assert.Contains("You can't go there without a vehicle.", Do(runner, "LAUNCH"));
        Assert.Contains("The boat inflates and appears seaworthy.", Do(runner, "INFLATE BOAT WITH PUMP"));
        Assert.Contains("You are in the magic boat.", Do(runner, "BOARD BOAT"));
        Assert.Contains("You can't go there in a magic boat.", Do(runner, "NORTH"));

        Do(runner, "LAUNCH");
        Assert.Equal("RIVR1", runner.State.Here.Id);
        Assert.Contains("You cannot go upstream due to strong currents.", Do(runner, "UP"));
        runner.Run("DOWN", "DOWN", "DOWN");
        Assert.Equal("RIVR4", runner.State.Here.Id);

        Do(runner, "TAKE BUOY");
        Assert.Contains("Something seems funny about the feel of the buoy.", runner.GetOutput());
        Assert.Contains("Opening the red buoy reveals a large emerald.", Do(runner, "OPEN BUOY"));

        Do(runner, "WEST");
        Assert.Equal("BEACH", runner.State.Here.Id);
        Assert.Contains("You are on your own feet again.", Do(runner, "DISEMBARK"));
        Assert.Null(runner.State.Winner.Vehicle);
    }

    [Fact]
    public void Getting_out_on_the_river_drowns_you()
    {
        using var runner = WithLight("DOCK");
        runner.Give("PUMP");
        runner.Run("INFLATE BOAT", "BOARD BOAT", "LAUNCH");

        Assert.Contains("Unfortunately, that leaves you in the water, where you drown.", Do(runner, "DISEMBARK"));
    }

    [Fact]
    public void Without_a_pump_you_lack_the_lung_power()
    {
        using var runner = WithLight("DOCK");
        runner.Give("SWORD");

        Assert.Contains("You would inflate it with that?", Do(runner, "INFLATE BOAT WITH SWORD"));
        Assert.Equal("IBOAT", runner.State.Here.Objects.First(o => o.Synonyms.Contains("BOAT")).Id);
    }

    [Fact]
    public void Boarding_with_the_sharp_stick_punctures_the_boat_and_putty_repairs_it()
    {
        using var runner = WithLight("DOCK");
        runner.Give("PUMP");
        Do(runner, "INFLATE BOAT");
        Do(runner, "TAKE STICK");

        Assert.Contains("There is a hissing sound and the boat deflates.", Do(runner, "BOARD BOAT"));
        Assert.Null(runner.State.Winner.Vehicle);
        Assert.Contains(runner.State.Here.Objects, o => o.Id == "DBOAT");

        runner.Give("PUTTY");
        Assert.Contains("Well done.  The boat is repaired.", Do(runner, "PLUG BOAT WITH PUTTY"));
        Assert.Contains(runner.State.Here.Objects, o => o.Id == "IBOAT");
    }

    [Fact]
    public void The_boat_deflates_only_on_the_ground_and_with_you_out_of_it()
    {
        using var runner = WithLight("DOCK");
        runner.Give("PUMP");
        runner.Run("INFLATE BOAT", "BOARD BOAT");

        Assert.Contains("You can't deflate the boat while you're in it.", Do(runner, "DEFLATE BOAT"));
        Do(runner, "DISEMBARK");
        Assert.Contains("The boat deflates.", Do(runner, "DEFLATE BOAT"));
        Assert.Contains(runner.State.Here.Objects, o => o.Id == "IBOAT");
    }

    [Fact]
    public void The_cliff_path_is_too_narrow_with_an_inflated_boat()
    {
        using var runner = WithLight("WCLF1");
        runner.Give("RBOAT");

        Assert.Contains("The path is too narrow.", Do(runner, "SOUTH"));
        Do(runner, "DROP BOAT");
        Do(runner, "SOUTH");
        Assert.Equal("WCLF2", runner.State.Here.Id);
    }

    [Fact]
    public void Digging_on_the_beach_uncovers_the_statue_and_then_buries_you()
    {
        using var runner = WithLight("BEACH");
        runner.Give("SHOVE");

        Assert.Contains("You seem to be digging a hole here.", Do(runner, "DIG WITH SHOVEL"));
        Assert.Contains("The hole is getting deeper, but that's about it.", Do(runner, "DIG WITH SHOVEL"));
        Assert.Contains("You are surrounded by a wall of sand on all sides.", Do(runner, "DIG WITH SHOVEL"));
        Assert.Contains("You can see a small statue here in the sand.", Do(runner, "DIG WITH SHOVEL"));
        Assert.Contains("There is a beautiful statue here.", Do(runner, "LOOK"));
        Assert.Contains("The hole collapses, smothering you.", Do(runner, "DIG WITH SHOVEL"));
    }

    [Fact]
    public void Digging_in_the_guano_gets_you_nowhere()
    {
        using var runner = WithLight("TCAVE");
        Do(runner, "TAKE SHOVEL");

        Assert.Contains("You are digging into a pile of bat guano.", Do(runner, "DIG WITH SHOVEL"));
        Assert.Contains("You seem to be getting knee deep in guano.", Do(runner, "DIG WITH SHOVEL"));
        Assert.Contains("You are covered with bat turds, cretin.", Do(runner, "DIG WITH SHOVEL"));
        Assert.Contains("This is getting you nowhere.", Do(runner, "DIG WITH SHOVEL"));
    }

    [Fact]
    public void The_label_tells_how_to_use_the_boat()
    {
        using var runner = WithLight("DOCK");
        runner.Give("PUMP");
        Do(runner, "INFLATE BOAT");
        Do(runner, "TAKE LABEL");

        Assert.Contains("FROBOZZ MAGIC BOAT COMPANY", Do(runner, "READ LABEL"));
    }
}
