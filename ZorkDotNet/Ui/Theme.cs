namespace ZorkDotNet.Ui;

/// <summary>What a cell on the full-screen interface is part of; a <see cref="Theme"/> gives each its colours.</summary>
public enum Style
{
    Background,
    Title,
    TitleDim,
    Button,
    ButtonKey,
    Frame,
    FrameTitle,
    Story,
    Command,
    List,
    ListSelected,
    Input,
    InputPrompt,
    Help,
}

/// <summary>
/// The full-screen interface's colours: a foreground and background from the 256-colour palette for each
/// <see cref="Style"/> (every common terminal has those, unlike 24-bit colour).
/// </summary>
public sealed class Theme
{
    private readonly (byte Fg, byte Bg)[] _colors;

    private Theme(string name, (byte Fg, byte Bg)[] colors)
    {
        Name = name;
        _colors = colors;
    }

    public string Name { get; }

    /// <summary>The colours of <paramref name="style"/>.</summary>
    public (byte Fg, byte Bg) this[Style style] => _colors[(int)style];

    /// <summary>Light text on dark grey.</summary>
    public static readonly Theme Dark = new("dark",
    [
        (252, 234), // Background
        (231, 24),  // Title
        (153, 24),  // TitleDim
        (223, 237), // Button
        (214, 237), // ButtonKey
        (242, 234), // Frame
        (180, 234), // FrameTitle
        (252, 234), // Story
        (179, 234), // Command
        (248, 234), // List
        (231, 31),  // ListSelected
        (231, 236), // Input
        (179, 236), // InputPrompt
        (245, 233), // Help
    ]);

    /// <summary>Dark text on white.</summary>
    public static readonly Theme Light = new("light",
    [
        (236, 255), // Background
        (231, 31),  // Title
        (195, 31),  // TitleDim
        (236, 252), // Button
        (130, 252), // ButtonKey
        (248, 255), // Frame
        (94, 255),  // FrameTitle
        (235, 255), // Story
        (130, 255), // Command
        (240, 255), // List
        (231, 31),  // ListSelected
        (232, 254), // Input
        (130, 254), // InputPrompt
        (242, 254), // Help
    ]);

    /// <summary>The other theme (F8 switches).</summary>
    public Theme Other => this == Dark ? Light : Dark;

    /// <summary>The theme called <paramref name="name"/> ("dark" or "light"), or null.</summary>
    public static Theme? Named(string name) => name.ToLowerInvariant() switch
    {
        "dark" => Dark,
        "light" => Light,
        _ => null,
    };

    /// <summary>Without colour (NO_COLOR), these styles are shown in reverse video so they still stand out.</summary>
    public static bool Reversed(Style style) => style is Style.Title or Style.TitleDim or Style.Button or Style.ButtonKey or Style.ListSelected;

    /// <summary>Without colour, these styles are bold.</summary>
    public static bool Bold(Style style) => style is Style.Command or Style.FrameTitle or Style.ButtonKey or Style.InputPrompt;
}
