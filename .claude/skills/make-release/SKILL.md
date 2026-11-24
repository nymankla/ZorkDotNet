---
name: make-release
description: Release checklist for ZorkDotNet - pick the version, tag, and watch the Build and test workflow until the GitHub release has its seven Windows, Linux and macOS zips. Use when the user asks for a release, a new version or a tag.
---

# Make a release

`.github/workflows/dotnet-desktop.yml` ("Build and test") runs lint and tests, publishes Native AOT executables for
win-x64/arm64, linux-x64/arm64 and osx-x64/arm64 (each on its own OS runner, with a smoke test) plus a trimmed
single-file linux-arm, and, on a `v*` tag (or a manual run with a version), creates a GitHub release with `ZorkDotNet-<rid>.zip` for each and generated release notes. There is no
version in the project file and no changelog: the tag is the version.

## 1. Pick the number

- `gh release list --limit 5` and `git tag --sort=-v:refname | head` show the last version. No release yet means the
  first is `v0.1.0` unless the user names one.
- `git log --oneline <last tag>..main` shows what's new. Nothing new means nothing to release: say so and stop.
- Newly ported features or puzzles → minor. Only fixes → patch. Before 1.0 a breaking change (old saves no longer
  restore) is a minor bump too. A number the user gave wins.

## 2. Check before tagging

- Working tree clean, on `main`, up to date: `git fetch origin && git status -sb`.
- `dotnet format ZorkDotNet.sln --verify-no-changes` and `dotnet test ZorkDotNet.sln -c Release` pass locally.
- The last CI run on `main` is green: `gh run list --workflow dotnet-desktop.yml --branch main --limit 1`.

## 3. Tag

Ask the user before pushing the tag: it publishes a release.

```bash
git tag -a vx.y.z -m "ZorkDotNet x.y.z"
git push origin vx.y.z
```

## 4. Watch it

```bash
id=$(gh run list --workflow dotnet-desktop.yml --event push --limit 1 --json databaseId -q '.[0].databaseId')
gh run watch "$id" --exit-status     # run in the background; it takes a few minutes
gh release view vx.y.z --json url,assets -q '.url, (.assets[] | .name + " " + (.size|tostring))'
```

Expect seven assets: `ZorkDotNet-win-x64.zip`, `-win-arm64`, `-linux-x64`, `-linux-arm64`, `-linux-arm`, `-osx-x64`
and `-osx-arm64`.

## If the run fails

- Read the failure: `gh run view <id> --log-failed | rg -i "fail|error|expected|actual"`.
- Fix it on a branch with a normal pull request (with a test when it is a bug).
- If **no release was published** (`gh release view vx.y.z` fails), move the tag to the fixed commit. Deleting a
  pushed tag needs the user's OK first:
  ```bash
  git tag -d vx.y.z && git push origin :refs/tags/vx.y.z
  git tag -a vx.y.z -m "ZorkDotNet x.y.z" && git push origin vx.y.z
  ```
- If a release **was** published, never move its tag; release the next patch version instead.

## Do not

- Don't release with failing tests or a red CI on `main`.
- Don't change the workflow as part of a release; that is its own change.
