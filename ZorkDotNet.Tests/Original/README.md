# Original source (reference data)

`dung.56` is copied verbatim from the 1977 MIT Zork source:

- Repository: https://github.com/MITDDC/zork
- Path: `zork/dung.56`
- Commit: `d94501340842e006791fce0757dc0043b8ec8916` (2022-08-10)
- License: MIT No Attribution (see https://github.com/MITDDC/zork/blob/master/LICENSE.md)

It holds the dungeon data: rooms, exits, objects, verbs and global strings.
`OriginalWorldTests` parses it (`MdlReader`, `OriginalDungeon`) and compares it with the port's `World` (`WorldComparer`).

## KnownDifferences.txt

Every difference between the port and `dung.56` that hasn't been fixed yet is listed in `KnownDifferences.txt`. It works as the to-do list for a complete port.

- A difference that isn't in the file fails the test. That's a regression, or new data that doesn't match the original.
- A line in the file that no longer happens also fails the test. Delete the line once the difference is fixed.
- To regenerate the file after deliberate changes, run the tests with `ZORK_UPDATE_KNOWN_DIFFERENCES=1`, then review the diff before committing.
