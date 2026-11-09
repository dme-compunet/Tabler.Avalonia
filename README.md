<p align="center">
  <img src="https://raw.githubusercontent.com/dme-compunet/Tabler.Avalonia/master/assets/logo.png" width="96" alt="Tabler logo" />
</p>

<h1 align="center">Tabler.Avalonia</h1>

<p align="center">
  <a href="https://tabler.io/icons">Tabler Icons</a> for <a href="https://avaloniaui.net">AvaloniaUI</a>: 6,000+ outline and filled icons.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/Tabler.Avalonia"><img src="https://img.shields.io/nuget/v/Tabler.Avalonia?logo=nuget" alt="NuGet" /></a>
  <a href="https://github.com/dme-compunet/Tabler.Avalonia/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/dme-compunet/Tabler.Avalonia/ci.yml?branch=master&label=CI" alt="CI" /></a>
  <a href="https://github.com/dme-compunet/Tabler.Avalonia/blob/master/LICENSE"><img src="https://img.shields.io/github/license/dme-compunet/Tabler.Avalonia" alt="License" /></a>
</p>

## Features

- **No setup:** no styles to include and no `xmlns` to declare.
- **Compact:** the whole set is embedded as ~0.4 MB of compressed data. Icons are decoded on first use.
- **Outline and filled** variants, including outline icons with filled details.
- **Always up to date:** a weekly job picks up new Tabler Icons releases and publishes a new version.
- **Stable enum values:** an icon keeps its numeric value across versions (see [Versioning](#versioning)).
- **Trimming and NativeAOT** compatible.

## Installation

```bash
dotnet add package Tabler.Avalonia
```

## Usage

### The `TablerIcon` control

```xml
<TablerIcon Kind="Home" />
<TablerIcon Kind="Home" Size="32" StrokeWidth="1.5" />
<TablerIcon Kind="HomeFilled" Foreground="Red" />
```

`Size` defaults to 24 and `StrokeWidth` to 1.8. `Foreground` is inherited, like text.

### The markup extension

```xml
<Button Content="{TablerIcon Search}" />
<Button Content="{TablerIcon Search, 20}" />
<Button Content="{TablerIcon Kind=Search, Size=20, StrokeWidth=1.5}" />
```

### Names

Outline icons use the Tabler name in PascalCase (`arrow-left` → `ArrowLeft`). Filled icons add a
`Filled` suffix (`home` filled → `HomeFilled`). Browse all icons at [tabler.io/icons](https://tabler.io/icons)
or in the gallery app attached to each [release](https://github.com/dme-compunet/Tabler.Avalonia/releases).

### Metadata and raw data

```csharp
var meta = TablerIconKind.Home.GetMetadata(); // Name, Variant, Category, Tags
var data = TablerIconKind.Home.GetData();     // raw stroke / fill path data (24×24)
```

## Versioning

Versions follow [SemVer](https://semver.org), with `TablerIconKind` as the public API:

| Change in Tabler Icons | Version bump |
|---|---|
| Icons removed or renamed | major (minor while 0.x) |
| Icons added | minor |
| Drawings or search tags changed only | patch |

Release notes list the added, modified and removed icons, followed by the merged pull requests.

Enum values are assigned once and never reused. Code compiled against an older version (for example
a library you depend on) keeps showing the same icons after an update. If an icon was removed, its old
value renders nothing instead of a different icon.

## Repository layout

```
icons/tabler-icons.txt          generated source of truth (geometry, metadata and stable ids)
src/Tabler.Avalonia             the library
src/Tabler.Avalonia.Gallery     gallery app
src/tools/Generator.Icons       SVG → path data converter, binary builder and release notes
src/build/Icons.targets         turns the .txt into embedded binaries + TablerIconKind.g.cs at build time
.github/workflows               CI, weekly icon update, release
```

## Development

Build and run the gallery:

```bash
dotnet run --project src/Tabler.Avalonia.Gallery
```

Update the icons to the latest Tabler Icons release (or pass `--version v3.51.0`):

```bash
dotnet run --project src/tools/Generator.Icons -- update icons/tabler-icons.txt
```

Or from a local [tabler-icons](https://github.com/tabler/tabler-icons) checkout:

```bash
dotnet run --project src/tools/Generator.Icons -- generate ../tabler-icons icons/tabler-icons.txt
```

Then build, and commit `icons/tabler-icons.txt` together with `src/Tabler.Avalonia/TablerIconKind.g.cs`.
CI fails if the two are out of sync.

Preview the release notes for a change:

```bash
git show v0.1.0:icons/tabler-icons.txt > old.txt
dotnet run --project src/tools/Generator.Icons -- changes old.txt icons/tabler-icons.txt
```

## Releasing

- **Automatic:** [`update-icons.yml`](.github/workflows/update-icons.yml) runs every Monday. When Tabler
  published a new release with icon changes, it commits the update, tags the next version and starts
  [`release.yml`](.github/workflows/release.yml).
- **Manual:** push a version tag (`git tag v1.2.3 && git push origin v1.2.3`).

`release.yml` packs the library, publishes it to NuGet with [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing),
creates the GitHub release and attaches the NativeAOT gallery builds.

## License

[MIT](LICENSE). The icons are from [Tabler Icons](https://github.com/tabler/tabler-icons), MIT © Paweł Kuna
(see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)).
