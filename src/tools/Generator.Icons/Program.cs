using Generator.Icons;

const string Usage = """
    Usage:
      update   <icons.txt> [--version <tag>] [--force]
               Download a tabler-icons release (latest by default) and regenerate icons.txt.
               Does nothing when icons.txt was already generated from that release.

      generate <tabler-icons-dir> <icons.txt> [--source <label>]
               Regenerate icons.txt from a local tabler-icons checkout.

      build    <icons.txt> <Tabler.Avalonia project dir>
               Write the embedded binaries and TablerIconKind.g.cs (run by the build).

      changes  <old-icons.txt|-> <new-icons.txt> [--markdown <file>] [--current-version <version>]
               Summarize icon changes as Markdown and compute the next version. Use '-' when
               there is no previous version.
    """;

var positional = new List<string>();
var options = new Dictionary<string, string?>(StringComparer.Ordinal);

for (var i = 1; i < args.Length; i++)
{
    if (args[i].StartsWith("--", StringComparison.Ordinal))
    {
        var takesValue = args[i] is not "--force";
        options[args[i]] = takesValue && i + 1 < args.Length ? args[++i] : null;
    }
    else
    {
        positional.Add(args[i]);
    }
}

string? Option(string name) => options.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value) ? value : null;

try
{
    switch (args.FirstOrDefault())
    {
        case "update" when positional.Count == 1:
            await new UpdateCommand
            {
                OutputPath = positional[0],
                Version = Option("--version"),
                Force = options.ContainsKey("--force"),
            }.Execute();
            break;

        case "generate" when positional.Count == 2:
            new GenerateCommand { InputPath = positional[0], OutputPath = positional[1], Source = Option("--source") }.Execute();
            break;

        case "build" when positional.Count == 2:
            new BuildCommand { InputPath = positional[0], OutputPath = positional[1] }.Execute();
            break;

        case "changes" when positional.Count == 2:
            new ChangesCommand
            {
                OldPath = positional[0] == "-" ? null : positional[0],
                NewPath = positional[1],
                MarkdownPath = Option("--markdown"),
                CurrentVersion = Option("--current-version"),
            }.Execute();
            break;

        default:
            Console.Error.WriteLine(Usage);
            return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

return 0;
