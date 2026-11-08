<p align="center">
  <img src="assets/logo.svg" width="96" alt="Tabler logo" />
</p>

# Tabler.Avalonia (feasibility draft)

[Tabler Icons](https://tabler.io/icons) for AvaloniaUI, modeled on Lucide.Avalonia.
This is a proof-of-concept: no CI, auto-update, or release pipeline yet.

## Usage

```xml
<TablerIcon Kind="Home" Size="24" StrokeWidth="2" />
<TablerIcon Kind="HomeFilled" Foreground="Red" />
<Button Content="{TablerIconContent Search}" />
```

Outline icons use the Tabler name in PascalCase (`arrow-left` → `ArrowLeft`);
filled icons get a `Filled` suffix (`home` filled → `HomeFilled`).

```csharp
var meta = TablerIconKind.Home.GetMetadata(); // Name, Variant, Category, Tags
var data = TablerIconKind.Home.GetData();     // raw stroke / fill path data
```

## Layout

```
assets/                         official Tabler logo (svg) + rendered icon.png (NuGet) / logo.ico (gallery)
icons/tabler-icons.txt          generated source of truth (geometry + metadata)
src/tools/Generator.Icons       SVG -> path data converter
src/build/Icons.targets         turns the .txt into embedded .bin + TablerIconKind.g.cs at build time
src/Tabler.Avalonia             the library
src/Tabler.Avalonia.Gallery     gallery app (SukiUI as a NuGet package)
```

## Regenerating from the tabler-icons repo

```bash
dotnet run --project src/tools/Generator.Icons -c Release -- generate D:\source\github\tabler-icons icons/tabler-icons.txt
```

Then build normally; `Icons.targets` rebuilds the binaries when the .txt changes.

```bash
dotnet run --project src/Tabler.Avalonia.Gallery
```

## Gallery architecture

The gallery follows the current Avalonia 12 guidance and uses no ReactiveUI:

- **MVVM:** `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]` partial properties, `[RelayCommand]`).
  Search is debounced with a `CancellationTokenSource`; matching runs on the thread pool.
- **DI:** `Microsoft.Extensions.DependencyInjection`. The clipboard, toast, theme and copy services
  sit behind interfaces, and the view models never reference SukiUI.
- **Views:** compiled bindings only. The code-behind does nothing beyond wiring. View-only behavior
  lives in attached properties under `Behaviors/`:
  - `ScrollViewerAssist`: jumps back to the top when results change.
  - `UniformGridAssist`: keeps the grid's cell size in sync with the icon size.
- **Items:** `IconViewModel` is immutable. Size and stroke width are bound from the window's view
  model, so a slider change doesn't touch 6,000+ objects.
- **DevTools:** `AvaloniaUI.DiagnosticsSupport` with `.WithDeveloperTools()` (Debug only). It replaces
  the removed `Avalonia.Diagnostics`. To use it, install the client tool with
  `dotnet tool install --global AvaloniaUI.DeveloperTools` and sign in with an Avalonia account.

## Feasibility findings

The Lucide converter approach (Svg.Skia + SvgToXaml -> `SKPath.ToSvgPathData()`) **works for Tabler**:

| | |
|---|---|
| Icons converted | 6,257 (5,203 outline + 1,054 filled), 0 failures, ~10 s |
| SVG elements | `<path>` only, no transforms, no `<g>` |
| Embedded size | `icons.bin` ~1 MB + `metadata.bin` ~0.1 MB (Brotli), DLL ~1.3 MB |

Differences from Lucide that the draft handles:

1. **Stroke and fill in one icon.** Lucide is stroke-only. Tabler filled icons are fill-only,
   and 81 outline icons contain filled sub-paths (dots and similar). The converter separates
   drawings by `SKPaint.Style` into one stroke geometry plus a list of fill geometries, and
   `TablerIcon` draws both.
2. **Each fill path is kept as its own geometry.** 229 filled icons have several `<path>`s. If
   they were merged into one geometry, overlapping parts with opposite winding would cut holes.
3. **Metadata** comes from the SVG's leading `<!-- tags: [...] category: ... -->` comment.
   Filled icons have none, so they inherit tags and category from their outline twin.
4. **Default stroke width is 2** (Tabler's default), not Lucide's 1.8.
5. **The gallery is virtualized** (`ItemsRepeater` + `UniformGridLayout`). Lucide's
   non-virtualized `WrapPanel` froze with ~6,300 cards.

### Known gaps / next steps

- Skia turns SVG arcs into many `Q` segments, so the text file is large (~13 MB). Avalonia can
  parse SVG path data, including arcs, natively. Storing the original `d` strings would be
  smaller and exact, but it needs a check that Avalonia's parser accepts Tabler's compact syntax
  (`.5.5`, flag packing).
- One outline icon uses `opacity=".5"` on a sub-path. The draft ignores it.
- The `SukiWindow.LogoContent` logo doesn't show in the title bar (SukiUI 6 behavior, not investigated).
- Not done yet: aliases (`aliases.json`), auto-update workflow, NuGet packaging, release notes.
