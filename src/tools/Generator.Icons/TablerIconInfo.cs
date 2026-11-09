using System.Text;

namespace Generator.Icons;

public sealed class TablerIconInfo
{
    /// <summary>
    /// Stable numeric value of the enum member. Assigned once and never changed or reused,
    /// so code compiled against an older package version keeps pointing at the same icon.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>Original Tabler file name (kebab-case, without extension).</summary>
    public required string Name { get; init; }

    /// <summary>"outline" or "filled".</summary>
    public required string Variant { get; init; }

    /// <summary>Path data drawn with a pen (may be empty).</summary>
    public required string Stroke { get; init; }

    /// <summary>Path data drawn with a brush, one entry per SVG path (may be empty).</summary>
    public required string[] Fills { get; init; }

    public required string Category { get; init; }

    public required string[] Tags { get; init; }

    public bool IsFilled => Variant == "filled";

    /// <summary>The enum member name, e.g. "Home" / "HomeFilled".</summary>
    public string KindName => KebabToPascal(Name) + (IsFilled ? "Filled" : "");

    public bool HasSameGeometry(TablerIconInfo other) =>
        Stroke == other.Stroke && Fills.AsSpan().SequenceEqual(other.Fills);

    public static string KebabToPascal(string input)
    {
        var sb = new StringBuilder(input.Length);
        var nextUpper = true;

        foreach (var c in input)
        {
            if (c == '-')
            {
                nextUpper = true;
            }
            else
            {
                sb.Append(nextUpper ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                nextUpper = false;
            }
        }

        // C# identifiers can't start with a digit (no current Tabler icon does, but be safe).
        return sb.Length > 0 && char.IsDigit(sb[0]) ? "_" + sb : sb.ToString();
    }
}

/// <summary>
/// The content of <c>icons/tabler-icons.txt</c>: the generated source of truth for the library.
/// </summary>
/// <remarks>
/// The file starts with a header block, followed by one block per icon, ordered by id.
/// Blocks are separated by blank lines and every line is <c>key: value</c>:
/// <code>
/// source:   tabler-icons v3.51.0
/// next-id:  6265
///
/// id:       0
/// name:     a-b-2
/// variant:  outline
/// stroke:   M16 21L19 21...
/// fill:     F1 M...|F0 M...
/// category: Text
/// tags:     ab-test,split-test
/// </code>
/// New icons are appended at the end, so updates produce small, readable diffs.
/// </remarks>
public sealed class IconCatalog
{
    private const string FillSeparator = "|";

    /// <summary>Where the icons came from, e.g. "tabler-icons v3.51.0".</summary>
    public required string Source { get; init; }

    /// <summary>The id the next new icon gets (ids of removed icons are never reused).</summary>
    public required int NextId { get; init; }

    /// <summary>Icons ordered by id.</summary>
    public required TablerIconInfo[] Icons { get; init; }

    /// <summary>The Tabler version part of <see cref="Source"/> (e.g. "v3.51.0"), or null.</summary>
    public string? SourceVersion => Source.StartsWith(SourcePrefix, StringComparison.Ordinal) ? Source[SourcePrefix.Length..] : null;

    public const string SourcePrefix = "tabler-icons ";

    public static IconCatalog Read(string path) => Parse(File.ReadAllText(path));

    public static IconCatalog Parse(string text)
    {
        var blocks = text
            .ReplaceLineEndings("\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseBlock)
            .Where(b => b.Count > 0)
            .ToList();

        var header = blocks.Count > 0 && !blocks[0].ContainsKey("name") ? blocks[0] : null;

        if (header != null)
        {
            blocks.RemoveAt(0);
        }

        var icons = new TablerIconInfo[blocks.Count];

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];

            string Get(string key) => block.TryGetValue(key, out var value)
                ? value
                : throw new FormatException($"Icon block {i} has no '{key}' line.");

            icons[i] = new TablerIconInfo
            {
                // Files written before ids were introduced: the position was the enum value.
                Id = block.TryGetValue("id", out var id) ? int.Parse(id) : i,
                Name = Get("name"),
                Variant = Get("variant"),
                Stroke = Get("stroke"),
                Fills = SplitList(Get("fill"), FillSeparator),
                Category = Get("category"),
                Tags = SplitList(Get("tags"), ","),
            };
        }

        Array.Sort(icons, (a, b) => a.Id.CompareTo(b.Id));

        for (var i = 1; i < icons.Length; i++)
        {
            if (icons[i].Id == icons[i - 1].Id)
            {
                throw new FormatException($"Duplicate icon id {icons[i].Id} ('{icons[i - 1].Name}' and '{icons[i].Name}').");
            }
        }

        var maxId = icons.Length == 0 ? -1 : icons[^1].Id;

        return new IconCatalog
        {
            Source = header?.GetValueOrDefault("source") ?? "unknown",
            NextId = Math.Max(header?.GetValueOrDefault("next-id") is { } next ? int.Parse(next) : 0, maxId + 1),
            Icons = icons,
        };
    }

    public void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        // Always LF, so the file is identical no matter which OS generated it.
        using var writer = new StreamWriter(File.Create(path), new UTF8Encoding(false)) { NewLine = "\n" };

        writer.WriteLine("source:   " + Source);
        writer.WriteLine("next-id:  " + NextId);
        writer.WriteLine();

        foreach (var icon in Icons)
        {
            writer.WriteLine("id:       " + icon.Id);
            writer.WriteLine("name:     " + icon.Name);
            writer.WriteLine("variant:  " + icon.Variant);
            writer.WriteLine("stroke:   " + icon.Stroke);
            writer.WriteLine("fill:     " + string.Join(FillSeparator, icon.Fills));
            writer.WriteLine("category: " + icon.Category);
            writer.WriteLine("tags:     " + string.Join(',', icon.Tags));
            writer.WriteLine();
        }
    }

    private static Dictionary<string, string> ParseBlock(string block)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in block.Split('\n'))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var colon = line.IndexOf(':');

            if (colon <= 0)
            {
                throw new FormatException($"Expected 'key: value' but got '{line}'.");
            }

            result[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        return result;
    }

    private static string[] SplitList(string value, string separator) =>
        value.Length == 0 ? [] : value.Split(separator);
}
