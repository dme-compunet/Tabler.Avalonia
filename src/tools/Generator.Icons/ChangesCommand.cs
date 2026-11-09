using System.Text;

namespace Generator.Icons;

/// <summary>
/// Compares two versions of the icons text file: writes a Markdown summary for the release notes
/// and works out the next package version from the kind of change.
/// </summary>
/// <remarks>
/// Version rules (SemVer, the enum is the public API): removed icons are a breaking change
/// (major, or minor while the version is 0.x), added icons are a feature (minor), changed
/// drawings only are a fix (patch).
/// </remarks>
public sealed class ChangesCommand
{
    /// <summary>The previous icons file, or null when there is no previous release.</summary>
    public string? OldPath { get; init; }

    public required string NewPath { get; init; }

    /// <summary>Where to write the Markdown summary (optional; printed either way).</summary>
    public string? MarkdownPath { get; init; }

    /// <summary>The current package version (e.g. "1.2.3" or "v1.2.3"), to compute the next one.</summary>
    public string? CurrentVersion { get; init; }

    /// <summary>Lists longer than this are collapsed into a &lt;details&gt; element.</summary>
    private const int CollapseThreshold = 20;

    private const string IconPageUrl = "https://tabler.io/icons/icon/";
    private const string ReleasesUrl = "https://github.com/tabler/tabler-icons/releases/tag/";

    public void Execute()
    {
        var newCatalog = IconCatalog.Read(NewPath);
        var oldCatalog = OldPath is not null && File.Exists(OldPath) ? IconCatalog.Read(OldPath) : null;

        int added = 0, modified = 0, removed = 0;
        var bump = Bump.None;

        // Without a previous release there is nothing to compare: just describe the icon set.
        var markdown = oldCatalog is null
            ? CreateInitialMarkdown(newCatalog)
            : CreateMarkdown(oldCatalog, newCatalog, out added, out modified, out removed, out bump);

        Console.WriteLine($"Added: {added}, modified: {modified}, removed: {removed}, bump: {bump}");
        Console.WriteLine("----- Markdown -----");
        Console.WriteLine(markdown);

        if (MarkdownPath is not null)
        {
            File.WriteAllText(MarkdownPath, markdown, new UTF8Encoding(false));
        }

        var nextVersion = bump != Bump.None && CurrentVersion is { Length: > 0 } current ? Increment(current, bump) : "";

        if (nextVersion.Length > 0)
        {
            Console.WriteLine($"Next version: {CurrentVersion} -> {nextVersion}");
        }

        ActionsOutput.Set("added", added.ToString());
        ActionsOutput.Set("modified", modified.ToString());
        ActionsOutput.Set("removed", removed.ToString());
        ActionsOutput.Set("has-changes", (bump != Bump.None).ToString().ToLowerInvariant());
        ActionsOutput.Set("bump", bump.ToString().ToLowerInvariant());
        ActionsOutput.Set("next-version", nextVersion);
    }

    private enum Bump { None, Patch, Minor, Major }

    private static string CreateInitialMarkdown(IconCatalog catalog)
    {
        var outline = catalog.Icons.Count(i => !i.IsFilled);
        var filled = catalog.Icons.Length - outline;

        return $"## Icons 🎨\n\n{catalog.Icons.Length:N0} icons ({outline:N0} outline, {filled:N0} filled) from {SourceLink(catalog)}.\n\n";
    }

    private static string CreateMarkdown(IconCatalog oldCatalog, IconCatalog newCatalog,
                                         out int addedCount, out int modifiedCount, out int removedCount, out Bump bump)
    {
        var oldIcons = oldCatalog.Icons.ToDictionary(i => i.KindName);
        var newIcons = newCatalog.Icons.ToDictionary(i => i.KindName);

        var added = newCatalog.Icons.Where(i => !oldIcons.ContainsKey(i.KindName)).OrderBy(i => i.KindName, StringComparer.Ordinal).ToArray();
        var removed = oldCatalog.Icons.Where(i => !newIcons.ContainsKey(i.KindName)).OrderBy(i => i.KindName, StringComparer.Ordinal).ToArray();
        var modified = newCatalog.Icons.Where(i => oldIcons.TryGetValue(i.KindName, out var old) && !old.HasSameGeometry(i))
                                       .OrderBy(i => i.KindName, StringComparer.Ordinal).ToArray();

        // Search metadata only (tags/category): not listed one by one, but still worth a patch release.
        var retagged = newCatalog.Icons.Count(i => oldIcons.TryGetValue(i.KindName, out var old)
                                                   && (old.Category != i.Category || !old.Tags.AsSpan().SequenceEqual(i.Tags)));

        addedCount = added.Length;
        modifiedCount = modified.Length;
        removedCount = removed.Length;

        bump = removed.Length > 0 ? Bump.Major
             : added.Length > 0 ? Bump.Minor
             : modified.Length > 0 || retagged > 0 ? Bump.Patch
             : Bump.None;

        if (bump == Bump.None)
        {
            return "";
        }

        var sb = new StringBuilder();

        if (oldCatalog.Source != newCatalog.Source)
        {
            sb.Append($"Icons updated from {SourceLink(oldCatalog)} to {SourceLink(newCatalog)}.\n\n");
        }

        AppendSection(sb, "New icons 🎨", added, withLink: true);
        AppendSection(sb, "Modified icons 🔨", modified, withLink: true);

        if (removed.Length > 0)
        {
            sb.Append("> [!WARNING]\n");
            sb.Append("> Removed icons are a breaking change: code that uses them no longer compiles.\n\n");
        }

        AppendSection(sb, "Removed icons 🗑️", removed, withLink: false);

        if (retagged > 0)
        {
            sb.Append($"Search tags or categories updated for {retagged} icon{(retagged == 1 ? "" : "s")}.\n\n");
        }

        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, string title, TablerIconInfo[] icons, bool withLink)
    {
        if (icons.Length == 0)
        {
            return;
        }

        sb.Append($"## {title}\n\n");

        var collapse = icons.Length > CollapseThreshold;

        if (collapse)
        {
            sb.Append($"<details>\n<summary>{icons.Length} icons</summary>\n\n");
        }

        foreach (var icon in icons)
        {
            sb.Append(withLink
                ? $"- `{icon.KindName}` ([{icon.Name}]({IconPageUrl}{icon.Name}))\n"
                : $"- `{icon.KindName}`\n");
        }

        if (collapse)
        {
            sb.Append("\n</details>\n");
        }

        sb.Append('\n');
    }

    private static string SourceLink(IconCatalog catalog) =>
        catalog.SourceVersion is { } version ? $"[Tabler Icons {version}]({ReleasesUrl}{version})" : catalog.Source;

    private static string Increment(string version, Bump bump)
    {
        var core = version.TrimStart('v').Split('-', '+')[0];
        var parts = core.Split('.');

        if (parts.Length != 3 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor) || !int.TryParse(parts[2], out var patch))
        {
            throw new FormatException($"Invalid version '{version}'. Expected X.Y.Z.");
        }

        // While in 0.x, breaking changes bump the minor version (SemVer §4).
        if (bump == Bump.Major && major == 0)
        {
            bump = Bump.Minor;
        }

        return bump switch
        {
            Bump.Major => $"{major + 1}.0.0",
            Bump.Minor => $"{major}.{minor + 1}.0",
            _ => $"{major}.{minor}.{patch + 1}",
        };
    }
}
