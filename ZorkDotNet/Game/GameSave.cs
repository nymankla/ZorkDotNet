using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZorkDotNet.Game;

/// <summary>
/// SAVE / RESTORE (rooms.99 DO-SAVE / DO-RESTORE). The original saved its whole memory image, so a save holds
/// everything that can change during play: the player, flags, clock events, every room (seen, exits, contents)
/// and every object (state, texts, contents). Static wiring (descriptions of rooms, action routines) comes from
/// World. The file is JSON written with a source-generated serializer, which is safe for trimmed builds.
/// </summary>
public static class GameSave
{
    public const string DefaultSavePath = "zork.sav";
    public const string DefaultScriptPath = "zork.log";

    /// <summary>Bump when the format changes incompatibly; older files are then refused.</summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// DO-SAVE: "Saving.", write the file, then FINISH without asking: the score is shown and the game ends,
    /// as in the original.
    /// </summary>
    public static void Save(GameState state)
    {
        state.Output.WriteLine("Saving.");
        try
        {
            File.WriteAllText(state.SavePath, JsonSerializer.Serialize(Capture(state), SaveJsonContext.Default.SaveData));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            state.Output.WriteLine("Save failed.");
            state.Output.WriteLine(ex.Message);
            return;
        }
        Scoring.PrintScore(state, ask: false);
        state.Running = false;
    }

    /// <summary>DO-RESTORE: "Restored." and the room description, or "Restore failed." leaving the game as it was.</summary>
    public static void Restore(GameState state)
    {
        SaveData? data;
        try
        {
            data = JsonSerializer.Deserialize(File.ReadAllText(state.SavePath), SaveJsonContext.Default.SaveData);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            data = null;
        }

        // Validate everything before changing anything, so a bad file can't leave a half-restored game.
        if (data == null || state.World == null || Validate(state.World, data) != null)
        {
            state.Output.WriteLine("Restore failed.");
            return;
        }

        Apply(state, state.World, data);
        state.Output.WriteLine("Restored.");
        Parser.Execute(state, "LOOK");
    }

    // ---------------- Capture ----------------

    internal static SaveData Capture(GameState state)
    {
        var world = state.World ?? throw new InvalidOperationException("No world to save.");
        var w = state.Winner;
        return new SaveData
        {
            Version = FormatVersion,
            Player = new PlayerData
            {
                Room = w.CurrentRoom.Id,
                Score = w.Score,
                Moves = w.Moves,
                Strength = w.Strength,
                Staggered = w.Staggered,
                Brief = w.BriefMode,
                SuperBrief = w.SuperBriefMode,
                Vehicle = w.Vehicle?.Id,
                Inventory = w.Inventory.Select(o => o.Id).ToList(),
            },
            Flags = new Dictionary<string, bool>(state.Flags),
            ObjectState = new Dictionary<string, int>(state.ObjectState),
            VillainStrength = new Dictionary<string, int>(state.VillainStrength),
            LampTicks = state.LampTicks,
            LampStage = state.LampStage,
            HelloCount = state.HelloCount,
            Deaths = state.Deaths,
            CandleTicks = state.CandleTicks,
            CandleStage = state.CandleStage,
            MatchesLeft = state.MatchesLeft,
            LightShaftScored = state.LightShaftScored,
            BalloonLocation = state.BalloonLocation,
            Events = state.ScheduledEvents.Select(e => new EventData { TriggerMove = e.TriggerMove, Name = e.Name, Argument = e.Argument }).ToList(),
            Rooms = world.Rooms.Select(r => new RoomData
            {
                Id = r.Id,
                Seen = r.Seen,
                Light = r.HasLight,
                RVars = r.RVars,
                Munged = r.Munged,
                MungedMessage = r.MungedMessage,
                Exits = r.Exits.ToDictionary(e => e.Key, e => new ExitData { Room = e.Value.RoomId, Message = e.Value.BlockedMessage, Flag = e.Value.ConditionFlag }),
                Objects = r.Objects.Select(o => o.Id).ToList(),
            }).ToList(),
            Objects = world.Objects.Select(o => new ObjectData
            {
                Id = o.Id,
                Flags = (int)o.Flags,
                Touched = o.Touched,
                Light = o.LightAmount,
                Open = o.Open,
                Size = o.Size,
                Capacity = o.Capacity,
                Find = o.FindScore,
                Trophy = o.TrophyScore,
                Here = o.DescriptionHere,
                Short = o.DescriptionShort,
                Untouched = o.DescriptionUntouched,
                Contents = o.Contents.Select(c => c.Id).ToList(),
            }).ToList(),
        };
    }

    // ---------------- Validate and apply ----------------

    /// <summary>Returns a reason when the save doesn't fit this world, otherwise null.</summary>
    internal static string? Validate(World world, SaveData data)
    {
        if (data.Version != FormatVersion) return "unsupported version " + data.Version;
        bool IsRoom(string? id) => id != null && world.FindRoom(id) != null;
        bool IsObject(string? id) => id != null && world.FindObject(id) != null;

        if (!IsRoom(data.Player.Room)) return "unknown room " + data.Player.Room;
        if (data.Player.Vehicle != null && !IsObject(data.Player.Vehicle)) return "unknown vehicle " + data.Player.Vehicle;
        if (data.Player.Inventory.FirstOrDefault(i => !IsObject(i)) is { } badInv) return "unknown object " + badInv;
        if (data.Events.FirstOrDefault(e => !Clocks.IsKnown(e.Name)) is { } badEvent) return "unknown clock event " + badEvent.Name;
        foreach (var r in data.Rooms)
        {
            if (!IsRoom(r.Id)) return "unknown room " + r.Id;
            if (r.Objects.FirstOrDefault(i => !IsObject(i)) is { } bad) return "unknown object " + bad;
            if (r.Exits.Values.FirstOrDefault(e => e.Room != null && e.Message == null && e.Flag == null && !IsRoom(e.Room)) is { } badExit)
                return "unknown exit target " + badExit.Room;
        }
        foreach (var o in data.Objects)
        {
            if (!IsObject(o.Id)) return "unknown object " + o.Id;
            if (o.Contents.FirstOrDefault(i => !IsObject(i)) is { } bad) return "unknown object " + bad;
        }
        return null;
    }

    private static void Apply(GameState state, World world, SaveData data)
    {
        // Objects: state first, locations are rebuilt below from the room, container and inventory lists.
        foreach (var d in data.Objects)
        {
            var o = world.FindObject(d.Id)!;
            o.Flags = (ObjectFlags)d.Flags;
            o.Touched = d.Touched;
            o.LightAmount = d.Light;
            o.Open = d.Open;
            o.Size = d.Size;
            o.Capacity = d.Capacity;
            o.FindScore = d.Find;
            o.TrophyScore = d.Trophy;
            o.DescriptionHere = d.Here;
            o.DescriptionShort = d.Short;
            o.DescriptionUntouched = d.Untouched;
        }
        foreach (var o in world.Objects)
        {
            o.InRoom = null;
            o.Container = null;
            o.Carrier = null;
            o.Contents.Clear();
        }
        foreach (var r in world.Rooms) r.Objects.Clear();
        state.Winner.Inventory.Clear();

        foreach (var d in data.Rooms)
        {
            var r = world.FindRoom(d.Id)!;
            r.Seen = d.Seen;
            r.HasLight = d.Light;
            r.RVars = d.RVars;
            r.Munged = d.Munged;
            r.MungedMessage = d.MungedMessage;
            r.Exits.Clear();
            foreach (var (dir, e) in d.Exits)
                r.Exits[dir] = ToExit(e);
            foreach (var id in d.Objects)
            {
                var o = world.FindObject(id)!;
                o.InRoom = r;
                r.Objects.Add(o);
            }
        }
        foreach (var d in data.Objects)
        {
            var container = world.FindObject(d.Id)!;
            foreach (var id in d.Contents)
            {
                var o = world.FindObject(id)!;
                o.Container = container;
                container.Contents.Add(o);
            }
        }

        var w = state.Winner;
        var p = data.Player;
        w.CurrentRoom = world.FindRoom(p.Room)!;
        w.Score = p.Score;
        w.Moves = p.Moves;
        w.Strength = p.Strength;
        w.Staggered = p.Staggered;
        w.BriefMode = p.Brief;
        w.SuperBriefMode = p.SuperBrief;
        w.Vehicle = p.Vehicle == null ? null : world.FindObject(p.Vehicle);
        foreach (var id in p.Inventory)
        {
            var o = world.FindObject(id)!;
            o.Carrier = w;
            w.Inventory.Add(o);
        }

        state.Flags.Clear();
        foreach (var (k, v) in data.Flags) state.Flags[k] = v;
        state.ObjectState.Clear();
        foreach (var (k, v) in data.ObjectState) state.ObjectState[k] = v;
        state.VillainStrength.Clear();
        foreach (var (k, v) in data.VillainStrength) state.VillainStrength[k] = v;
        state.LampTicks = data.LampTicks;
        state.LampStage = data.LampStage;
        state.HelloCount = data.HelloCount;
        state.Deaths = data.Deaths;
        state.CandleTicks = data.CandleTicks;
        state.CandleStage = data.CandleStage;
        state.MatchesLeft = data.MatchesLeft;
        state.LightShaftScored = data.LightShaftScored;
        state.BalloonLocation = data.BalloonLocation ?? "VLBOT";
        state.ScheduledEvents.Clear();
        state.ScheduledEvents.AddRange(data.Events.Select(e => new ScheduledEvent(e.TriggerMove, e.Name, e.Argument)));

        // Nothing from before the restore is pending any more.
        state.PendingVerb = null;
        state.PendingWords = null;
        state.PendingAmbiguousWord = null;
        state.PendingAmbiguousCandidates = null;
        state.PendingNeedsDirect = state.PendingNeedsIndirect = false;
        state.AwaitingQuitConfirmation = false;
        state.Running = true;
    }

    private static ExitTarget ToExit(ExitData e) =>
        e.Flag != null ? new ExitTarget(e.Flag, e.Room!, e.Message)
        : e.Room != null ? new ExitTarget(e.Room)
        : ExitTarget.Blocked(e.Message ?? "");
}

// ---------------- Save file format ----------------

internal sealed class SaveData
{
    public int Version { get; set; }
    public PlayerData Player { get; set; } = new();
    public Dictionary<string, bool> Flags { get; set; } = new();
    public Dictionary<string, int> ObjectState { get; set; } = new();
    public Dictionary<string, int> VillainStrength { get; set; } = new();
    public int LampTicks { get; set; }
    public int LampStage { get; set; }
    public int HelloCount { get; set; }
    public int Deaths { get; set; }
    public int CandleTicks { get; set; }
    public int CandleStage { get; set; }
    public int MatchesLeft { get; set; }
    public bool LightShaftScored { get; set; }
    public string? BalloonLocation { get; set; }
    public List<EventData> Events { get; set; } = new();
    public List<RoomData> Rooms { get; set; } = new();
    public List<ObjectData> Objects { get; set; } = new();
}

internal sealed class PlayerData
{
    public string Room { get; set; } = "";
    public int Score { get; set; }
    public int Moves { get; set; }
    public int Strength { get; set; }
    public bool Staggered { get; set; }
    public bool Brief { get; set; }
    public bool SuperBrief { get; set; }
    public string? Vehicle { get; set; }
    public List<string> Inventory { get; set; } = new();
}

internal sealed class EventData
{
    public int TriggerMove { get; set; }
    public string Name { get; set; } = "";
    public string? Argument { get; set; }
}

internal sealed class RoomData
{
    public string Id { get; set; } = "";
    public bool Seen { get; set; }
    public bool Light { get; set; }
    public int RVars { get; set; }
    public bool Munged { get; set; }
    public string? MungedMessage { get; set; }
    public Dictionary<string, ExitData> Exits { get; set; } = new();
    public List<string> Objects { get; set; } = new();
}

internal sealed class ExitData
{
    public string? Room { get; set; }
    public string? Message { get; set; }
    public string? Flag { get; set; }
}

internal sealed class ObjectData
{
    public string Id { get; set; } = "";
    public int Flags { get; set; }
    public bool Touched { get; set; }
    public int Light { get; set; }
    public bool Open { get; set; }
    public int Size { get; set; }
    public int Capacity { get; set; }
    public int Find { get; set; }
    public int Trophy { get; set; }
    public string Here { get; set; } = "";
    public string Short { get; set; } = "";
    public string? Untouched { get; set; }
    public List<string> Contents { get; set; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SaveData))]
internal sealed partial class SaveJsonContext : JsonSerializerContext;
