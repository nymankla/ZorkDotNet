using ZorkDotNet.Game;

namespace ZorkDotNet.Ui;

/// <summary>A line of the story pane; <see cref="IsCommand"/> marks what the player typed.</summary>
public sealed record StoryLine(string Text, bool IsCommand);

/// <summary>
/// A button in the full-screen interface's button row: its shortcut key, its label and the command it sends (null:
/// it switches the theme).
/// </summary>
public sealed record Button(ConsoleKey Key, string KeyName, string Label, string? Command);

/// <summary>Where the keys go: the input box, the list of commands entered, or the button row.</summary>
public enum Focus
{
    Input,
    Commands,
    Buttons,
}

/// <summary>
/// The full-screen interface without the terminal: the story so far, the commands entered, the input line, focus,
/// scrolling and theme, and what each key does. Commands are played with <see cref="Parser.PlayTurn"/>, as the
/// classic interface does, and the game's text is collected from <see cref="GameState.Output"/>.
/// </summary>
public sealed class FancySession : IDisposable
{
    /// <summary>
    /// The button row. Each game button sends its command as if it were typed; the last switches the theme. They are
    /// pressed with their function key, or with Tab to the row, the arrows and Enter (some terminals, VS Code's among
    /// them, keep function keys such as F1, F5 and F8 for themselves).
    /// </summary>
    public static readonly IReadOnlyList<Button> Buttons =
    [
        new(ConsoleKey.F1, "F1", "Look", "look"),
        new(ConsoleKey.F2, "F2", "Inventory", "inventory"),
        new(ConsoleKey.F3, "F3", "Score", "score"),
        new(ConsoleKey.F4, "F4", "Save", "save"),
        new(ConsoleKey.F5, "F5", "Restore", "restore"),
        new(ConsoleKey.F6, "F6", "Help", "help"),
        new(ConsoleKey.F7, "F7", "Quit", "quit"),
        new(ConsoleKey.F8, "F8", "Theme", null),
    ];

    private readonly StringWriter _capture = new();
    private int _historyIndex = -1;
    private string _draft = "";

    public FancySession(GameState state, Theme theme)
    {
        State = state;
        Theme = theme;
        state.Output = _capture;
        Parser.Start(state);
        TakeOutput();
    }

    public GameState State { get; }
    public Theme Theme { get; set; }
    public List<StoryLine> Story { get; } = [];
    public List<string> Commands { get; } = [];
    public string Input { get; private set; } = "";
    public int Cursor { get; private set; }
    public Focus Focus { get; private set; } = Focus.Input;
    public int SelectedCommand { get; private set; } = -1;
    public int SelectedButton { get; private set; }

    /// <summary>How many lines the story pane is scrolled back from the end (0 follows the story).</summary>
    public int ScrollBack { get; set; }

    /// <summary>What the last command printed (shown on the normal screen when the interface closes).</summary>
    public string LastOutput { get; private set; } = "";

    public void Dispose() => _capture.Dispose();

    /// <summary>The game is over (QUIT answered, or SAVE).</summary>
    public bool Ended => !State.Running;

    /// <summary>
    /// Plays <paramref name="command"/>: echoes it in the story and the command list, then shows the reply. As in the
    /// classic interface, an empty line only counts as the answer to a question.
    /// </summary>
    public void Send(string command)
    {
        if (Ended) return;
        var line = command.Trim();
        var answering = State.AwaitingAnswer;
        if (line.Length == 0 && !answering) return;
        Story.Add(new StoryLine(answering ? line : "> " + line, IsCommand: true));
        if (line.Length > 0) Commands.Add(line);
        Parser.PlayTurn(State, line);
        TakeOutput();
        ScrollBack = 0;
        _historyIndex = -1;
        _draft = "";
    }

    /// <summary>Does what <paramref name="key"/> means; returns false when the player leaves (Ctrl+Q or Ctrl+C).</summary>
    public bool HandleKey(ConsoleKeyInfo key, int pageHeight)
    {
        var ctrl = (key.Modifiers & ConsoleModifiers.Control) != 0;
        if (ctrl && key.Key is ConsoleKey.Q or ConsoleKey.C) return false;
        if (Buttons.FirstOrDefault(b => b.Key == key.Key) is { } button)
        {
            Press(button);
            return true;
        }
        switch (key.Key)
        {
            case ConsoleKey.PageUp:
                ScrollBack += Math.Max(1, pageHeight - 1);
                return true;
            case ConsoleKey.PageDown:
                ScrollBack = Math.Max(0, ScrollBack - Math.Max(1, pageHeight - 1));
                return true;
            case ConsoleKey.Tab:
                SwitchFocus(backwards: (key.Modifiers & ConsoleModifiers.Shift) != 0);
                return true;
        }
        switch (Focus)
        {
            case Focus.Commands: CommandsKey(key); break;
            case Focus.Buttons: ButtonsKey(key); break;
            default: InputKey(key); break;
        }
        return true;
    }

    /// <summary>A button sends its command, or switches the theme.</summary>
    public void Press(Button button)
    {
        if (button.Command == null) Theme = Theme.Other;
        else Send(button.Command);
    }

    /// <summary>Tab goes input, command list (once there is one), buttons, and round again; Shift+Tab goes back.</summary>
    private void SwitchFocus(bool backwards)
    {
        Focus[] order = Commands.Count > 0 ? [Focus.Input, Focus.Commands, Focus.Buttons] : [Focus.Input, Focus.Buttons];
        var next = order[(Array.IndexOf(order, Focus) + (backwards ? order.Length - 1 : 1)) % order.Length];
        if (next == Focus.Commands) SelectedCommand = Commands.Count - 1;
        Focus = next;
    }

    private void ButtonsKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                SelectedButton = (SelectedButton + Buttons.Count - 1) % Buttons.Count;
                break;
            case ConsoleKey.RightArrow:
                SelectedButton = (SelectedButton + 1) % Buttons.Count;
                break;
            case ConsoleKey.Enter or ConsoleKey.Spacebar:
                Press(Buttons[SelectedButton]);
                break;
            case ConsoleKey.Escape:
                Focus = Focus.Input;
                break;
            default:
                if (!char.IsControl(key.KeyChar))
                {
                    Focus = Focus.Input;
                    InputKey(key);
                }
                break;
        }
    }

    private void CommandsKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                SelectedCommand = Math.Max(0, SelectedCommand - 1);
                break;
            case ConsoleKey.DownArrow:
                SelectedCommand = Math.Min(Commands.Count - 1, SelectedCommand + 1);
                break;
            case ConsoleKey.Enter when SelectedCommand >= 0:
                // The command goes back into the input box, to send again or change first.
                SetInput(Commands[SelectedCommand]);
                Focus = Focus.Input;
                break;
            case ConsoleKey.Escape:
                Focus = Focus.Input;
                break;
            default:
                if (!char.IsControl(key.KeyChar))
                {
                    Focus = Focus.Input;
                    InputKey(key);
                }
                break;
        }
    }

    private void InputKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                var command = Input;
                SetInput("");
                Send(command);
                break;
            case ConsoleKey.Escape:
                SetInput("");
                break;
            case ConsoleKey.Backspace when Cursor > 0:
                Input = Input.Remove(Cursor - 1, 1);
                Cursor--;
                break;
            case ConsoleKey.Delete when Cursor < Input.Length:
                Input = Input.Remove(Cursor, 1);
                break;
            case ConsoleKey.LeftArrow:
                Cursor = Math.Max(0, Cursor - 1);
                break;
            case ConsoleKey.RightArrow:
                Cursor = Math.Min(Input.Length, Cursor + 1);
                break;
            case ConsoleKey.Home:
                Cursor = 0;
                break;
            case ConsoleKey.End:
                Cursor = Input.Length;
                break;
            case ConsoleKey.UpArrow:
                Recall(-1);
                break;
            case ConsoleKey.DownArrow:
                Recall(+1);
                break;
            default:
                if (!char.IsControl(key.KeyChar))
                {
                    Input = Input.Insert(Cursor, key.KeyChar.ToString());
                    Cursor++;
                }
                break;
        }
    }

    /// <summary>Up and Down walk through the commands entered, as in a shell; past the newest is what was being typed.</summary>
    private void Recall(int step)
    {
        if (Commands.Count == 0) return;
        if (_historyIndex < 0)
        {
            if (step > 0) return;
            _draft = Input;
            _historyIndex = Commands.Count;
        }
        _historyIndex = Math.Max(0, _historyIndex + step);
        if (_historyIndex >= Commands.Count)
        {
            _historyIndex = -1;
            SetInput(_draft);
        }
        else
            SetInput(Commands[_historyIndex]);
    }

    private void SetInput(string text)
    {
        Input = text;
        Cursor = text.Length;
    }

    /// <summary>Moves what the game printed into the story, one line per line.</summary>
    private void TakeOutput()
    {
        var text = _capture.ToString().Replace("\r", "", StringComparison.Ordinal);
        _capture.GetStringBuilder().Clear();
        LastOutput = text;
        if (text.EndsWith('\n')) text = text[..^1];
        if (text.Length == 0) return;
        foreach (var line in text.Split('\n'))
            Story.Add(new StoryLine(line, IsCommand: false));
    }
}
