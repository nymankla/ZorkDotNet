# Checking against the original (`ZorkDotNet.Tests/Original/`)

- `dung.56` is the original dungeon data, copied verbatim (MIT No Attribution; source and commit are in `Original/README.md`). Don't edit it. `.gitattributes` keeps its bytes unchanged.
- `MdlReader` parses MDL, and `OriginalDungeon` extracts rooms, exits, objects (`#OBJECT`, `AOBJECT`, `SOBJECT`), verbs and directions. `WorldComparer` compares these with a freshly built `World`. For verbs it runs each original word through `Parser.Execute` and checks the parser doesn't reply "I don't know the word".
- `OriginalWorldTests.World_matches_original_except_known_differences` checks the result against `Original/KnownDifferences.txt`, which is the remaining to-do list. The test fails on a **new** difference (a regression, or data that doesn't match the original) and on a listed difference that **no longer occurs**.
- After fixing something, delete its line from `KnownDifferences.txt`. Regenerate the whole file with `ZORK_UPDATE_KNOWN_DIFFERENCES=1 dotnet test ZorkDotNet.sln` and check the git diff: it should only remove lines, unless you meant to add one.
- The comparer covers world data and vocabulary only. Logic (`act*.37/38`, `act2.27`, `act3.13`, clock events, demons) still needs behavior tests through `GameRunner`.
