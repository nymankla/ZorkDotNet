using Xunit;
using ZorkDotNet.Game;

namespace ZorkDotNet.Tests;

/// <summary>
/// An object's place (<see cref="Place"/>) and the lists of the rooms, adventurers and containers that hold it always
/// agree, because <see cref="GameObject.MoveTo"/> is the only way to change either.
/// </summary>
public sealed class LocationTests : IDisposable
{
    private readonly string _savePath = Path.Combine(Path.GetTempPath(), "zork-loc-" + Guid.NewGuid().ToString("N") + ".sav");

    public void Dispose()
    {
        if (File.Exists(_savePath)) File.Delete(_savePath);
    }

    /// <summary>Every object is listed by its place, and every listing is the object's place or shared scenery.</summary>
    private static void AssertConsistent(GameState state)
    {
        var world = state.World;
        foreach (var o in world.Objects)
        {
            switch (o.Location)
            {
                case Room r: Assert.Contains(o, r.Objects); break;
                case Adventurer a: Assert.Contains(o, a.Inventory); break;
                case GameObject c when o.Id != "BLABE": Assert.Contains(o, c.Contents); break;
            }
        }
        foreach (var r in world.Rooms)
            foreach (var o in r.Objects)
                Assert.True(o.InRoom == r || (o.InRoom != null && !o.CanTake), o.Id + " listed in " + r.Id + " but is elsewhere");
        foreach (var o in world.Objects)
            foreach (var inner in o.Contents)
                Assert.Same(o, inner.Container);
        foreach (var a in new[] { state.Winner, state.Robot })
            foreach (var o in a.Inventory)
                Assert.Same(a, o.Carrier);
    }

    [Fact]
    public void The_world_starts_consistent()
    {
        using var runner = new GameRunner();
        AssertConsistent(runner.State);
    }

    [Fact]
    public void MoveTo_leaves_the_old_place_and_joins_the_new_one()
    {
        using var runner = new GameRunner();
        var world = runner.State.World;
        var sword = world.FindObject("SWORD")!;
        var lroom = sword.InRoom!;
        var bottle = world.FindObject("BOTTL")!;

        sword.MoveTo(runner.State.Winner);
        Assert.DoesNotContain(sword, lroom.Objects);
        Assert.Same(runner.State.Winner, sword.Carrier);
        Assert.Null(sword.InRoom);

        sword.MoveTo(bottle, first: true);
        Assert.DoesNotContain(sword, runner.State.Winner.Inventory);
        Assert.Same(sword, bottle.Contents[0]);

        sword.MoveTo(null);
        Assert.DoesNotContain(sword, bottle.Contents);
        Assert.Null(sword.Location);
    }

    [Fact]
    public void Holder_finds_the_adventurer_through_containers()
    {
        using var runner = new GameRunner();
        var bottle = runner.Give("BOTTL");
        var water = runner.State.World.FindObject("WATER")!;

        Assert.Same(bottle, water.Container);
        Assert.Same(runner.State.Winner, water.Holder);
        bottle.MoveTo(runner.State.Here);
        Assert.Null(water.Holder);
        Assert.Same(runner.State.Here, water.OutermostRoom);
    }

    [Fact]
    public void Scenery_stays_listed_in_every_room_it_belongs_to()
    {
        using var runner = new GameRunner();
        var world = runner.State.World;
        var rainbow = world.FindObject("RAINB")!;

        Assert.Contains(rainbow, world.FindRoom("FALLS")!.Objects);
        Assert.Contains(rainbow, world.FindRoom("POG")!.Objects);
        Assert.Equal("FALLS", rainbow.InRoom?.Id);
    }

    [Fact]
    public void A_played_and_restored_game_stays_consistent()
    {
        using (var game = new GameRunner())
        {
            game.State.SavePath = _savePath;
            game.Run("NORTH", "EAST", "OPEN WINDOW", "ENTER", "TAKE BOTTLE", "WEST", "TAKE LAMP", "TAKE SWORD", "PUT BOTTLE IN CASE");
            AssertConsistent(game.State);
            game.Execute("SAVE");
        }

        using var restored = new GameRunner();
        restored.State.SavePath = _savePath;
        restored.Execute("RESTORE");

        AssertConsistent(restored.State);
        Assert.Contains(restored.State.World.FindObject("SWORD")!, restored.State.Winner.Inventory);
    }
}
