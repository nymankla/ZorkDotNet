using System.Text;
using ZorkDotNet.Game;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var state = new GameState();

Parser.Start(state);

while (state.Running)
{
    // FINISH reads the answer to "Do you wish to leave the game?" without the command prompt.
    // JIGS-UP likewise reads "Do you want me to try to patch you?", where an empty answer means yes.
    if (!state.AwaitingAnswer) state.Output.Write("> ");
    var line = Console.ReadLine();
    if (line == null) break;
    line = line.Trim();
    if (string.IsNullOrEmpty(line) && !state.AwaitingAnswer) continue;
    Parser.PlayTurn(state, line);
}
