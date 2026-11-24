---
name: finish-change
description: Check that a change to ZorkDotNet is really finished before it is committed - tests that fail without the change, lint and build, the KnownDifferences list, save/restore coverage, trim safety, a played check, and the CLAUDE.md sections this particular diff requires. Use when asked to finish, wrap up or check a change, before "commit" or "commit & pr", and after porting a feature or fixing a bug.
---

Works out from the diff what this change has to update, updates it, and runs the checks. It doesn't commit or push
unless the user asked for that. `CLAUDE.md` holds the rules; this is how to apply them to one diff.

## 1. See what changed

```bash
git status --short
git diff --stat
```

Never stage anything from `bin/`, `obj/`, `ZorkDotNet.Tests/TestLogs/` or a save file. Stage files by name; never
`git add -A` or `git add .`.

## 2. Go through what the diff requires

| If the diff... | Then also |
|---|---|
| changes anything in `ZorkDotNet/Game/` | A test that fails without the change, in the feature's own test file (`GratingTests.cs`, `LightingTests.cs`, ...). A puzzle gets a test that plays the whole solution |
| ports behavior from the MDL source | A comment naming the MDL file and routine (`act1.37` `TROLL`, `np.93` `SPARSE`, ...). If the port deliberately differs, the comment says why |
| fixes a line in `ZorkDotNet.Tests/Original/KnownDifferences.txt` | Delete that line. `OriginalWorldTests` fails if a listed difference no longer occurs. After a regenerate the diff should only remove lines (see `ZorkDotNet.Tests/CLAUDE.md`) |
| adds state that changes during play (a flag, object field, room field, villain, clock) | `GameSave.Capture`/`Apply`, and `SaveRestoreTests` still round-trips exactly |
| adds a clock event | A constant in `Clocks`, a case in `Clocks.Run`, an entry in `Known`; schedule with `state.Schedule`, never a lambda |
| adds a verb | `AddWords(...)` in `VerbSyntax.cs` and a `case` in `Parser.Dispatch` |
| adds a blocked exit | `ExitTarget.Blocked(message)`, not a bare string (that is read as a room id) |
| writes output | Through `state.Output`, never `Console` |
| moves the player | Doesn't touch `Moves`; only `Parser.PlayTurn` counts a turn |
| uses reflection, `Type.GetType`, reflection JSON or `Activator` | Rewrite it: the release build is Native AOT. New JSON types go in `GameSave`'s source-generated context |
| changes how a part works (a file in the Architecture list, a new file, the save format) | The **Architecture** section of `CLAUDE.md` |
| completes an item on the port tracking issue | Say which item, so it can be ticked off on https://github.com/nymankla/ZorkDotNet/issues/1 |

`ZorkDotNet.Tests/Original/dung.56` is never edited.

## 3. Run the checks

```bash
dotnet build ZorkDotNet.sln
dotnet format ZorkDotNet.sln --verify-no-changes
dotnet test ZorkDotNet.sln
```

Report the test totals. The build is the lint: warnings are errors, analyzers at `11.0-recommended`, code style
enforced. Fix the code; don't suppress a rule or edit `.editorconfig` to get around it. `dotnet format ZorkDotNet.sln`
fixes formatting.

For a change to save/restore, JSON, or anything AOT-sensitive, also publish Native AOT once and play it, including
a SAVE and a RESTORE (run it from the publish folder; the save file lands in the current directory):

```bash
dotnet publish ZorkDotNet/ZorkDotNet.csproj -c Release -r win-x64 -o <scratchpad>/aot
```

## 4. Play it

Tests check substrings; also read the real text once. `Program` reads stdin until EOF, so pipe in the commands that
reach the change:

```bash
printf 'NORTH\nEAST\nOPEN WINDOW\nWEST\n' | dotnet run --project ZorkDotNet
```

For an area far from the start, read the transcript the test wrote to `ZorkDotNet.Tests/TestLogs/` instead. Check the
wording against the MDL source, not just that something was printed.

## 5. Report

Say what passed, what was updated, what was left out and what could not be verified. Don't report a check as passed
if it was skipped.
