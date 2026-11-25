namespace ZorkDotNet.Ui;

/// <summary>The interface the player asked for on the command line.</summary>
public enum FrontEnd
{
    /// <summary>The original teletype style: a prompt and scrolling text (the default).</summary>
    Classic,

    /// <summary>The full-screen interface (<c>--fancy</c>).</summary>
    Fancy,
}

/// <summary>The command-line options.</summary>
public sealed record Options(FrontEnd FrontEnd, Theme Theme, bool ShowHelp, string? Error)
{
    public const string Usage = """
        Usage: zork [options]

          --fancy              Play in a full-screen interface, with buttons and a list of your commands.
          --theme dark|light   The full-screen interface's colours (dark by default).
          --classic            Play in the original style: a prompt and scrolling text (the default).
          --help               Show this help.
        """;

    /// <summary>Reads the arguments; an unknown option sets <see cref="Error"/>.</summary>
    public static Options Parse(IReadOnlyList<string> args)
    {
        var ui = FrontEnd.Classic;
        var theme = Theme.Dark;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--fancy":
                    ui = FrontEnd.Fancy;
                    break;
                case "--classic":
                    ui = FrontEnd.Classic;
                    break;
                case "--theme" when i + 1 < args.Count && Theme.Named(args[i + 1]) is { } named:
                    theme = named;
                    i++;
                    break;
                case "--theme":
                    return new Options(ui, theme, false, "--theme needs dark or light.");
                case "--help" or "-h" or "-?" or "/?":
                    return new Options(ui, theme, true, null);
                default:
                    return new Options(ui, theme, false, "Unknown option: " + args[i]);
            }
        }
        return new Options(ui, theme, false, null);
    }
}
