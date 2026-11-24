using Xunit;
using ZorkDotNet.Game;

namespace ZorkDotNet.Tests;

/// <summary>
/// Light (rooms.99 LIT? / LFCN, LAMP-ON / LAMP-OFF) and the lamp's burn clock (act1.38 LANTERN, LIGHT-INT,
/// LAMP-TICKS, dung.56 CLOCK-INT LNTIN 350). OLIGHT? -1 = off, 1 = on, 0 = can't be lit.
/// </summary>
public class LightingTests
{
    private const string Dimmer = "The lamp appears to be getting dimmer.";

    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    private static int Count(string text, string part) =>
        (text.Length - text.Replace(part, "", StringComparison.Ordinal).Length) / part.Length;

    [Fact]
    public void An_unlit_lamp_gives_no_light()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP");
        runner.Teleport("CELLA");

        Assert.False(GameState.IsLit(runner.State));
        Assert.Contains("It is pitch black.  You are likely to be eaten by a grue.", Do(runner, "LOOK"));
    }

    [Fact]
    public void Turning_the_lamp_on_in_the_dark_lights_and_describes_the_room()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP");
        runner.Teleport("CELLA");

        var output = Do(runner, "TURN ON LAMP");

        Assert.Contains("The lamp is now on.", output);
        Assert.Contains("You are in a dark and damp cellar", output);
        Assert.True(GameState.IsLit(runner.State));
        Assert.Contains("It is already on.", Do(runner, "TURN ON LAMP"));
    }

    [Fact]
    public void Turning_the_lamp_off_in_the_dark_makes_it_pitch_black()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP");
        runner.Teleport("CELLA");
        runner.Execute("TURN ON LAMP");

        var output = Do(runner, "TURN OFF LAMP");

        Assert.Contains("The lamp is now off.", output);
        Assert.Contains("It is now pitch black.", output);
        Assert.Equal(-1, runner.State.World!.FindObject("LAMP")!.LightAmount);
        Assert.Contains("It is already off.", Do(runner, "TURN OFF LAMP"));
    }

    [Fact]
    public void A_lamp_on_the_floor_is_taken_before_it_is_turned_on()
    {
        // TURN ON's slot is AOBJS ROBJS without NO-TAKE, so np.93 TAKE-IT-OR-LEAVE-IT takes the lamp first.
        using var runner = new GameRunner();
        runner.Teleport("LROOM");

        var output = Do(runner, "TURN ON LAMP");

        Assert.Contains("Taken.", output);
        Assert.Contains("The lamp is now on.", output);
        Assert.Contains(runner.State.World!.FindObject("LAMP")!, runner.State.Winner.Inventory);
    }

    [Fact]
    public void Something_that_gives_no_light_cannot_be_turned_on_or_off()
    {
        using var runner = new GameRunner();
        runner.Give("SWORD");

        Assert.Contains("You can't turn that on.", Do(runner, "TURN ON SWORD"));
        Assert.Contains("You can't turn that off.", Do(runner, "TURN OFF SWORD"));
    }

    [Fact]
    public void A_light_inside_an_open_or_transparent_container_lights_the_room()
    {
        using var runner = new GameRunner();
        runner.Teleport("CELLA");
        var bottle = runner.State.World!.FindObject("BOTTL")!;
        var lamp = runner.State.World.FindObject("LAMP")!;
        bottle.MoveTo(runner.State.Here);
        lamp.MoveTo(bottle);

        Assert.False(GameState.IsLit(runner.State));
        lamp.LightAmount = 1;
        Assert.True(GameState.IsLit(runner.State));
    }

    [Fact]
    public void The_lamp_burns_for_464_moves_with_the_original_warnings()
    {
        using var runner = new GameRunner();
        var lamp = runner.Give("LAMP");
        var mark = runner.GetOutput().Length;
        runner.Execute("TURN ON LAMP"); // this turn already counts on the clock

        for (var i = 0; i < 462; i++) runner.Execute("LOOK");
        Assert.Equal(1, lamp.LightAmount);

        runner.Execute("LOOK");
        var output = runner.GetOutputSince(mark);
        Assert.Equal(0, lamp.LightAmount);
        Assert.Equal(4, Count(output, Dimmer));
        Assert.Equal(1, Count(output, "The lamp is dying."));
        Assert.EndsWith("I hope you have more light than from a lamp." + Environment.NewLine, output);
        Assert.Contains("You can't turn that on.", Do(runner, "TURN ON LAMP"));
    }

    [Fact]
    public void The_first_warning_comes_after_350_moves_lit()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP");
        runner.Execute("TURN ON LAMP");
        for (var i = 0; i < 348; i++) runner.Execute("LOOK");
        Assert.DoesNotContain(Dimmer, runner.GetOutput());

        Assert.Contains(Dimmer, Do(runner, "LOOK"));
        Assert.Equal(50, runner.State.FindClock(Clocks.Lantern)!.Ticks);
    }

    [Fact]
    public void The_lamp_clock_stops_while_the_lamp_is_off()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP");
        runner.Run("TURN ON LAMP", "LOOK", "TURN OFF LAMP");
        var ticks = runner.State.FindClock(Clocks.Lantern)!.Ticks;

        runner.Run("LOOK", "LOOK", "LOOK");

        Assert.Equal(ticks, runner.State.FindClock(Clocks.Lantern)!.Ticks);
    }

    [Fact]
    public void A_lit_lamp_keeps_burning_when_put_down()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP");
        runner.Run("TURN ON LAMP", "DROP LAMP");
        var ticks = runner.State.FindClock(Clocks.Lantern)!.Ticks;

        runner.Run("LOOK", "LOOK");

        Assert.Equal(ticks - 2, runner.State.FindClock(Clocks.Lantern)!.Ticks);
    }

    [Fact]
    public void Putting_the_candles_out_stops_their_clock_until_they_are_lit_again()
    {
        // act1.38 CANDLES: TURN-OFF does CLOCK-DISABLE, LIGHT with a burning match CLOCK-ENABLE.
        using var runner = new GameRunner();
        runner.Teleport("TEMP2");
        runner.Execute("TAKE CANDLES");
        var clock = runner.State.FindClock(Clocks.Candles)!;
        Assert.True(clock.Enabled);

        Assert.Contains("The flame is extinguished.", Do(runner, "EXTINGUISH CANDLES"));
        var ticks = clock.Ticks;
        runner.Run("LOOK", "LOOK");
        Assert.Equal(ticks, clock.Ticks);

        runner.Give("MATCH");
        runner.Execute("LIGHT MATCH");
        Assert.Contains("The candles are lighted.", Do(runner, "LIGHT CANDLES WITH MATCH"));
        Assert.True(clock.Enabled);
    }
}
