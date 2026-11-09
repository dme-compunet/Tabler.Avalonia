using System.Text.Json;
using System.Text.RegularExpressions;
using Generator.Icons.Utilities;

namespace Generator.Icons;

/// <summary>
/// Generates <c>icons/tabler-icons.txt</c> (icon geometry and metadata) from a tabler-icons
/// source tree (<c>icons/outline</c>, <c>icons/filled</c>).
/// </summary>
/// <remarks>
/// Ids of icons that already exist in the output file are kept; new icons get fresh ids,
/// so the enum values of existing icons never change between versions.
/// </remarks>
public sealed partial class GenerateCommand
{
    /// <summary>Root of the tabler-icons source tree.</summary>
    public required string InputPath { get; init; }

    /// <summary>The icons text file; read for the existing ids, then overwritten.</summary>
    public required string OutputPath { get; init; }

    /// <summary>The source label, e.g. "tabler-icons v3.51.0". Read from package.json when null.</summary>
    public string? Source { get; init; }

    private sealed record FrontMatter(string? Category, string[] Tags);

    public IconCatalog Execute()
    {
        var outlineDir = Path.Combine(InputPath, "icons", "outline");
        var filledDir = Path.Combine(InputPath, "icons", "filled");

        if (!Directory.Exists(outlineDir) || !Directory.Exists(filledDir))
        {
            throw new DirectoryNotFoundException($"Expected '{outlineDir}' and '{filledDir}'.");
        }

        var previous = File.Exists(OutputPath) ? IconCatalog.Read(OutputPath) : null;
        var source = Source ?? ReadSourceFromPackageJson() ?? previous?.Source ?? "unknown";

        Console.WriteLine($"Generating icons from {source}...");

        var outlineFiles = GetSvgFiles(outlineDir);
        var filledFiles = GetSvgFiles(filledDir);

        // Filled icons carry no tags/category; they inherit them from their outline twin.
        var outlineMeta = outlineFiles.ToDictionary(f => Path.GetFileNameWithoutExtension(f), ReadFrontMatter);

        var sources = new List<(string Path, string Variant, FrontMatter Meta)>(outlineFiles.Length + filledFiles.Length);

        foreach (var file in outlineFiles)
        {
            sources.Add((file, "outline", outlineMeta[Path.GetFileNameWithoutExtension(file)]));
        }

        foreach (var file in filledFiles)
        {
            var own = ReadFrontMatter(file);
            var meta = outlineMeta.TryGetValue(Path.GetFileNameWithoutExtension(file), out var o)
                ? new FrontMatter(own.Category ?? o.Category, own.Tags.Length > 0 ? own.Tags : o.Tags)
                : own;

            sources.Add((file, "filled", meta));
        }

        // Converting ~6,000 SVGs is CPU-bound and every file is independent.
        var converted = new TablerIconInfo[sources.Count];

        Parallel.For(0, sources.Count, i =>
        {
            var (path, variant, meta) = sources[i];
            converted[i] = CreateIconInfo(path, variant, meta, id: -1);
        });

        var duplicates = converted.GroupBy(i => i.KindName).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate enum names: {string.Join(", ", duplicates)}");
        }

        var empty = converted.Where(i => i.Stroke.Length == 0 && i.Fills.Length == 0).Select(i => i.KindName).ToArray();

        if (empty.Length > 0)
        {
            Console.WriteLine($"WARNING: {empty.Length} icons produced no geometry: {string.Join(", ", empty)}");
        }

        var catalog = AssignIds(converted, previous, source);

        Console.WriteLine($"Writing {catalog.Icons.Length} icons to {OutputPath}...");
        catalog.Write(OutputPath);

        return catalog;
    }

    private static IconCatalog AssignIds(TablerIconInfo[] icons, IconCatalog? previous, string source)
    {
        var existingIds = previous?.Icons.ToDictionary(i => i.KindName, i => i.Id) ?? [];
        var nextId = previous?.NextId ?? 0;

        var result = new List<TablerIconInfo>(icons.Length);

        // Known icons keep their id. New ones are appended: outline first, then filled, by name
        // (the same order the first generation used).
        foreach (var icon in icons.OrderBy(i => existingIds.ContainsKey(i.KindName) ? 0 : 1)
                                  .ThenBy(i => i.IsFilled)
                                  .ThenBy(i => i.Name, StringComparer.Ordinal))
        {
            var id = existingIds.TryGetValue(icon.KindName, out var existing) ? existing : nextId++;

            result.Add(new TablerIconInfo
            {
                Id = id,
                Name = icon.Name,
                Variant = icon.Variant,
                Stroke = icon.Stroke,
                Fills = icon.Fills,
                Category = icon.Category,
                Tags = icon.Tags,
            });
        }

        return new IconCatalog
        {
            Source = source,
            NextId = nextId,
            Icons = [.. result.OrderBy(i => i.Id)],
        };
    }

    private string? ReadSourceFromPackageJson()
    {
        var path = Path.Combine(InputPath, "package.json");

        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement.TryGetProperty("version", out var version)
            ? IconCatalog.SourcePrefix + "v" + version.GetString()
            : null;
    }

    private static string[] GetSvgFiles(string directory) =>
        [.. Directory.GetFiles(directory, "*.svg").OrderBy(Path.GetFileName, StringComparer.Ordinal)];

    private static TablerIconInfo CreateIconInfo(string path, string variant, FrontMatter meta, int id)
    {
        var geometry = SvgToGeometryConverter.GenerateFromFile(path);

        return new TablerIconInfo
        {
            Id = id,
            Name = Path.GetFileNameWithoutExtension(path),
            Variant = variant,
            Stroke = geometry.Stroke,
            Fills = geometry.Fills,
            Category = meta.Category ?? "",
            Tags = meta.Tags,
        };
    }

    /// <summary>
    /// Tabler stores metadata in a leading XML comment:
    /// <code>
    /// &lt;!--
    /// tags: [home, house]
    /// category: Buildings
    /// --&gt;
    /// </code>
    /// </summary>
    private static FrontMatter ReadFrontMatter(string path)
    {
        var text = File.ReadAllText(path);
        var comment = CommentRegex().Match(text);

        if (!comment.Success)
        {
            return new FrontMatter(null, []);
        }

        var body = comment.Groups[1].Value;

        var category = CategoryRegex().Match(body) is { Success: true } c ? c.Groups[1].Value.Trim().Trim('"') : null;

        var tags = TagsRegex().Match(body) is { Success: true } t
            ? t.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                               .Select(x => x.Trim('"', '\'').ToLowerInvariant())
                               .Where(x => x.Length > 0)
                               .ToArray()
            : [];

        return new FrontMatter(category, tags);
    }

    [GeneratedRegex(@"^\s*<!--(.*?)-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"^\s*category:\s*(.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex CategoryRegex();

    [GeneratedRegex(@"^\s*tags:\s*\[(.*?)\]", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex TagsRegex();
}
