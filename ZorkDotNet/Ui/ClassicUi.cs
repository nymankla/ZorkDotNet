using ZorkDotNet.Game;

namespace ZorkDotNet.Ui;

/// <summary>The original interface: a <c>&gt;</c> prompt, the command typed after it, and the game's answer below.</summary>
public static class ClassicUi
{
    /// <summary>Plays the game, reading commands from <paramref name="input"/> until the game ends or input runs out.</summary>
    public static void Run(GameState state, TextReader input)
    {
        Parser.Start(state);
        while (state.Running)
        {
            // FINISH reads the answer to "Do you wish to leave the game?" without the command prompt.
            // JIGS-UP likewise reads "Do you want me to try to patch you?", where an empty answer means yes.
            if (!state.AwaitingAnswer) state.Output.Write("> ");
            var line = input.ReadLine();
            if (line == null) break;
            line = line.Trim();
            if (string.IsNullOrEmpty(line) && !state.AwaitingAnswer) continue;
            Parser.PlayTurn(state, line);
        }
    }
}
