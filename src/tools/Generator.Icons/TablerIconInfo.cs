namespace Generator.Icons;

public class TablerIconInfo
{
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

    /// <summary>The enum member name, e.g. "Home" / "HomeFilled".</summary>
    public string KindName => KebabToPascal(Name) + (Variant == "filled" ? "Filled" : "");

    private const string FillSeparator = "|";

    public static TablerIconInfo[] ReadArrayFromFile(string path)
    {
        var lines = File.ReadAllLines(path);
        var icons = new List<TablerIconInfo>();

        static string RemovePrefix(string value, string prefix)
        {
            if (!value.StartsWith(prefix))
            {
                throw new InvalidOperationException($"Expected '{prefix}' but got '{value}'.");
            }

            return value[prefix.Length..].Trim();
        }

        static string[] SplitList(string value, string separator) =>
            value.Length == 0 ? [] : value.Split(separator);

        for (var i = 0; i + 5 < lines.Length; i += 7)
        {
            icons.Add(new TablerIconInfo
            {
                Name = RemovePrefix(lines[i], "name:"),
                Variant = RemovePrefix(lines[i + 1], "variant:"),
                Stroke = RemovePrefix(lines[i + 2], "stroke:"),
                Fills = SplitList(RemovePrefix(lines[i + 3], "fill:"), FillSeparator),
                Category = RemovePrefix(lines[i + 4], "category:"),
                Tags = SplitList(RemovePrefix(lines[i + 5], "tags:"), ","),
            });
        }

        return [.. icons];
    }

    public static void WriteArrayToFile(TablerIconInfo[] icons, string path)
    {
        using var writer = new StreamWriter(File.Create(path));

        foreach (var icon in icons)
        {
            writer.WriteLine("name:     " + icon.Name);
            writer.WriteLine("variant:  " + icon.Variant);
            writer.WriteLine("stroke:   " + icon.Stroke);
            writer.WriteLine("fill:     " + string.Join(FillSeparator, icon.Fills));
            writer.WriteLine("category: " + icon.Category);
            writer.WriteLine("tags:     " + string.Join(',', icon.Tags));
            writer.WriteLine();
        }
    }

    public static string KebabToPascal(string input)
    {
        var sb = new System.Text.StringBuilder(input.Length);
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
