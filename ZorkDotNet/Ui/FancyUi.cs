using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ZorkDotNet.Game;

namespace ZorkDotNet.Ui;

/// <summary>
/// The full-screen interface in a real terminal: it uses the alternate screen (the terminal's own contents come back
/// afterwards), draws a <see cref="FancySession"/> after every key and on resize, and puts everything back when it
/// ends, Ctrl+C included.
/// </summary>
public static class FancyUi
{
    /// <summary>
    /// The full-screen interface needs a terminal to draw in and read keys from, at least
    /// <see cref="Layout.MinWidth"/> by <see cref="Layout.MinHeight"/>. Otherwise <paramref name="reason"/> says why not.
    /// </summary>
    public static bool CanRun(out string reason)
    {
        reason = "";
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            reason = "The full-screen interface needs an interactive terminal.";
            return false;
        }
        try
        {
            if (new Layout(Console.WindowWidth, Console.WindowHeight).Fits) return true;
            reason = "The window is too small for the full-screen interface (it needs at least "
                + Layout.MinWidth.ToString(System.Globalization.CultureInfo.InvariantCulture) + "×"
                + Layout.MinHeight.ToString(System.Globalization.CultureInfo.InvariantCulture) + ").";
        }
        catch (IOException)
        {
            reason = "The full-screen interface can't find the size of this terminal.";
        }
        return false;
    }

    /// <summary>Plays the game full-screen until it ends or the player leaves.</summary>
    public static void Run(GameState state, Theme theme)
    {
        var color = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
        using var session = new FancySession(state, theme);
        var treatControlC = Console.TreatControlCAsInput;
        if (OperatingSystem.IsWindows()) WindowsConsole.EnableVirtualTerminal();
        Console.TreatControlCAsInput = true; // Ctrl+C leaves through the same tidy exit as Ctrl+Q
        // Alternate screen, no line wrap at the right edge, cursor hidden while drawing.
        Console.Out.Write(AnsiRenderer.Escape + "?1049h" + AnsiRenderer.Escape + "?7l");
        try
        {
            Loop(session, color);
        }
        finally
        {
            Console.Out.Write(AnsiRenderer.Escape + "0m" + AnsiRenderer.Escape + "?7h" + AnsiRenderer.Escape + "?25h" + AnsiRenderer.Escape + "?1049l");
            Console.Out.Flush();
            Console.TreatControlCAsInput = treatControlC;
        }
        // The last thing the game said (the score, when it ends) stays on the normal screen.
        if (session.Ended) Console.Out.Write(session.LastOutput);
    }

    private static void Loop(FancySession session, bool color)
    {
        var size = (Width: -1, Height: -1);
        var dirty = true;
        while (true)
        {
            var now = (Width: Console.WindowWidth, Height: Console.WindowHeight);
            if (dirty || now != size)
            {
                size = now;
                Draw(session, size.Width, size.Height, color);
                dirty = false;
            }
            if (!Console.KeyAvailable)
            {
                Thread.Sleep(25);
                continue;
            }
            var key = Console.ReadKey(intercept: true);
            if (session.Ended) return;
            if (!session.HandleKey(key, new Layout(size.Width, size.Height).PaneHeight)) return;
            dirty = true;
        }
    }

    private static void Draw(FancySession session, int width, int height, bool color)
    {
        var buffer = new CellBuffer(width, height);
        var cursor = FancyView.Draw(session, buffer);
        var frame = AnsiRenderer.Escape + "?25l" + AnsiRenderer.Render(buffer, session.Theme, color);
        if (cursor is { } at) frame += AnsiRenderer.MoveTo(at.X, at.Y) + AnsiRenderer.Escape + "?25h";
        Console.Out.Write(frame);
        Console.Out.Flush();
    }
}

/// <summary>Turns on ANSI escape sequences in the Windows console (Windows Terminal has them on already).</summary>
[SupportedOSPlatform("windows")]
internal static partial class WindowsConsole
{
    private const int StdOutputHandle = -11;
    private const uint EnableProcessedOutput = 0x1;
    private const uint EnableVirtualTerminalProcessing = 0x4;

    public static bool EnableVirtualTerminal()
    {
        var handle = GetStdHandle(StdOutputHandle);
        if (!GetConsoleMode(handle, out var mode)) return false;
        return (mode & EnableVirtualTerminalProcessing) != 0
            || SetConsoleMode(handle, mode | EnableProcessedOutput | EnableVirtualTerminalProcessing);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(nint hConsoleHandle, uint dwMode);
}
