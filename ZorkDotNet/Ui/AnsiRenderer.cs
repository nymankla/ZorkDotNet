using System.Globalization;
using System.Text;

namespace ZorkDotNet.Ui;

/// <summary>
/// Turns a <see cref="CellBuffer"/> into ANSI escape sequences: each row is placed with a cursor move (line wrap is
/// off while the interface runs), and the colour is changed only where the style changes.
/// </summary>
public static class AnsiRenderer
{
    public const string Escape = "\u001b[";

    /// <summary>One frame as text to write to the terminal; without <paramref name="color"/>, only reverse video and bold.</summary>
    public static string Render(CellBuffer buffer, Theme theme, bool color)
    {
        var sb = new StringBuilder(buffer.Width * buffer.Height * 2);
        for (var y = 0; y < buffer.Height; y++)
        {
            sb.Append(Escape).Append(N(y + 1)).Append(";1H");
            Style? current = null;
            for (var x = 0; x < buffer.Width; x++)
            {
                var style = buffer.StyleAt(x, y);
                if (style != current)
                {
                    sb.Append(Sgr(style, theme, color));
                    current = style;
                }
                sb.Append(buffer.CharAt(x, y));
            }
        }
        return sb.Append(Escape).Append("0m").ToString();
    }

    /// <summary>The escape sequence that selects <paramref name="style"/>.</summary>
    public static string Sgr(Style style, Theme theme, bool color)
    {
        if (!color)
            return Escape + "0" + (Theme.Reversed(style) ? ";7" : "") + (Theme.Bold(style) ? ";1" : "") + "m";
        var (fg, bg) = theme[style];
        return Escape + "0;38;5;" + N(fg) + ";48;5;" + N(bg) + "m";
    }

    /// <summary>Moves the cursor to (x, y), counted from 0.</summary>
    public static string MoveTo(int x, int y) => Escape + N(y + 1) + ";" + N(x + 1) + "H";

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}
