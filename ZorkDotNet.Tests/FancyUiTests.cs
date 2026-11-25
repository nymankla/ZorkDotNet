using Xunit;
using ZorkDotNet.Game;
using ZorkDotNet.Ui;

namespace ZorkDotNet.Tests;

/// <summary>
/// The command-line options and the two interfaces (issue #23): the classic one unchanged, and the full-screen one,
/// tested without a terminal through <see cref="FancySession"/>, <see cref="FancyView"/> and <see cref="AnsiRenderer"/>.
/// </summary>
public class FancyUiTests
{
    private static FancySession NewSession(Theme? theme = null)
    {
        var state = new GameState { Random = new FixedRandom(99) };
        state.Robber.Active = false;
        return new FancySession(state, theme ?? Theme.Dark);
    }

    private static ConsoleKeyInfo Key(ConsoleKey key, char c = '\0', bool control = false) => new(c, key, false, false, control);

    private static void Type(FancySession session, string text)
    {
        foreach (var c in text)
            session.HandleKey(Key(c == ' ' ? ConsoleKey.Spacebar : ConsoleKey.A, c), 10);
    }

    private static string Screen(CellBuffer buffer) =>
        string.Join("\n", Enumerable.Range(0, buffer.Height).Select(buffer.RowText));

    // ---------------- Options ----------------

    [Theory]
    [InlineData(new string[0], FrontEnd.Classic, "dark")]
    [InlineData(new[] { "--fancy" }, FrontEnd.Fancy, "dark")]
    [InlineData(new[] { "--fancy", "--theme", "light" }, FrontEnd.Fancy, "light")]
    [InlineData(new[] { "--THEME", "Light", "--fancy" }, FrontEnd.Fancy, "light")]
    [InlineData(new[] { "--fancy", "--classic" }, FrontEnd.Classic, "dark")]
    public void Options_choose_the_interface_and_theme(string[] args, FrontEnd frontEnd, string theme)
    {
        var options = Options.Parse(args);

        Assert.Null(options.Error);
        Assert.Equal(frontEnd, options.FrontEnd);
        Assert.Equal(theme, options.Theme.Name);
    }

    [Theory]
    [InlineData("--bogus")]
    [InlineData("--theme")]
    [InlineData("--theme", "purple")]
    public void Unknown_options_are_errors(params string[] args) => Assert.NotNull(Options.Parse(args).Error);

    [Fact]
    public void Help_is_an_option() => Assert.True(Options.Parse(["--help"]).ShowHelp);

    // ---------------- The classic interface ----------------

    [Fact]
    public void The_classic_interface_is_unchanged()
    {
        var output = new StringWriter();
        var state = new GameState { Output = output };

        ClassicUi.Run(state, new StringReader("north\nquit\ny\n"));

        var text = output.ToString().Replace("\r", "", StringComparison.Ordinal);
        Assert.StartsWith("Welcome to Dungeon.\nThis version created ", text, StringComparison.Ordinal);
        Assert.Contains("There is a small mailbox here.\n> You are facing the north side of a white house.", text, StringComparison.Ordinal);
        // The quit question is answered without a prompt, as in the original.
        Assert.Contains("Do you wish to leave the game? (Y is affirmative): ", text, StringComparison.Ordinal);
        Assert.False(state.Running);
    }

    // ---------------- The session ----------------

    [Fact]
    public void The_session_opens_with_the_start_of_the_game()
    {
        using var session = NewSession();

        Assert.Equal("Welcome to Dungeon.", session.Story[0].Text);
        Assert.Contains(session.Story, l => l.Text.StartsWith("You are in an open field", StringComparison.Ordinal));
        Assert.Empty(session.Commands);
    }

    [Fact]
    public void A_typed_command_is_echoed_listed_and_answered()
    {
        using var session = NewSession();

        Type(session, "open mailbox");
        Assert.Equal("open mailbox", session.Input);
        session.HandleKey(Key(ConsoleKey.Enter), 10);

        Assert.Equal("", session.Input);
        Assert.Equal(["open mailbox"], session.Commands);
        Assert.Contains(new StoryLine("> open mailbox", IsCommand: true), session.Story);
        Assert.Equal("Opening the mailbox reveals a leaflet.", session.Story[^1].Text);
    }

    [Fact]
    public void The_status_line_shows_the_room_score_and_moves()
    {
        using var session = NewSession();

        session.Send("north");

        Assert.Equal("North of House   Score: 0   Moves: 1 ", FancyView.Status(session.State));
    }

    [Fact]
    public void Buttons_send_their_commands()
    {
        using var session = NewSession();

        session.HandleKey(Key(ConsoleKey.F2), 10);

        Assert.Equal("inventory", session.Commands[^1]);
        Assert.Contains("> inventory", session.Story.Select(l => l.Text));
    }

    [Fact]
    public void Up_and_down_recall_earlier_commands()
    {
        using var session = NewSession();
        session.Send("north");
        session.Send("south");
        Type(session, "lo");

        session.HandleKey(Key(ConsoleKey.UpArrow), 10);
        Assert.Equal("south", session.Input);
        session.HandleKey(Key(ConsoleKey.UpArrow), 10);
        Assert.Equal("north", session.Input);
        session.HandleKey(Key(ConsoleKey.UpArrow), 10);
        Assert.Equal("north", session.Input);
        session.HandleKey(Key(ConsoleKey.DownArrow), 10);
        session.HandleKey(Key(ConsoleKey.DownArrow), 10);
        Assert.Equal("lo", session.Input); // back to what was being typed
    }

    [Fact]
    public void A_command_from_the_list_goes_back_into_the_input_box()
    {
        using var session = NewSession();
        session.Send("north");
        session.Send("south");

        session.HandleKey(Key(ConsoleKey.Tab), 10);
        Assert.Equal(Focus.Commands, session.Focus);
        Assert.Equal(1, session.SelectedCommand);
        session.HandleKey(Key(ConsoleKey.UpArrow), 10);
        session.HandleKey(Key(ConsoleKey.Enter), 10);

        Assert.Equal(Focus.Input, session.Focus);
        Assert.Equal("north", session.Input);
        Assert.Equal(2, session.Commands.Count); // copied, not sent
    }

    [Fact]
    public void The_input_line_can_be_edited()
    {
        using var session = NewSession();
        Type(session, "nrth");

        session.HandleKey(Key(ConsoleKey.Home), 10);
        session.HandleKey(Key(ConsoleKey.RightArrow), 10);
        Type(session, "o");
        Assert.Equal("north", session.Input);
        session.HandleKey(Key(ConsoleKey.End), 10);
        session.HandleKey(Key(ConsoleKey.Backspace), 10);
        Assert.Equal("nort", session.Input);
        session.HandleKey(Key(ConsoleKey.Escape), 10);
        Assert.Equal("", session.Input);
    }

    [Fact]
    public void Questions_are_answered_in_the_input_box()
    {
        using var session = NewSession();

        session.HandleKey(Key(ConsoleKey.F7), 10);
        Assert.True(session.State.AwaitingAnswer);
        Assert.Contains("Do you wish to leave the game?", session.Story[^1].Text, StringComparison.Ordinal);
        session.Send("y");

        Assert.True(session.Ended);
        Assert.Equal(new StoryLine("y", IsCommand: true), session.Story.Last(l => l.IsCommand)); // no prompt for an answer
    }

    [Fact]
    public void F8_switches_the_theme_and_ctrl_q_leaves()
    {
        using var session = NewSession();

        session.HandleKey(Key(ConsoleKey.F8), 10);
        Assert.Same(Theme.Light, session.Theme);
        session.HandleKey(Key(ConsoleKey.F8), 10);
        Assert.Same(Theme.Dark, session.Theme);
        Assert.False(session.HandleKey(Key(ConsoleKey.Q, '\u0011', control: true), 10));
    }

    [Fact]
    public void Script_keeps_writing_to_the_interface()
    {
        using var session = NewSession();

        session.Send("script");
        session.Send("script"); // a second SCRIPT used to keep writing to the closed first file
        session.Send("look");
        session.Send("unscript");

        Assert.Contains(session.Story.Skip(5), l => l.Text.StartsWith("You are in an open field", StringComparison.Ordinal)); // LOOK's answer
        Assert.Equal("Script file closed.", session.Story[^1].Text);
    }

    // ---------------- The view ----------------

    [Fact]
    public void The_view_draws_the_status_buttons_story_and_input()
    {
        using var session = NewSession();
        Type(session, "op");
        var buffer = new CellBuffer(100, 30);

        var cursor = FancyView.Draw(session, buffer);

        Assert.Contains("ZorkDotNet", buffer.RowText(0), StringComparison.Ordinal);
        Assert.EndsWith("West of House   Score: 0   Moves: 0 ", buffer.RowText(0), StringComparison.Ordinal);
        Assert.Contains(" F1 Look ", buffer.RowText(1), StringComparison.Ordinal);
        Assert.Contains(" F7 Quit ", buffer.RowText(1), StringComparison.Ordinal);
        Assert.Contains("Welcome to Dungeon.", Screen(buffer), StringComparison.Ordinal);
        Assert.Contains("│ > op", buffer.RowText(27), StringComparison.Ordinal);
        Assert.Equal((6, 27), cursor);
        Assert.Equal(Style.Title, buffer.StyleAt(0, 0));
    }

    [Fact]
    public void Commands_are_listed_beside_the_story()
    {
        using var session = NewSession();
        session.Send("north");
        session.Send("south");
        var buffer = new CellBuffer(100, 30);
        var layout = new Layout(100, 30);

        FancyView.Draw(session, buffer);

        Assert.StartsWith("north", buffer.RowText(Layout.PaneTop)[layout.ListLeft..], StringComparison.Ordinal);
        Assert.StartsWith("south", buffer.RowText(Layout.PaneTop + 1)[layout.ListLeft..], StringComparison.Ordinal);
    }

    [Fact]
    public void Paging_up_scrolls_the_story_back()
    {
        using var session = NewSession();
        for (var i = 0; i < 10; i++) session.Send("look");
        var buffer = new CellBuffer(80, 20);
        FancyView.Draw(session, buffer);
        var bottom = Screen(buffer);

        session.HandleKey(Key(ConsoleKey.PageUp), new Layout(80, 20).PaneHeight);
        FancyView.Draw(session, buffer);

        Assert.NotEqual(bottom, Screen(buffer));
        Assert.Contains("PgDn: more below", Screen(buffer), StringComparison.Ordinal);
        session.Send("north"); // new output follows the story again
        Assert.Equal(0, session.ScrollBack);
    }

    [Fact]
    public void A_window_that_is_too_small_says_so()
    {
        using var session = NewSession();
        var buffer = new CellBuffer(40, 10);

        Assert.Null(FancyView.Draw(session, buffer));
        Assert.Contains("Make the window bigger", Screen(buffer), StringComparison.Ordinal);
        Assert.False(new Layout(59, 30).Fits);
        Assert.True(new Layout(60, 16).Fits);
    }

    [Theory]
    [InlineData("A short line.", 20, new[] { "A short line." })]
    [InlineData("The quick brown fox jumps", 10, new[] { "The quick", "brown fox", "jumps" })]
    [InlineData("  sword:  Taken.", 40, new[] { "  sword:  Taken." })]
    [InlineData("  indented words wrap here", 12, new[] { "  indented", "words wrap", "here" })]
    [InlineData("abcdefghijkl", 5, new[] { "abcde", "fghij", "kl" })]
    [InlineData("", 10, new[] { "" })]
    public void Story_lines_wrap_at_words(string text, int width, string[] expected) =>
        Assert.Equal(expected, FancyView.Wrap(text, width));

    // ---------------- The renderer ----------------

    [Fact]
    public void Colours_come_from_the_theme_and_no_color_uses_reverse_video()
    {
        var buffer = new CellBuffer(10, 2);
        buffer.Put(0, 0, "Title", Style.Title);

        var colored = AnsiRenderer.Render(buffer, Theme.Dark, color: true);
        var plain = AnsiRenderer.Render(buffer, Theme.Dark, color: false);

        Assert.Contains("\u001b[0;38;5;231;48;5;24mTitle", colored, StringComparison.Ordinal);
        Assert.DoesNotContain("38;5", plain, StringComparison.Ordinal);
        Assert.Contains("\u001b[0;7mTitle", plain, StringComparison.Ordinal);
        Assert.StartsWith("\u001b[1;1H", colored, StringComparison.Ordinal);
        Assert.Contains("\u001b[2;1H", colored, StringComparison.Ordinal);
    }
}
