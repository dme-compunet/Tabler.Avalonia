namespace Generator.Icons;

/// <summary>
/// Writes step outputs when running in GitHub Actions (no-op elsewhere).
/// </summary>
public static class ActionsOutput
{
    public static void Set(string name, string value)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_OUTPUT") is not { Length: > 0 } path)
        {
            return;
        }

        if (value.Contains('\n'))
        {
            var delimiter = "EOF_" + Guid.NewGuid().ToString("N");
            File.AppendAllText(path, $"{name}<<{delimiter}\n{value}\n{delimiter}\n");
        }
        else
        {
            File.AppendAllText(path, $"{name}={value}\n");
        }
    }
}
