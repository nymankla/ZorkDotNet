using System.Globalization;
using System.Text;
using ZorkDotNet.Game;

namespace ZorkDotNet.Tests;

/// <summary>
/// Runs the game with a scripted list of commands and logs all input/output to a file and in-memory.
/// </summary>
public sealed class GameRunner : IDisposable
{
    private readonly GameState _state;
    private readonly StringWriter _capture;
    private readonly StreamWriter? _logFile;
    private readonly TextWriter _tee;

    public GameState State => _state;

    /// <summary>
    /// Create a runner that optionally writes to a log file. If logPath is null, only in-memory capture is used.
    /// </summary>
    public GameRunner(string? logPath = null)
    {
        _state = new GameState();
        _ = new World(_state);
        // Chance events (the grue's PROB 75) never happen unless a test asks for them with its own FixedRandom.
        _state.Random = new FixedRandom(99);
        // The thief's demon roams and robs; tests that want him switch him back on.
        _state.ThiefActive = false;
        _capture = new StringWriter(new StringBuilder());
        if (!string.IsNullOrEmpty(logPath))
        {
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            _logFile = new StreamWriter(logPath, append: false, Encoding.UTF8);
            _tee = new TeeWriter(_capture, _logFile);
        }
        else
        {
            _logFile = null;
            _tee = _capture;
        }
        _state.Output = _tee;
    }

    /// <summary>
    /// Run a single command exactly like the game loop (Parser.PlayTurn). Logs "> command" then game output.
    /// </summary>
    public void Execute(string command)
    {
        _tee.WriteLine("> " + command);
        Parser.PlayTurn(_state, command);
        _tee.Flush();
    }

    /// <summary>
    /// Run multiple commands in sequence (no intro LOOK is run automatically).
    /// </summary>
    public void Run(params string[] commands)
    {
        foreach (var cmd in commands)
        {
            if (string.IsNullOrWhiteSpace(cmd)) continue;
            Execute(cmd.Trim());
        }
    }

    /// <summary>
    /// Get all output so far as a single string.
    /// </summary>
    public string GetOutput() => _capture.ToString();

    /// <summary>Output written since <paramref name="mark"/> (a previous <c>GetOutput().Length</c>).</summary>
    public string GetOutputSince(int mark) => _capture.ToString()[mark..];

    /// <summary>Put the player directly in a room (for testing areas far from the start). Costs no move.</summary>
    public void Teleport(string roomId)
    {
        var room = _state.World!.FindRoom(roomId) ?? throw new ArgumentException("No such room: " + roomId, nameof(roomId));
        _state.Winner.CurrentRoom = room;
        room.Seen = true;
    }

    /// <summary>Move an object from wherever it is into the player's inventory.</summary>
    public GameObject Give(string objectId)
    {
        var obj = _state.World!.FindObject(objectId) ?? throw new ArgumentException("No such object: " + objectId, nameof(objectId));
        obj.InRoom?.Objects.Remove(obj);
        obj.Container?.Contents.Remove(obj);
        obj.Carrier?.Inventory.Remove(obj);
        obj.InRoom = null;
        obj.Container = null;
        obj.Carrier = _state.Winner;
        _state.Winner.Inventory.Add(obj);
        return obj;
    }

    public void Dispose()
    {
        _logFile?.Dispose();
    }

    /// <summary>
    /// Create a log path under a base directory (e.g. TestLogs) with test name and optional timestamp.
    /// </summary>
    public static string GetLogPath(string testName, string baseDir = "TestLogs", bool includeTimestamp = true)
    {
        var name = testName.Replace(" ", "_");
        var stamp = includeTimestamp ? "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) : "";
        return Path.Combine(baseDir, name + stamp + ".log");
    }
}
