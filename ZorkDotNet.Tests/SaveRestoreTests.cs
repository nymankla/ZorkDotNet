using System.Text.Json;
using Xunit;
using ZorkDotNet.Game;

namespace ZorkDotNet.Tests;

/// <summary>SAVE / RESTORE keep the whole game state (rooms.99 DO-SAVE / DO-RESTORE).</summary>
public sealed class SaveRestoreTests : IDisposable
{
    private readonly string _savePath = Path.Combine(Path.GetTempPath(), "zork-test-" + Guid.NewGuid().ToString("N") + ".sav");

    public void Dispose()
    {
        if (File.Exists(_savePath)) File.Delete(_savePath);
    }

    private GameRunner NewGame()
    {
        var runner = new GameRunner();
        runner.State.SavePath = _savePath;
        return runner;
    }

    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    /// <summary>Everything the save holds, as text, so two games can be compared exactly.</summary>
    private static string Snapshot(GameRunner runner) =>
        JsonSerializer.Serialize(GameSave.Capture(runner.State), SaveJsonContext.Default.SaveData);

    /// <summary>A game with moved objects, changed flags, an altered exit and a pending clock event.</summary>
    private GameRunner PlayedGame()
    {
        var runner = NewGame();
        runner.Run("SOUTH", "EAST", "OPEN WINDOW", "ENTER", "WEST", "TAKE LAMP", "MOVE RUG", "OPEN TRAP DOOR", "EAST", "DROP LAMP");
        runner.Give("KEYS");
        runner.Execute("TURN ON LAMP");
        runner.Teleport("MGRAT");
        runner.Execute("UNLOCK GRATING WITH KEYS");
        var match = runner.Give("MATCH");
        match.LightAmount = 1;
        runner.State.Schedule(5, Clocks.MatchOut, "MATCH");
        return runner;
    }

    [Fact]
    public void Restore_brings_back_exactly_the_saved_state()
    {
        using (var game = PlayedGame())
        {
            game.Execute("SAVE");
        }
        var saved = File.ReadAllText(_savePath);

        using var restored = NewGame();
        restored.Run("NORTH", "NORTH"); // a different game, to be overwritten
        restored.Execute("RESTORE");

        // Capturing the restored game must give the saved file back, field for field.
        Assert.Equal(saved, Snapshot(restored));
    }

    [Fact]
    public void Restore_puts_objects_back_where_they_were_left()
    {
        using (var game = NewGame())
        {
            game.Run("SOUTH", "EAST", "OPEN WINDOW", "ENTER", "WEST", "TAKE LAMP", "EAST", "DROP LAMP", "SAVE");
        }

        using var restored = NewGame();
        restored.Execute("RESTORE");

        var lamp = restored.State.World!.FindObject("LAMP")!;
        Assert.Equal("KITCH", lamp.InRoom?.Id);
        Assert.DoesNotContain(lamp, restored.State.World.FindRoom("LROOM")!.Objects);
        Assert.Equal("KITCH", restored.State.Here.Id);
        Assert.True(restored.State.GetFlag("KITCHEN-WINDOW!-FLAG"));
    }

    [Fact]
    public void Pending_clock_events_still_fire_after_restore()
    {
        using (var game = PlayedGame())
        {
            game.Execute("SAVE");
        }

        using var restored = NewGame();
        restored.Execute("RESTORE");
        restored.Run("LOOK", "LOOK", "LOOK", "LOOK", "LOOK");

        Assert.Contains("The match has burned out.", restored.GetOutput());
    }

    [Fact]
    public void Save_says_saving_shows_the_score_and_ends_the_game()
    {
        using var game = NewGame();
        var output = Do(game, "SAVE");

        Assert.Contains("Saving.", output);
        Assert.Contains("Your score is 0 [total of", output);
        Assert.False(game.State.Running);
        Assert.True(File.Exists(_savePath));
    }

    [Fact]
    public void Restore_says_restored_and_describes_the_room()
    {
        using (var game = NewGame())
        {
            game.Run("NORTH", "SAVE");
        }

        using var restored = NewGame();
        var output = Do(restored, "RESTORE");

        Assert.Contains("Restored.", output);
        Assert.Contains("north side of a white house", output);
        Assert.True(restored.State.Running);
    }

    [Fact]
    public void Restore_without_a_save_file_fails_and_changes_nothing()
    {
        using var game = NewGame();
        game.Execute("NORTH");
        var before = Snapshot(game);

        Assert.Contains("Restore failed.", Do(game, "RESTORE"));
        game.State.Winner.Moves--; // typing RESTORE is itself a move; everything else must be unchanged
        Assert.Equal(before, Snapshot(game));
    }

    [Theory]
    [InlineData("this is not a save file")]
    [InlineData("{\"Version\": 99}")]
    public void Restore_of_an_unreadable_file_fails(string content)
    {
        File.WriteAllText(_savePath, content);
        using var game = NewGame();

        Assert.Contains("Restore failed.", Do(game, "RESTORE"));
        Assert.Equal("WHOUS", game.State.Here.Id);
    }

    [Fact]
    public void Restore_of_a_save_naming_unknown_objects_fails_and_changes_nothing()
    {
        using (var game = NewGame())
        {
            game.Run("NORTH", "SAVE");
        }
        File.WriteAllText(_savePath, File.ReadAllText(_savePath).Replace("\"MAILB\"", "\"NOSUCHTHING\"", StringComparison.Ordinal));

        using var restored = NewGame();
        Assert.Contains("Restore failed.", Do(restored, "RESTORE"));
        Assert.Equal("WHOUS", restored.State.Here.Id);
        Assert.Equal("WHOUS", restored.State.World!.FindObject("MAILB")!.InRoom?.Id);
    }
}
