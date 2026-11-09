using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Generator.Icons;

/// <summary>
/// Downloads a tabler-icons release from GitHub and regenerates the icons text file from it,
/// unless the file was already generated from that release.
/// </summary>
public sealed class UpdateCommand
{
    private const string Repository = "tabler/tabler-icons";

    /// <summary>The icons text file.</summary>
    public required string OutputPath { get; init; }

    /// <summary>The release tag to use (e.g. "v3.51.0"), or null for the latest release.</summary>
    public string? Version { get; init; }

    /// <summary>Regenerate even if the file is already at the requested release.</summary>
    public bool Force { get; init; }

    public async Task Execute()
    {
        using var http = CreateHttpClient();

        var version = Version ?? await GetLatestReleaseTag(http);
        var current = File.Exists(OutputPath) ? IconCatalog.Read(OutputPath).SourceVersion : null;

        Console.WriteLine($"Current: {current ?? "(none)"}, requested: {version}");

        ActionsOutput.Set("version", version);
        ActionsOutput.Set("previous-version", current ?? "");

        if (current == version && !Force)
        {
            Console.WriteLine("Icons are up to date.");
            ActionsOutput.Set("updated", "false");
            return;
        }

        var workDir = Path.Combine(Path.GetTempPath(), "tabler-icons-" + Guid.NewGuid().ToString("N"));

        try
        {
            await DownloadIcons(http, version, workDir);

            new GenerateCommand
            {
                InputPath = workDir,
                OutputPath = OutputPath,
                Source = IconCatalog.SourcePrefix + version,
            }.Execute();
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, recursive: true);
            }
        }

        ActionsOutput.Set("updated", "true");
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();

        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Tabler.Avalonia-Generator", "1.0"));

        // Optional: avoids the low anonymous API rate limit on shared CI runners.
        if (Environment.GetEnvironmentVariable("GITHUB_TOKEN") is { Length: > 0 } token)
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return http;
    }

    private static async Task<string> GetLatestReleaseTag(HttpClient http)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        return document.RootElement.GetProperty("tag_name").GetString()
               ?? throw new InvalidOperationException("The latest release has no tag name.");
    }

    /// <summary>
    /// Streams the release source archive and extracts only <c>icons/outline</c> and
    /// <c>icons/filled</c>, so nothing else of the (large) repository touches the disk.
    /// </summary>
    private static async Task DownloadIcons(HttpClient http, string version, string targetDir)
    {
        var url = $"https://codeload.github.com/{Repository}/tar.gz/refs/tags/{Uri.EscapeDataString(version)}";

        Console.WriteLine($"Downloading {url}...");

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var gzip = new GZipStream(await response.Content.ReadAsStreamAsync(), CompressionMode.Decompress);
        await using var tar = new TarReader(gzip);

        var count = 0;

        while (await tar.GetNextEntryAsync() is { } entry)
        {
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            {
                continue;
            }

            // Entries look like "tabler-icons-3.51.0/icons/outline/home.svg".
            var parts = entry.Name.Split('/');

            if (parts is not [_, "icons", "outline" or "filled", var file] || !file.EndsWith(".svg", StringComparison.Ordinal)
                || file.Contains("..") || file.Contains('\\'))
            {
                continue;
            }

            var directory = Path.Combine(targetDir, "icons", parts[2]);
            Directory.CreateDirectory(directory);

            await entry.ExtractToFileAsync(Path.Combine(directory, file), overwrite: true);
            count++;
        }

        if (count == 0)
        {
            throw new InvalidOperationException($"No icons found in the {version} archive.");
        }

        Console.WriteLine($"Extracted {count} SVG files.");
    }
}
