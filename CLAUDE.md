# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

A C#/.NET 8 reimplementation of the 1977 MIT Zork (MDL source: https://github.com/MITDDC/zork). Console app, assembly name `Zork`, plus an xUnit test project. Code comments refer to the original MDL files and routine names (`dung.56`, `act1.37`, `np.92`/`np.93`, `defs.63`; `SPARSE`, `SYN-MATCH`, `GWIM`, `CEVENT`, `FIGHT-DEMON`, ...). Keep that convention when you port more behavior, and treat the MDL source as the reference for how things should behave.

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

- `Directory.Build.props` turns on the .NET analyzers (`AnalysisLevel` pinned to `8.0-recommended` to match CI's SDK), `EnforceCodeStyleInBuild`, and **`TreatWarningsAsErrors`**. Any analyzer or code-style warning fails the build, so fix it rather than suppressing it.
- `.editorconfig` holds the formatting, naming (`_camelCase` private fields, PascalCase `static readonly` fields and constants) and rule severities. Rules turned off on purpose (CA1861, CA1711, and CA1707 in tests) each have a comment explaining why.
- `end_of_line` is deliberately not set. Git stores LF, and `core.autocrlf` converts per platform.
- Object/room action handlers must match a fixed delegate signature. If a parameter is unused, name it `_`, `_1`, `_2` to satisfy IDE0060.

CI (`.github/workflows/dotnet-desktop.yml`) runs the tests and then publishes self-contained, single-file, **trimmed** executables for win/linux/osx (x64/arm64, plus linux-arm). Trimming matters: avoid reflection-based patterns that the trimmer can't see. When you push a `v*` tag or start the workflow manually, it creates a GitHub Release. The Linux and macOS binaries are renamed to `zork`.

## Architecture

Everything lives in `ZorkDotNet/Game/` (namespace `ZorkDotNet.Game`). The main loop in `Program.cs` and `GameRunner.Execute` in the tests both call `Parser.PlayTurn(state, line)` (see **Turns** below).

- **`GameState`**: all mutable state. It holds the player (`Winner`, an `Adventurer`; `Here` is the current room), string-keyed boolean `Flags` (MDL-style names such as `"TRAP-DOOR!-FLAG"`), `ObjectState`/`VillainStrength` dictionaries, the `ScheduledEvents` timers, and the pending "Which X?" disambiguation fields. All output goes through `state.Output` (a `TextWriter`), never `Console` directly, so tests can capture it. `ProcessClocks()` runs the per-turn daemons: lamp burn, scheduled events, the thief/robber, and the combat demons.
- **`World`**: builds the whole dungeon in code (`BuildWorld`). It sets the global flags first, then adds rooms (`AddRoom`), then objects (`AddObject`), and finally places objects with `PlaceInRoom`/`PutInContainer`. The `objectIds` argument of `AddRoom` only works for objects that already exist, which is why most placement happens later. Exits are `new("ROOM")`, `new ExitTarget(flag, room, elseMessage)` (MDL `#CEXIT`) or `ExitTarget.Blocked(message)` (MDL `#NEXIT`). Always use the factory for blocked exits: a bare string is read as a room id. It also registers special exit hooks (`_exitActions`, `_beforeExitActions`, e.g. the carousel and the coffin check) and assigns `state.World = this`.
- **IDs and words are cut to 5 characters.** `World.NormId` and `Parser.NormWord` keep only the first five letters (an MDL rule), so `"CANDLES"`, `"CANDL"` and `"CANDLE"` are the same word. Object synonyms and verb words in the tables are stored in this truncated form.
- **`Parser`**: `Execute` first handles a few hard-coded special cases (rug, cyclops "SINBAD", exorcism), replies to a pending "Which X?" question, and treats a bare direction as a move. Otherwise it looks the verb up in `VerbTable`, then runs `Sparse` (splits the words into direct object, preposition and indirect object), `SynMatch` (picks a `Syntax`, does an implicit TAKE where the syntax wants a room object, and handles FLIP), and finally `Dispatch`. `Dispatch` is a large switch on the action name that calls the `Do*` helpers or `ObjectActions`. Movement goes through `TryGo` → `MoveToRoom`, which handles conditional/blocked exits, munged rooms and `Room.OnEnter`.
- **`VerbSyntax.cs`**: `VerbTable` maps each normalized verb word to a `VerbSpec`, which lists one or more `Syntax` patterns (slot `VArg` flags: `VABIT` = inventory, `VRBIT` = room, `VTBIT` = no implicit take). To add a verb, register it here with `AddWords(...)` and add a matching `case` in `Parser.Dispatch`.
- **Behavior hooks**: object-specific behavior is `GameObject.ObjectAction(state, verb, obj)`, implemented as static methods in `ObjectActions`. Room-specific behavior is `Room.RoomAction(state, verb)` (often dynamic LOOK text) and `Room.OnEnter`, implemented in `RoomActions`. You wire them up by passing the method to `AddObject`/`AddRoom` in `World`.
- **`Combat`**: weapon and light-source checks, the troll/thief melee, the sword glow, and room robbing. Several features use `Random.Shared`, so tests should not assert on random outcomes.
- **`Clocks`**: named clock events (MDL `CEVENT`). Schedule with `state.Schedule(delay, Clocks.X, argument)`, never with a lambda, so pending events can be saved. A new clock needs a constant, a case in `Clocks.Run`, and an entry in `Known`.
- **`GameSave`**: SAVE/RESTORE of the complete state as JSON (source-generated, so it's safe for trimming): player, flags, clock events, every room (seen, light, exits, contents) and every object (flags, texts, light, contents). Anything new that can change during play must be added to `Capture`/`Apply`. `SaveRestoreTests` checks that a restored game captures back to exactly the saved file. As in the original, SAVE ends the game, and RESTORE validates the whole file before changing anything.
- **`Scoring`**: `SCORE-MAX` is computed from room and object values plus the light shaft. It also holds the original score report and rank table, and QUIT with its yes/no question.
- **Turns**: `Parser.PlayTurn` is one `RDCOM` turn (count the move, run the command, run the clocks). `Program` and `GameRunner` both use it, so code that moves the player must not change `Moves`.

## Goal: a complete, faithful port

The goal is a complete port of the original game. Behavior should match the MDL source, not what seems plausible. When the port and the original disagree, the original is right unless a code comment explains the difference. The tracking issue lists everything that's still missing: https://github.com/nymankla/ZorkDotNet/issues/1 (label `full-port`).

## Tests

**Every change needs tests.** Each bug fix, ported feature, room, object, verb or puzzle gets a test that would fail without the change. Puzzles get a test that plays the whole solution. Keep `dotnet test` green and the lint check clean before you commit.

`ZorkDotNet.Tests/GameRunner` builds a fresh `GameState` + `World`, sends commands with `Run(...)`/`Execute(...)`, and captures the output (`GetOutput()`). A test usually checks substrings of the output and `runner.State.Here?.Id`. Each test also writes a transcript to `ZorkDotNet.Tests/TestLogs/` (the folder is wiped when the test class loads), which helps when debugging a failing scenario. `GameRunner` doesn't run the intro `LOOK`. To test an area far from the start, use `runner.Teleport(roomId)` and `runner.Give(objectId)` instead of walking there and collecting things. `GetOutputSince(mark)` returns only what one command printed. Put tests for one puzzle or feature in their own file (e.g. `GratingTests.cs`).

### Checking against the original (`ZorkDotNet.Tests/Original/`)

- `dung.56` is the original dungeon data, copied verbatim (MIT No Attribution; source and commit are in `Original/README.md`). Don't edit it. `.gitattributes` keeps its bytes unchanged.
- `MdlReader` parses MDL, and `OriginalDungeon` extracts rooms, exits, objects (`#OBJECT`, `AOBJECT`, `SOBJECT`), verbs and directions. `WorldComparer` compares these with a freshly built `World`. For verbs it runs each original word through `Parser.Execute` and checks the parser doesn't reply "I don't know the word".
- `OriginalWorldTests.World_matches_original_except_known_differences` checks the result against `Original/KnownDifferences.txt`, which is the remaining to-do list. The test fails on a **new** difference (a regression, or data that doesn't match the original) and on a listed difference that **no longer occurs**.
- After fixing something, delete its line from `KnownDifferences.txt`. Regenerate the whole file with `ZORK_UPDATE_KNOWN_DIFFERENCES=1 dotnet test ZorkDotNet.sln` and check the git diff: it should only remove lines, unless you meant to add one.
- The comparer covers world data and vocabulary only. Logic (`act*.37/38`, `act2.27`, `act3.13`, clock events, demons) still needs behavior tests through `GameRunner`.
