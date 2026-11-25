using System.Text;
using ZorkDotNet.Game;
using ZorkDotNet.Ui;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var options = Options.Parse(args);
if (options.Error != null)
{
    Console.Error.WriteLine(options.Error);
    Console.Error.WriteLine(Options.Usage);
    return 2;
}
if (options.ShowHelp)
{
    Console.WriteLine(Options.Usage);
    return 0;
}

var state = new GameState();
if (options.FrontEnd == FrontEnd.Fancy)
{
    if (FancyUi.CanRun(out var reason))
    {
        FancyUi.Run(state, options.Theme);
        return 0;
    }
    Console.Error.WriteLine(reason + " Starting the classic one.");
}
ClassicUi.Run(state, Console.In);
return 0;
