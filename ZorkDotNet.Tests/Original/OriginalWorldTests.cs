using System.Runtime.CompilerServices;
using Xunit;

namespace ZorkDotNet.Tests.Original;

/// <summary>
/// Checks the port's World and vocabulary against the original dung.56. Differences that aren't fixed yet are
/// listed in KnownDifferences.txt (see Original/README.md).
/// </summary>
public class OriginalWorldTests
{
    private const string UpdateVariable = "ZORK_UPDATE_KNOWN_DIFFERENCES";
    private static readonly string OutputDir = Path.Combine(AppContext.BaseDirectory, "Original");

    private static OriginalDungeon LoadOriginal() => OriginalDungeon.Load(Path.Combine(OutputDir, "dung.56"));

    [Fact]
    public void Reader_parses_the_whole_original_dungeon()
    {
        var original = LoadOriginal();

        Assert.Equal(144, original.Rooms.Count);
        Assert.Equal(112, original.Objects.Count(o => o.IsFullObject));
        Assert.Equal(19, original.Objects.Count(o => o.Kind == "AOBJECT"));
        Assert.Equal(24, original.Objects.Count(o => o.Kind == "SOBJECT"));
        Assert.Contains(original.Verbs, v => v.Name == "TAKE" && v.Words.Contains("CARRY"));
    }

    [Fact]
    public void Reader_reads_rooms_exits_and_objects_correctly()
    {
        var original = LoadOriginal();

        var falls = original.Rooms.Single(r => r.Id == "FALLS");
        Assert.Equal("Aragain Falls", falls.ShortDescription);
        Assert.Equal(new OriginalExit("FANTE", null, null), falls.Exits["NORTH"]);
        Assert.Equal(new OriginalExit("RAINB", null, "RAINBOW"), falls.Exits["EAST"]);

        var whous = original.Rooms.Single(r => r.Id == "WHOUS");
        Assert.True(whous.Light);
        Assert.Equal("The door is locked, and there is evidently no key.", whous.Exits["EAST"].Message);

        // Exit defined through a global (<PSETG CURRENT #NEXIT "...">).
        Assert.Equal("You cannot go upstream due to strong currents.", original.Rooms.Single(r => r.Id == "RIVR1").Exits["UP"].Message);

        var lamp = original.Objects.Single(o => o.Id == "LAMP");
        Assert.Equal("LANTERN", lamp.Action);
        Assert.Equal(-1, lamp.Light);
        Assert.Equal(15, lamp.Size);
        Assert.Contains("LANTE", lamp.Synonyms);

        var mailbox = original.Objects.Single(o => o.Id == "MAILB");
        Assert.Equal(OriginalDungeon.BigFix, mailbox.Size);
        Assert.Equal(["ADVER"], mailbox.Contents);
        Assert.Equal(["CONTBIT", "OVISON"], mailbox.Flags);
    }

    [Fact]
    public void World_matches_original_except_known_differences()
    {
        var actual = WorldComparer.Compare(LoadOriginal());

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            WriteKnownDifferences(actual);
            return;
        }

        var known = File.ReadAllLines(Path.Combine(OutputDir, "KnownDifferences.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);
        var added = actual.Where(d => !known.Contains(d)).ToList();
        var fixedOnes = known.Where(k => !actual.Contains(k)).Order(StringComparer.Ordinal).ToList();

        var message = "";
        if (added.Count > 0)
            message += $"{added.Count} new difference(s) from dung.56 (fix them, or if deliberate add them to Original/KnownDifferences.txt):\n  " + string.Join("\n  ", added) + "\n";
        if (fixedOnes.Count > 0)
            message += $"{fixedOnes.Count} known difference(s) no longer occur. Remove them from Original/KnownDifferences.txt:\n  " + string.Join("\n  ", fixedOnes) + "\n";
        if (message.Length > 0)
            message += $"To regenerate the file, run the tests with {UpdateVariable}=1 and review the diff.";
        Assert.True(message.Length == 0, message);
    }

    private static void WriteKnownDifferences(List<string> differences, [CallerFilePath] string sourceFile = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(sourceFile)!, "KnownDifferences.txt");
        var header = new[]
        {
            "# Differences between the port and the original dung.56 that are not fixed yet.",
            "# One per line; checked by OriginalWorldTests. Delete a line when you fix it.",
            "# Regenerate with ZORK_UPDATE_KNOWN_DIFFERENCES=1 (see Original/README.md).",
        };
        File.WriteAllLines(path, header.Concat(differences));
    }
}
