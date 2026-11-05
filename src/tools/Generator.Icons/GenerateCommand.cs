using System.Text.RegularExpressions;
using Generator.Icons.Utilities;

namespace Generator.Icons;

/// <summary>
/// Generates a text file containing icon geometry and metadata
/// from the original tabler-icons repository (icons/outline, icons/filled).
/// </summary>
public partial class GenerateCommand
{
    /// <summary>Root path of the tabler-icons repository.</summary>
    public required string InputPath { get; init; }

    /// <summary>Output path of the generated text file.</summary>
    public required string OutputPath { get; init; }

    private sealed record FrontMatter(string? Category, string[] Tags);

    public void Execute()
    {
        var outlineDir = Path.Combine(InputPath, "icons", "outline");
        var filledDir = Path.Combine(InputPath, "icons", "filled");

        if (!Directory.Exists(outlineDir) || !Directory.Exists(filledDir))
        {
            throw new DirectoryNotFoundException($"Expected '{outlineDir}' and '{filledDir}'.");
        }

        Console.WriteLine("Generating icons...");

        var outlineFiles = Directory.GetFiles(outlineDir, "*.svg").OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
        var filledFiles = Directory.GetFiles(filledDir, "*.svg").OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();

        // Filled icons carry no tags/category; they inherit them from their outline twin.
        var outlineMeta = outlineFiles.ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => ReadFrontMatter(f));

        var icons = new List<TablerIconInfo>(outlineFiles.Length + filledFiles.Length);

        foreach (var file in outlineFiles)
        {
            icons.Add(CreateIconInfo(file, "outline", outlineMeta[Path.GetFileNameWithoutExtension(file)!]));
        }

        foreach (var file in filledFiles)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var own = ReadFrontMatter(file);
            var meta = outlineMeta.TryGetValue(name, out var o)
                ? new FrontMatter(own.Category ?? o.Category, own.Tags.Length > 0 ? own.Tags : o.Tags)
                : own;

            icons.Add(CreateIconInfo(file, "filled", meta));
        }

        var empty = icons.Where(i => i.Stroke.Length == 0 && i.Fills.Length == 0).Select(i => i.Name).ToArray();
        if (empty.Length > 0)
        {
            Console.WriteLine($"WARNING: {empty.Length} icons produced no geometry: {string.Join(", ", empty)}");
        }

        var duplicates = icons.GroupBy(i => i.KindName).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate enum names: {string.Join(", ", duplicates)}");
        }

        Console.WriteLine($"Writing {icons.Count} icons to {OutputPath}...");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutputPath))!);
        TablerIconInfo.WriteArrayToFile([.. icons], OutputPath);
    }

    private static TablerIconInfo CreateIconInfo(string path, string variant, FrontMatter meta)
    {
        var geometry = SvgToGeometryConverter.GenerateFromFile(path);

        return new TablerIconInfo
        {
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
                               .Select(x => x.Trim('"', '\'').Replace(",", " ").ToLowerInvariant())
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
