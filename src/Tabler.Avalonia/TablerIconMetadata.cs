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

    private static readonly char[] TermSeparators = [' ', ',', '\t'];

    // Priorities 0-4 are for a match of the whole search phrase (see MatchTerm).
    // Multi-word searches that only match word by word rank after those.
    private const int MultiTermBase = 5;

    // Each missing word costs more than the worst possible sum of per-word priorities,
    // so an icon matching more words always ranks above one matching fewer.
    private const int MissingTermPenalty = 1000;

    /// <summary>
    /// Checks whether the icon matches a search value. Lower priority = better match.
    /// </summary>
    /// <remarks>
    /// A value with several words (e.g. "arrow left circle") first tries the whole phrase.
    /// Otherwise each word is matched on its own: icons matching all the words come first,
    /// then icons matching fewer words; ties are broken by how well each word matched.
    /// </remarks>
    public bool Contains(string? value, out int priority)
    {
        priority = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        value = value.Trim().ToLowerInvariant();

        var kind = Kind.ToString().ToLowerInvariant();

        if (MatchTerm(value, kind, out priority))
        {
            return true;
        }

        var terms = value
            .Split(TermSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToArray();

        if (terms.Length < 2)
        {
            return false;
        }

        var matched = 0;
        var sum = 0;

        foreach (var term in terms)
        {
            if (MatchTerm(term, kind, out var termPriority))
            {
                matched++;
                sum += termPriority;
            }
        }

        if (matched == 0)
        {
            return false;
        }

        priority = MultiTermBase + (terms.Length - matched) * MissingTermPenalty + sum;
        return true;
    }

    /// <summary>
    /// Matches a single (already trimmed and lower-cased) search value against the icon.
    /// Priority: 0 exact name, 1 name prefix, 2 name contains, 3 category, 4 tag.
    /// </summary>
    private bool MatchTerm(string value, string kind, out int priority)
    {
        priority = 0;

        var compact = value.Replace(" ", "").Replace("-", "");

        // A term made only of separators (e.g. "-") would match every icon.
        if (compact.Length == 0)
        {
            return false;
        }

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
            if (tag.Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                priority = 4;
                return true;
            }
        }

        priority = 0;
        return false;
    }
}
