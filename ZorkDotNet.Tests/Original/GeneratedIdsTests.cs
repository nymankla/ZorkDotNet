using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace ZorkDotNet.Tests.Original;

/// <summary>
/// <c>ZorkDotNet/Game/Ids.cs</c> (the <c>Flag</c>, <c>Obj</c> and <c>RoomId</c> constants) is generated from dung.56,
/// so a flag, object or room name in the code can only be one the original has. Regenerate it with
/// <c>ZORK_UPDATE_IDS=1</c>.
/// </summary>
public partial class GeneratedIdsTests
{
    private const string UpdateVariable = "ZORK_UPDATE_IDS";
    private static readonly string OutputDir = Path.Combine(AppContext.BaseDirectory, "Original");

    [Fact]
    public void Ids_are_generated_from_the_original()
    {
        var dung = File.ReadAllText(Path.Combine(OutputDir, "dung.56"), Encoding.Latin1);
        var expected = Generate(OriginalDungeon.Parse(dung), dung);
        var path = IdsPath();

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllText(path, expected);
            return;
        }

        var actual = File.ReadAllText(path).ReplaceLineEndings("\n");
        Assert.True(actual == expected, $"Ids.cs is out of date with dung.56. Run the tests with {UpdateVariable}=1 and review the diff.");
    }

    [Fact]
    public void Game_code_names_flags_objects_and_rooms_by_their_constants()
    {
        var gameDir = Path.GetDirectoryName(IdsPath())!;
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(gameDir, "*.cs").Where(f => Path.GetFileName(f) is not ("Ids.cs" or "PortFlags.cs")))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
                if (RawIdUse().IsMatch(lines[i]))
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
        }
        Assert.True(offenders.Count == 0, "Use Flag.*, Obj.* and RoomId.* instead of string literals:\n" + string.Join("\n", offenders));
    }

    /// <summary>A flag, object or room looked up by a string literal.</summary>
    [GeneratedRegex(@"(GetFlag|SetFlag|FindObject|FindRoom)\(""|""[A-Z0-9-]+!-FLAG""|\.Id (==|!=) ""[A-Z0-9]+""|\.Id is ""[A-Z0-9]+""")]
    private static partial Regex RawIdUse();

    private static string IdsPath([CallerFilePath] string sourceFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", "..", "ZorkDotNet", "Game", "Ids.cs"));

    internal static string Generate(OriginalDungeon original, string dung)
    {
        var sb = new StringBuilder();
        sb.Append("""
            namespace ZorkDotNet.Game;

            // Generated from dung.56 by GeneratedIdsTests (ZORK_UPDATE_IDS=1). Don't edit by hand; flags the port adds
            // itself are in PortFlags.cs.

            /// <summary>The game's flags: dung.56 MGVALS (the monads, named "...!-FLAG").</summary>
            public static partial class Flag
            {

            """.ReplaceLineEndings("\n"));
        var mgvals = Mgvals().Match(dung).Groups[1].Value;
        foreach (var flag in mgvals.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Where(f => f.EndsWith("!-FLAG", StringComparison.Ordinal)))
        {
            var name = flag[..^"!-FLAG".Length];
            if (name.EndsWith("-FLAG", StringComparison.Ordinal)) name = name[..^"-FLAG".Length];
            sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"    public const string {Pascal(name)} = \"{flag}\";\n");
        }
        sb.Append("}\n\n/// <summary>Object ids (dung.56 OID), with the object's short description.</summary>\npublic static class Obj\n{\n");
        foreach (var o in original.Objects.OrderBy(o => o.Id, StringComparer.Ordinal))
            sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"    /// <summary>{Xml(o.ShortDescription)}</summary>\n    public const string {Pascal(o.Id)} = \"{o.Id}\";\n");
        sb.Append("}\n\n/// <summary>Room ids (dung.56 RID), with the room's short description.</summary>\npublic static class RoomId\n{\n");
        foreach (var r in original.Rooms.OrderBy(r => r.Id, StringComparer.Ordinal))
            sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"    /// <summary>{Xml(r.ShortDescription)}</summary>\n    public const string {Pascal(r.Id)} = \"{r.Id}\";\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    /// <summary>KITCHEN-WINDOW → KitchenWindow, MAZ14 → Maz14; "#####" (the adventurer's own object) → Player.</summary>
    private static string Pascal(string id)
    {
        if (id == "#####") return "Player";
        var sb = new StringBuilder();
        foreach (var part in id.Split('-', StringSplitOptions.RemoveEmptyEntries))
        {
            var clean = new string(part.Where(char.IsLetterOrDigit).ToArray());
            if (clean.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(clean[0])).Append(clean[1..].ToLowerInvariant());
        }
        if (sb.Length == 0 || char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }

    private static string Xml(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

    [GeneratedRegex(@"<PSETG MGVALS\s*'!\[(.*?)!\]>", RegexOptions.Singleline)]
    private static partial Regex Mgvals();
}
