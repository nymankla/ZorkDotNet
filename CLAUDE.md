# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

A C#/.NET 11 (C# 15) reimplementation of the 1977 MIT Zork (MDL source: https://github.com/MITDDC/zork). Console app, assembly name `Zork`, plus an xUnit test project. Code comments refer to the original MDL files and routine names (`dung.56`, `act1.37`, `np.92`/`np.93`, `defs.63`; `SPARSE`, `SYN-MATCH`, `GWIM`, `CEVENT`, `FIGHT-DEMON`, ...). Keep that convention when you port more behavior, and treat the MDL source as the reference for how things should behave.

## Commands

Use the repo-root `ZorkDotNet.sln`. `ZorkDotNet/ZorkDotNet.sln` is a leftover that contains only the game project, without the tests.

```sh
dotnet build ZorkDotNet.sln
dotnet run --project ZorkDotNet                     # play
dotnet test ZorkDotNet.sln                          # all tests (CI runs with -c Release)
dotnet test ZorkDotNet.sln --filter "FullyQualifiedName~QUIT_stops_game"   # single test
dotnet format ZorkDotNet.sln --verify-no-changes    # lint check, also run in CI (`dotnet format ZorkDotNet.sln` auto-fixes)
```

### Lint setup

- `Directory.Build.props` turns on the .NET analyzers (`AnalysisLevel` pinned to `11.0-recommended` to match CI's SDK), `EnforceCodeStyleInBuild`, and **`TreatWarningsAsErrors`**. Any analyzer or code-style warning fails the build, so fix it rather than suppressing it.
- `.editorconfig` holds the formatting, naming (`_camelCase` private fields, PascalCase `static readonly` fields and constants) and rule severities. Rules turned off on purpose (CA1861, CA1711, and CA1707 in tests) each have a comment explaining why.
- `end_of_line` is deliberately not set. Git stores LF, and `core.autocrlf` converts per platform.
- Object/room action handlers must match a fixed delegate signature. If a parameter is unused, name it `_`, `_1`, `_2` to satisfy IDE0060.

CI (`.github/workflows/dotnet-desktop.yml`) runs lint and tests, then publishes **Native AOT** executables for win/linux/osx (x64/arm64), each on its own OS's runner. linux-arm (32-bit) has no Native AOT and is published trimmed single-file. `PublishAot` is set in `ZorkDotNet.csproj`, so the AOT and trim analyzers run on every build and their warnings fail it: avoid reflection, reflection-based JSON, `Type.GetType` and runtime code generation. A local `dotnet publish ZorkDotNet/ZorkDotNet.csproj -c Release -r win-x64` needs the Visual Studio C++ build tools. When you push a `v*` tag or start the workflow manually, it creates a GitHub Release. The Linux and macOS binaries are renamed to `zork`.

## Architecture

Everything lives in `ZorkDotNet/Game/` (namespace `ZorkDotNet.Game`). The main loop in `Program.cs` and `GameRunner.Execute` in the tests both call `Parser.PlayTurn(state, line)` (see **Turns** below).

- **`GameState`**: all mutable state. It holds the player (`Winner`, an `Adventurer`; `Here` is the current room), string-keyed boolean `Flags` (MDL-style names such as `"TRAP-DOOR!-FLAG"`), `ObjectState`/`VillainStrength` dictionaries, the `ScheduledEvents` timers, and the pending "Which X?" disambiguation fields. All output goes through `state.Output` (a `TextWriter`), never `Console` directly, so tests can capture it. `ProcessClocks()` runs the per-turn daemons: lamp burn, scheduled events, the thief/robber, and the combat demons.
- **Object locations**: an object's `Location` is a `Place`, a C# 15 union of `Room` (on the floor, MDL OROOM), `Adventurer` (carried) and `GameObject` (inside it, OCAN); `InRoom`, `Carrier` and `Container` are read from it. Move an object only with `obj.MoveTo(place)` (`null` removes it from the game, `first: true` puts it at the head of the list): it keeps `Room.Objects`, `Adventurer.Inventory` and `GameObject.Contents` in step, and those lists are read-only. `Holder` is the adventurer carrying it, even inside a container. Scenery that dung.56 lists in several rooms (the rainbow) stays owned by one room and is listed in the others (`Room.AddScenery`). Pass a nullable `Room` to `MoveTo` only after a null check: a null room converts to a `Place` holding null. `LocationTests` checks that every place and list agree.
- **`World`**: builds the whole dungeon in code (`BuildWorld`). It sets the global flags first, then adds rooms (`AddRoom`), then objects (`AddObject`), and finally places objects with `PlaceInRoom`/`PutInContainer`. The `objectIds` argument of `AddRoom` only works for objects that already exist, which is why most placement happens later. Exits are `new("ROOM")`, `new ExitTarget(flag, room, elseMessage)` (MDL `#CEXIT`) or `ExitTarget.Blocked(message)` (MDL `#NEXIT`). Always use the factory for blocked exits: a bare string is read as a room id. It also registers special exit hooks: `_beforeExitActions` run before a conditional exit is judged (the coffin check, the studio chimney), and `_exitActions` are MDL `CXACTION`s, which run before the exit's flag is checked and return true when they have handled the move (the carousel). It assigns `state.World = this`.
- **IDs and words are cut to 5 characters.** `World.NormId` and `Parser.NormWord` keep only the first five letters (an MDL rule), so `"CANDLES"`, `"CANDL"` and `"CANDLE"` are the same word. Object synonyms and verb words in the tables are stored in this truncated form.
- **`Parser`**: `Execute` first handles a few hard-coded special cases (rug, cyclops "SINBAD", exorcism), replies to a pending "Which X?" question, and treats a bare direction as a move. Otherwise it looks the verb up in `VerbTable`, then runs `Sparse` (splits the words into direct object, preposition and indirect object), `SynMatch` (picks a `Syntax`, does an implicit TAKE where the syntax wants a room object, and handles FLIP), and finally `Dispatch`. `Dispatch` is a large switch on the action name that calls the `Do*` helpers or `ObjectActions`. Movement goes through `TryGo` → `MoveToRoom`, which handles conditional/blocked exits, munged rooms and `Room.OnEnter`.
- **`Verbs`**: verbs whose whole behavior is a short MDL routine (CURSE, JUMP, PRAY, HELLO, EXAMINE, TIME, ...). `Parser.Dispatch` falls through to `Verbs.Run` for any action it has no case for. Every death goes through `GameState.JigsUp` (`Death`, MDL `JIGS-UP`): it asks "Do you want me to try to patch you?", and the next input line is the answer (like QUIT's question). Never end the game for a death by setting `Running = false`.
- **`Balloon`**: the hot-air balloon in the volcano (act2.27 BALLOON and its clock, the wire, the gnome). A vehicle hooks into the parser through `Balloon.ReadIn` (before walking, taking and putting) and `Balloon.ReadOut` (after LOOK), as MDL READ-IN/READ-OUT do. Rooms are land unless `Room.IsWater` (the river, needs the boat) or `Room.IsAir` (the volcano, needs the balloon); `Parser.CanGo` is rooms.99 GOTO.
- **`Robot`**: the robot, the only other actor. `GameState.Robot` is its adventurer (room and inventory) and the ROBOT object its body; TELL ROBOT '...' runs one command for it with `GameState.RobotActing` set (MDL WINNER), limited to ROBOT-ACTIONS. Also the cage, sphere, machine-room buttons and the magnet room.
- **`Thief`**: the thief's demon (act1.38 ROBBER: he walks the original's ROOMS list, robs, appears and leaves) and his own action, the chalice and his hideaway. **`Combat`** is the port's own simple melee for the troll and the thief: the original's melee code (BLOW, FIGHT-DEMON, CURE-CLOCK) is not in the MIT archive.
- **`VerbSyntax.cs`**: `VerbTable` maps each normalized verb word to a `VerbSpec`, which lists one or more `Syntax` patterns (slot `VArg` flags: `VABIT` = inventory, `VRBIT` = room, `VTBIT` = no implicit take). To add a verb, register it here with `AddWords(...)` and add a matching `case` in `Parser.Dispatch`.
- **Behavior hooks**: object-specific behavior is `GameObject.ObjectAction(state, verb, obj)`, implemented as static methods in `ObjectActions`. Room-specific behavior is `Room.RoomAction(state, verb)` (often dynamic LOOK text) and `Room.OnEnter` (MDL `WALK-IN`), implemented in `RoomActions`. As in `ROOM-INFO`, `OnEnter` runs after the room and its objects are described, and not at all when the room is dark. READ texts come from `ReadTexts` (generated from the `ADD-DESC` forms in `dung.56`). You wire them up by passing the method to `AddObject`/`AddRoom` in `World`. The third argument of an object action is the *indirect* object (the tool, the target), never the object itself: look the object up by id. Where the original asks an object first (MDL `OBJECT-ACTION`) and falls back to a default reply, call `ObjectActions.Try`, which reports whether the action said anything. Objects that share a name are told apart by `adjectives:` in `AddObject` ("YELLOW BUTTON").
- **`Combat`**: several features use `Random.Shared`, so tests should not assert on random outcomes.
- **`Clocks`**: named clock events (MDL `CEVENT`). Schedule with `state.Schedule(delay, Clocks.X, argument)`, never with a lambda, so pending events can be saved. A new clock needs a constant, a case in `Clocks.Run`, and an entry in `Known`.
- **`GameSave`**: SAVE/RESTORE of the complete state as JSON (source-generated, so it's safe for trimming): player, flags, clock events, every room (seen, light, exits, contents) and every object (flags, texts, light, contents). Anything new that can change during play must be added to `Capture`/`Apply`. `SaveRestoreTests` checks that a restored game captures back to exactly the saved file. As in the original, SAVE ends the game, and RESTORE validates the whole file before changing anything.
- **Turns**: `Parser.PlayTurn` is one `RDCOM` turn (count the move, run the command, run the clocks). `Program` and `GameRunner` both use it, so code that moves the player must not change `Moves`.

## Goal: a complete, faithful port

The goal is a complete port of the original game. Behavior should match the MDL source, not what seems plausible. When the port and the original disagree, the original is right unless a code comment explains the difference. The tracking issue lists everything that's still missing: https://github.com/nymankla/ZorkDotNet/issues/1 (label `full-port`).

## Tests

**Every change needs tests.** Each bug fix, ported feature, room, object, verb or puzzle gets a test that would fail without the change. Puzzles get a test that plays the whole solution. Keep `dotnet test` green and the lint check clean before you commit.

`ZorkDotNet.Tests/GameRunner` builds a fresh `GameState` + `World`, sends commands with `Run(...)`/`Execute(...)`, and captures the output (`GetOutput()`). A test usually checks substrings of the output and `runner.State.Here?.Id`. Each test also writes a transcript to `ZorkDotNet.Tests/TestLogs/` (the folder is wiped when the test class loads), which helps when debugging a failing scenario. `GameRunner` doesn't run the intro `LOOK`. To test an area far from the start, use `runner.Teleport(roomId)` and `runner.Give(objectId)` instead of walking there and collecting things. `GetOutputSince(mark)` returns only what one command printed. Chance events that use `GameState.Random` (the grue, the thief) never happen in a `GameRunner` unless the test sets `State.Random = new FixedRandom(n)`, and the thief's demon is off unless the test sets `State.ThiefActive = true`. Put tests for one puzzle or feature in their own file (e.g. `GratingTests.cs`).

Never edit `ZorkDotNet.Tests/Original/dung.56` (the original dungeon data). How the comparison against the original works, and how to update `KnownDifferences.txt` after a fix, is in [ZorkDotNet.Tests/CLAUDE.md](ZorkDotNet.Tests/CLAUDE.md).
