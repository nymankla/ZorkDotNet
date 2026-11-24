using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>THROW (rooms.99 DROPPER) and the glacier (act1.38 GLACIER, GLACIER-ROOM).</summary>
public class ThrowTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    [Fact]
    public void A_thrown_object_lands_in_the_room()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        var sword = runner.Give("SWORD");

        Assert.Contains("Thrown.", Do(runner, "THROW SWORD"));
        Assert.Same(runner.State.Here, sword.InRoom);
    }

    [Fact]
    public void The_torch_melts_the_glacier_and_is_carried_downstream()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        var torch = runner.Give("TORCH");
        runner.Teleport("ICY");

        Assert.Contains("The torch hits the glacier and explodes into a great ball of flame", Do(runner, "THROW TORCH AT GLACIER"));
        Assert.True(runner.State.GetFlag("GLACIER-FLAG!-FLAG"));
        Assert.Null(runner.State.World.FindObject("ICE")!.Location);
        Assert.Equal("STREA", torch.InRoom?.Id);
        Assert.Equal("burned out ivory torch", torch.DescriptionShort);
        Assert.Contains("There is a large passageway leading westward.", Do(runner, "LOOK"));
        Do(runner, "WEST");
        Assert.Equal("RUBYR", runner.State.Here.Id);
    }

    [Fact]
    public void Melting_the_glacier_with_your_only_light_leaves_you_in_the_dark()
    {
        using var runner = new GameRunner();
        runner.Give("TORCH");
        runner.Teleport("ICY");

        Assert.Contains("The melting glacier seems to have carried the torch away, leaving you in the dark.", Do(runner, "THROW TORCH AT GLACIER"));
    }

    [Fact]
    public void Anything_else_leaves_the_glacier_unmoved()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        var sword = runner.Give("SWORD");
        runner.Teleport("ICY");

        var output = Do(runner, "THROW SWORD AT GLACIER");

        Assert.Contains("The glacier is unmoved by your ridiculous attempt." + Environment.NewLine + "Thrown.", output);
        Assert.Same(runner.State.Here, sword.InRoom);
        Assert.Contains("How exactly are you going to melt this glacier?", Do(runner, "MELT GLACIER"));
    }

    [Fact]
    public void The_trap_door_cannot_be_found_before_the_rug_is_moved()
    {
        using var runner = new GameRunner();
        runner.Teleport("LROOM");

        Assert.DoesNotContain("The door reluctantly opens", Do(runner, "OPEN TRAP DOOR"));
        Assert.False(runner.State.GetFlag("TRAP-DOOR!-FLAG"));

        Assert.Contains("the dusty cover of a closed trap-door appears", Do(runner, "MOVE RUG"));
        Assert.Contains("The door reluctantly opens", Do(runner, "OPEN TRAP DOOR"));
        Assert.True(runner.State.GetFlag("TRAP-DOOR!-FLAG"));
    }
}
