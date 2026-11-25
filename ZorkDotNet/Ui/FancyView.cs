using System.Globalization;
using ZorkDotNet.Game;

namespace ZorkDotNet.Ui;

/// <summary>
/// Where the parts of the full-screen interface go on a screen of <see cref="Width"/> by <see cref="Height"/>: the
/// title bar, the button row, the story pane beside the command list, the input box and the key help.
/// </summary>
public sealed record Layout(int Width, int Height)
{
    public const int MinWidth = 70;
    public const int MinHeight = 16;

    public bool Fits => Width >= MinWidth && Height >= MinHeight;
    public const int TitleRow = 0;
    public const int ButtonRow = 1;
    public const int BoxTop = 2;
    public const int PaneTop = 3;
    public int PaneBottom => Height - 5;
    public int PaneHeight => PaneBottom - PaneTop + 1;
    public int SeparatorRow => Height - 4;
    public int InputRow => Height - 3;
    public int BoxBottom => Height - 2;
    public int HelpRow => Height - 1;

    /// <summary>The inner width of the command list: a quarter of the screen, between 16 and 30 columns.</summary>
    public int ListWidth => Math.Clamp(Width / 4, 16, 30);

    /// <summary>The column of the line between the story and the command list.</summary>
    public int Divider => Width - ListWidth - 2;

    /// <summary>The story's text area, one space in from the frame.</summary>
    public const int StoryLeft = 2;
    public int StoryWidth => Divider - 3;
    public int ListLeft => Divider + 2;
    public int ListInnerWidth => Width - ListLeft - 2;
}

/// <summary>Draws a <see cref="FancySession"/> into a <see cref="CellBuffer"/>.</summary>
public static class FancyView
{
    private const string KeyHelp = " Enter send   ↑↓ earlier commands   Tab list and buttons   PgUp/PgDn scroll   Ctrl+C leave";

    /// <summary>Draws the session; returns where the text cursor belongs, or null when it should be hidden.</summary>
    public static (int X, int Y)? Draw(FancySession session, CellBuffer buffer)
    {
        var layout = new Layout(buffer.Width, buffer.Height);
        buffer.Fill(0, 0, buffer.Width, buffer.Height, Style.Background);
        if (!layout.Fits)
        {
            var message = "Make the window bigger (at least " + Layout.MinWidth.ToString(CultureInfo.InvariantCulture) + "×" + Layout.MinHeight.ToString(CultureInfo.InvariantCulture) + ").";
            buffer.Put(Math.Max(0, (buffer.Width - message.Length) / 2), buffer.Height / 2, message, Style.Story);
            return null;
        }
        DrawTitle(session, buffer, layout);
        DrawButtons(session, buffer, layout);
        DrawFrame(session, buffer, layout);
        DrawStory(session, buffer, layout);
        DrawCommands(session, buffer, layout);
        var cursor = DrawInput(session, buffer, layout);
        buffer.Fill(0, layout.HelpRow, buffer.Width, 1, Style.Help);
        buffer.Put(0, layout.HelpRow, KeyHelp, Style.Help);
        return cursor;
    }

    /// <summary>The status line: the room (or darkness), the score and the moves.</summary>
    public static string Status(GameState state)
    {
        var room = GameState.IsLit(state) ? state.Here.DescriptionShort : "Darkness";
        return room + "   Score: " + state.Winner.Score.ToString(CultureInfo.InvariantCulture)
            + "   Moves: " + state.Moves.ToString(CultureInfo.InvariantCulture) + " ";
    }

    private static void DrawTitle(FancySession session, CellBuffer buffer, Layout layout)
    {
        buffer.Fill(0, Layout.TitleRow, layout.Width, 1, Style.Title);
        buffer.Put(0, Layout.TitleRow, " ZorkDotNet", Style.Title);
        var status = Status(session.State);
        buffer.Put(Math.Max(13, layout.Width - status.Length), Layout.TitleRow, status, Style.TitleDim);
    }

    /// <summary>
    /// The buttons, each " F1  Look ", or just " Look " when the full row doesn't fit; the one chosen with Tab and the
    /// arrows is highlighted.
    /// </summary>
    private static void DrawButtons(FancySession session, CellBuffer buffer, Layout layout)
    {
        var buttons = FancySession.Buttons;
        var full = 1 + buttons.Sum(b => b.KeyName.Length + b.Label.Length + 4);
        var withKeys = full <= layout.Width;
        var x = 1;
        for (var i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i];
            var selected = session.Focus == Focus.Buttons && i == session.SelectedButton;
            if (withKeys)
                x = buffer.Put(x, Layout.ButtonRow, " " + button.KeyName, selected ? Style.ListSelected : Style.ButtonKey);
            x = buffer.Put(x, Layout.ButtonRow, " " + button.Label + " ", selected ? Style.ListSelected : Style.Button) + 1;
        }
    }

    private static void DrawFrame(FancySession session, CellBuffer buffer, Layout layout)
    {
        var right = layout.Width - 1;
        Line(buffer, Layout.BoxTop, '┌', '┬', '┐', layout);
        Line(buffer, layout.SeparatorRow, '├', '┴', '┤', layout);
        buffer.Set(layout.Divider, layout.SeparatorRow, '┴', Style.Frame);
        Line(buffer, layout.BoxBottom, '└', '─', '┘', layout);
        for (var y = Layout.PaneTop; y <= layout.PaneBottom; y++)
        {
            buffer.Set(0, y, '│', Style.Frame);
            buffer.Set(layout.Divider, y, '│', Style.Frame);
            buffer.Set(right, y, '│', Style.Frame);
        }
        buffer.Set(0, layout.InputRow, '│', Style.Frame);
        buffer.Set(right, layout.InputRow, '│', Style.Frame);
        buffer.Put(2, Layout.BoxTop, " Story ", Style.FrameTitle);
        buffer.Put(layout.Divider + 2, Layout.BoxTop, " Commands ", Style.FrameTitle);
        if (session.ScrollBack > 0)
            buffer.Put(Math.Max(2, layout.Divider - 22), layout.SeparatorRow, " PgDn: more below ", Style.FrameTitle);
    }

    private static void Line(CellBuffer buffer, int y, char left, char middle, char right, Layout layout)
    {
        for (var x = 0; x < layout.Width; x++) buffer.Set(x, y, '─', Style.Frame);
        buffer.Set(0, y, left, Style.Frame);
        buffer.Set(layout.Divider, y, middle, Style.Frame);
        buffer.Set(layout.Width - 1, y, right, Style.Frame);
    }

    private static void DrawStory(FancySession session, CellBuffer buffer, Layout layout)
    {
        var lines = new List<StoryLine>();
        foreach (var line in session.Story)
            foreach (var part in Wrap(line.Text, layout.StoryWidth))
                lines.Add(line with { Text = part });
        // Keep the scroll position in range now that the width is known.
        session.ScrollBack = Math.Clamp(session.ScrollBack, 0, Math.Max(0, lines.Count - layout.PaneHeight));
        var first = Math.Max(0, lines.Count - layout.PaneHeight - session.ScrollBack);
        for (var i = 0; i < layout.PaneHeight && first + i < lines.Count; i++)
        {
            var line = lines[first + i];
            buffer.Put(Layout.StoryLeft, Layout.PaneTop + i, line.Text, line.IsCommand ? Style.Command : Style.Story, layout.StoryWidth);
        }
    }

    private static void DrawCommands(FancySession session, CellBuffer buffer, Layout layout)
    {
        var commands = session.Commands;
        var selected = session.Focus == Focus.Commands ? session.SelectedCommand : -1;
        // Show the newest commands, or the part of the list around the selection.
        var last = selected >= 0 ? Math.Max(selected, Math.Min(commands.Count - 1, selected + (layout.PaneHeight / 2))) : commands.Count - 1;
        var first = Math.Max(0, last - layout.PaneHeight + 1);
        for (var i = first; i <= last && i < commands.Count; i++)
        {
            var y = Layout.PaneTop + (i - first);
            var style = i == selected ? Style.ListSelected : Style.List;
            if (i == selected) buffer.Fill(layout.ListLeft - 1, y, layout.ListInnerWidth + 2, 1, style);
            buffer.Put(layout.ListLeft, y, Fit(commands[i], layout.ListInnerWidth), style);
        }
    }

    private static (int X, int Y)? DrawInput(FancySession session, CellBuffer buffer, Layout layout)
    {
        var y = layout.InputRow;
        var inner = layout.Width - 2;
        buffer.Fill(1, y, inner, 1, Style.Input);
        if (session.Ended)
        {
            buffer.Put(2, y, "The game is over. Press any key to leave.", Style.InputPrompt, inner - 2);
            return null;
        }
        var prompt = session.State.AwaitingAnswer ? "? " : "> ";
        buffer.Put(2, y, prompt, Style.InputPrompt);
        var left = 2 + prompt.Length;
        var room = inner - prompt.Length - 2;
        // Scroll the input sideways so the cursor stays in view.
        var start = Math.Max(0, session.Cursor - room + 1);
        buffer.Put(left, y, session.Input[start..], Style.Input, room);
        return session.Focus == Focus.Input ? (left + session.Cursor - start, y) : null;
    }

    /// <summary>Cuts <paramref name="text"/> to <paramref name="width"/>, ending in "…" when it doesn't fit.</summary>
    public static string Fit(string text, int width) =>
        text.Length <= width ? text : text[..Math.Max(0, width - 1)] + "…";

    /// <summary>
    /// Word-wraps a line of the story to <paramref name="width"/>: tabs become spaces, the line's indentation is kept on
    /// its first line, and a word longer than the width is split.
    /// </summary>
    public static IEnumerable<string> Wrap(string text, int width)
    {
        text = text.Replace("\t", "    ", StringComparison.Ordinal).TrimEnd();
        if (width <= 0) yield break;
        if (text.Length <= width)
        {
            yield return text;
            yield break;
        }
        var indent = text.Length - text.TrimStart().Length;
        var line = text[..indent];
        foreach (var word in text[indent..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var rest = word;
            while (rest.Length > 0)
            {
                var gap = line.Trim().Length == 0 ? "" : " ";
                if (line.Length + gap.Length + rest.Length <= width)
                {
                    line += gap + rest;
                    rest = "";
                }
                else if (line.Trim().Length > 0)
                {
                    yield return line;
                    line = "";
                }
                else if (line.Length >= width)
                    line = "";
                else
                {
                    var take = width - line.Length;
                    yield return line + rest[..take];
                    rest = rest[take..];
                    line = "";
                }
            }
        }
        if (line.Length > 0) yield return line;
    }
}
