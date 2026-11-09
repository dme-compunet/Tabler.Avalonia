# AGENTS.md

Notes for working on this repository (people and coding agents). User-facing docs are in README.md.

## Layout

```
icons/tabler-icons.txt          generated source of truth: geometry, metadata and stable ids
src/Tabler.Avalonia             the library (the NuGet package)
src/Tabler.Avalonia.Gallery     gallery app (Avalonia 12, SukiUI, CommunityToolkit.Mvvm, NativeAOT)
src/tools/Generator.Icons       CLI: SVG → path data, binary builder, release notes
src/build/Icons.targets         runs the generator during the library build
.github/workflows               ci.yml, update-icons.yml, release.yml
```

## How the build works

`Icons.targets` runs `Generator.Icons build` before the library compiles. It reads `icons/tabler-icons.txt`
and writes:

- `Resources/icons.bin` and `Resources/metadata.bin`: Brotli-compressed, embedded, git-ignored.
- `TablerIconKind.g.cs`: the enum. It is committed, and CI fails if it doesn't match the txt.

The target reruns when the txt, the generator sources or `IconPathCodec.cs` change.
`IconPathCodec.cs` lives in the library and is linked into the generator, so the encoder and decoder
always share one implementation.

## Invariants (don't break these)

- **Enum values are stable.** Each icon has a permanent `id` in the txt, which becomes its enum value.
  Existing ids are never changed. New icons get `next-id` (from the txt header). Ids of removed icons are
  never reused. The binaries are indexed by id, and removed ids are empty slots (they render nothing,
  and `GetMetadata` throws for them).
- **Never edit `tabler-icons.txt` or `TablerIconKind.g.cs` by hand.** Regenerate them with the generator.
- **Generated text is always LF** (enforced by `.gitattributes` too), so output is identical on any OS.
- **Path data stays in the canonical form** the codec expects: commands M/L/C/Q/Z, at most 2 decimals,
  and fills prefixed with `F0 `/`F1 `. `build` verifies the encoding round-trips losslessly.
- The library targets Avalonia 11.3 (lowest supported). Don't raise it without a reason; the gallery
  uses Avalonia 12.

## Generator commands

```bash
# Update to the latest tabler-icons release (or --version v3.51.0; --force to regenerate anyway)
dotnet run --project src/tools/Generator.Icons -- update icons/tabler-icons.txt

# Regenerate from a local tabler-icons checkout
dotnet run --project src/tools/Generator.Icons -- generate ../tabler-icons icons/tabler-icons.txt

# Release notes and next version for a change
git show v0.1.0:icons/tabler-icons.txt > old.txt
dotnet run --project src/tools/Generator.Icons -- changes old.txt icons/tabler-icons.txt --current-version 0.1.0
```

After `update` or `generate`, build the library and commit the txt together with `TablerIconKind.g.cs`.
In GitHub Actions the commands also write step outputs (`$GITHUB_OUTPUT`).

## Build and run

```bash
dotnet build src/Tabler.Avalonia.slnx -c Release
dotnet run --project src/Tabler.Avalonia.Gallery
```

## Versioning and releases

SemVer, with `TablerIconKind` as the public API. `changes` computes the bump:

- removed icons → major (minor while 0.x)
- added icons → minor
- changed drawings or search tags only → patch

Releases:

- **Automatic:** `update-icons.yml` runs every Monday. When there's a new tabler-icons release with
  changes, it commits, creates an annotated `vX.Y.Z` tag (atomic push) and starts `release.yml` with
  `gh workflow run`. Tags pushed with `GITHUB_TOKEN` don't trigger workflows, so it has to start it.
  It doesn't release until a first tag exists.
- **Manual:** `git tag v1.2.3 && git push origin v1.2.3`. Tags with a suffix (`v1.0.0-beta.1`) become
  prereleases.

`release.yml` packs the library, publishes it to nuget.org with Trusted Publishing (`NuGet/login`, the
`NUGET_USER` secret, environment `release`), and creates the GitHub release. The notes are the icon
changes since the previous tag, followed by GitHub's generated notes. It then attaches NativeAOT
gallery builds (win-x64, linux-x64, osx-arm64). The nuget.org trusted publishing policy must name
`release.yml`, so don't rename it.

## Scripts

Keep automation in C# (the generator) plus minimal bash glue in the workflows. Don't add Nushell or
other tools that aren't available on both Linux and Windows.
