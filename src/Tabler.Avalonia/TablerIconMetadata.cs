namespace Tabler.Avalonia;

public enum TablerIconVariant
{
    Outline,
    Filled,
}

public class TablerIconMetadata
{
    public required TablerIconKind Kind { get; init; }

    /// <summary>Original Tabler icon name (kebab-case), e.g. "arrow-left".</summary>
    public required string Name { get; init; }

    public required TablerIconVariant Variant { get; init; }

    public required string Category { get; init; }

    public required string[] Tags { get; init; }

    /// <summary>
    /// Checks whether the icon matches a search value. Lower priority = better match.
    /// </summary>
    public bool Contains(string? value, out int priority)
    {
        priority = 0;

        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        value = value.Trim().ToLowerInvariant();

        var kind = Kind.ToString().ToLowerInvariant();
        var compact = value.Replace(" ", "").Replace("-", "");

        if (Name == value || kind == compact)
        {
            return true;
        }

        if (Name.StartsWith(value) || kind.StartsWith(compact))
        {
            priority = 1;
            return true;
        }

        if (Name.Contains(value) || kind.Contains(compact))
        {
            priority = 2;
            return true;
        }

        if (Category.Contains(value, StringComparison.OrdinalIgnoreCase))
        {
            priority = 3;
            return true;
        }

        foreach (var tag in Tags)
        {
            if (tag.Contains(value))
            {
                priority = 4;
                return true;
            }
        }

        return false;
    }
}
